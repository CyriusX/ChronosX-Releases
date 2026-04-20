using MediatR;
using TimeTrack.Backend.Application.Billing.DTOs;
using TimeTrack.Backend.Application.Common.Exceptions;
using TimeTrack.Backend.Application.Common.Interfaces;
using TimeTrack.Backend.Domain.Interfaces.Repositories;

namespace TimeTrack.Backend.Application.Billing.Queries;

public sealed record GetInvoicesQuery(int Limit = 50) : IRequest<IReadOnlyList<InvoiceResponse>>;

public sealed class GetInvoicesQueryHandler : IRequestHandler<GetInvoicesQuery, IReadOnlyList<InvoiceResponse>>
{
    private readonly ICurrentUserContext _currentUser;
    private readonly IBillingInvoiceRepository _invoiceRepository;

    public GetInvoicesQueryHandler(
        ICurrentUserContext currentUser,
        IBillingInvoiceRepository invoiceRepository)
    {
        _currentUser = currentUser;
        _invoiceRepository = invoiceRepository;
    }

    public async Task<IReadOnlyList<InvoiceResponse>> Handle(GetInvoicesQuery request, CancellationToken cancellationToken)
    {
        if (!_currentUser.OrgId.HasValue)
            throw new ForbiddenException("User not authenticated");

        var invoices = await _invoiceRepository.GetByOrgIdAsync(
            _currentUser.OrgId.Value, request.Limit, cancellationToken);

        return invoices.Select(i => new InvoiceResponse
        {
            Id = i.Id,
            AmountCents = i.AmountCents,
            Currency = i.Currency,
            Status = i.Status,
            Description = i.Description,
            PlanName = i.PlanName,
            Quantity = i.Quantity,
            PeriodStart = i.PeriodStart,
            PeriodEnd = i.PeriodEnd,
            PdfUrl = i.PdfUrl,
            PaidAt = i.PaidAt,
            CreatedAt = i.CreatedAt,
            RefundStatus = i.RefundStatus
        }).ToList();
    }
}
