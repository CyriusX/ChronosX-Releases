using Microsoft.Extensions.Logging;
using TimeTrack.Agent.Contracts.Repositories;

namespace TimeTrack.Agent.Application.UseCases.IdleJustification;

public sealed class DismissIdleJustificationRequest
{
    public Guid IdlePeriodId { get; init; }
}

/// <summary>
/// Dismisses an idle justification prompt locally so it is not shown again for that idle period.
/// </summary>
public sealed class DismissIdleJustificationUseCase
{
    private readonly IIdlePeriodRepository _idlePeriodRepository;
    private readonly ILogger<DismissIdleJustificationUseCase> _logger;

    public DismissIdleJustificationUseCase(
        IIdlePeriodRepository idlePeriodRepository,
        ILogger<DismissIdleJustificationUseCase> logger)
    {
        _idlePeriodRepository = idlePeriodRepository;
        _logger = logger;
    }

    public async Task ExecuteAsync(DismissIdleJustificationRequest request, CancellationToken cancellationToken = default)
    {
        var idlePeriod = await _idlePeriodRepository.GetByIdAsync(request.IdlePeriodId, cancellationToken);
        if (idlePeriod is null)
        {
            _logger.LogWarning("Cannot dismiss idle justification. Idle period {IdlePeriodId} was not found", request.IdlePeriodId);
            return;
        }

        idlePeriod.DismissJustification();
        await _idlePeriodRepository.UpdateAsync(idlePeriod, cancellationToken);
    }
}
