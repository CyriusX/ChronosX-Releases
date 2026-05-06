using Microsoft.EntityFrameworkCore;
using TimeTrack.Backend.Domain.Entities;
using TimeTrack.Backend.Domain.Interfaces.Repositories;
using TimeTrack.Backend.Infrastructure.Persistence;

namespace TimeTrack.Backend.Infrastructure.Repositories;

public sealed class BillingInvoiceRepository : IBillingInvoiceRepository
{
    private readonly TimeTrackDbContext _context;

    public BillingInvoiceRepository(TimeTrackDbContext context)
    {
        _context = context;
    }

    public async Task<BillingInvoice?> GetByStripeInvoiceIdAsync(string stripeInvoiceId, CancellationToken ct = default)
    {
        return await _context.BillingInvoices
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(i => i.StripeInvoiceId == stripeInvoiceId, ct);
    }

    public async Task AddAsync(BillingInvoice invoice, CancellationToken ct = default)
    {
        await _context.BillingInvoices.AddAsync(invoice, ct);
        await _context.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(BillingInvoice invoice, CancellationToken ct = default)
    {
        _context.BillingInvoices.Update(invoice);
        await _context.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<BillingInvoice>> GetByOrgIdAsync(Guid orgId, int limit = 50, CancellationToken ct = default)
    {
        return await _context.BillingInvoices
            .Where(i => i.OrgId == orgId)
            .OrderByDescending(i => i.CreatedAt)
            .Take(limit)
            .ToListAsync(ct);
    }

    public async Task CancelByOrgIdAsync(Guid orgId, CancellationToken ct = default)
    {
        var invoices = await _context.BillingInvoices
            .IgnoreQueryFilters()
            .Where(i => i.OrgId == orgId && i.Status != "cancelled")
            .ToListAsync(ct);

        foreach (var invoice in invoices)
            invoice.MarkCancelled();

        await _context.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<BillingInvoice>> GetPaidByOrgIdAsync(Guid orgId, CancellationToken ct = default)
    {
        return await _context.BillingInvoices
            .IgnoreQueryFilters()
            .Where(i => i.OrgId == orgId && i.Status == "paid" && i.RefundStatus != "refunded")
            .ToListAsync(ct);
    }

    public async Task MarkRefundedByOrgIdAsync(Guid orgId, CancellationToken ct = default)
    {
        var invoices = await _context.BillingInvoices
            .IgnoreQueryFilters()
            .Where(i => i.OrgId == orgId && i.Status == "paid" && i.RefundStatus != "refunded")
            .ToListAsync(ct);

        foreach (var invoice in invoices)
            invoice.MarkRefunded(invoice.AmountCents, $"bulk_refund_{Guid.NewGuid():N}");

        await _context.SaveChangesAsync(ct);
    }
}
