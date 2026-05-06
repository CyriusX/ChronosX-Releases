using Microsoft.Extensions.Logging;
using TimeTrack.Agent.Contracts.Repositories;

namespace TimeTrack.Agent.Application.UseCases.IdleJustification;

/// <summary>
/// Marks a local idle period as pending justification so the desktop host can prompt the user.
/// </summary>
public sealed class MarkIdleJustificationPendingUseCase
{
    private readonly IIdlePeriodRepository _idlePeriodRepository;
    private readonly ILogger<MarkIdleJustificationPendingUseCase> _logger;

    public MarkIdleJustificationPendingUseCase(
        IIdlePeriodRepository idlePeriodRepository,
        ILogger<MarkIdleJustificationPendingUseCase> logger)
    {
        _idlePeriodRepository = idlePeriodRepository;
        _logger = logger;
    }

    public async Task ExecuteAsync(Guid idlePeriodId, CancellationToken cancellationToken = default)
    {
        var idlePeriod = await _idlePeriodRepository.GetByIdAsync(idlePeriodId, cancellationToken);
        if (idlePeriod is null)
        {
            _logger.LogWarning("Cannot mark idle justification pending. Idle period {IdlePeriodId} was not found", idlePeriodId);
            return;
        }

        idlePeriod.MarkJustificationPending();
        await _idlePeriodRepository.UpdateAsync(idlePeriod, cancellationToken);
    }
}
