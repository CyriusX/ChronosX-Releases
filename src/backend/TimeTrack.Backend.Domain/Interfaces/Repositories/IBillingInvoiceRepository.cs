using TimeTrack.Backend.Domain.Entities;

namespace TimeTrack.Backend.Domain.Interfaces.Repositories;

public interface IBillingInvoiceRepository
{
    Task<BillingInvoice?> GetByStripeInvoiceIdAsync(string stripeInvoiceId, CancellationToken ct = default);
    Task AddAsync(BillingInvoice invoice, CancellationToken ct = default);
    Task UpdateAsync(BillingInvoice invoice, CancellationToken ct = default);
    Task<IReadOnlyList<BillingInvoice>> GetByOrgIdAsync(Guid orgId, int limit = 50, CancellationToken ct = default);
    Task CancelByOrgIdAsync(Guid orgId, CancellationToken ct = default);
    Task<IReadOnlyList<BillingInvoice>> GetPaidByOrgIdAsync(Guid orgId, CancellationToken ct = default);
    Task MarkRefundedByOrgIdAsync(Guid orgId, CancellationToken ct = default);
}
