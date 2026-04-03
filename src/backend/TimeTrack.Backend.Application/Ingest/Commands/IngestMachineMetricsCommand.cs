using MediatR;
using Microsoft.Extensions.Logging;
using TimeTrack.Backend.Application.Common.Interfaces;
using TimeTrack.Backend.Application.Ingest.DTOs;
using TimeTrack.Backend.Domain.Entities;
using TimeTrack.Backend.Domain.Interfaces.Repositories;

namespace TimeTrack.Backend.Application.Ingest.Commands;

/// <summary>
/// Command para ingestão de métricas de máquina
/// </summary>
public sealed class IngestMachineMetricsCommand : IRequest<IngestResponse>
{
    public required IEnumerable<MachineMetricsItem> Items { get; init; }
}

/// <summary>
/// Handler para ingestão de métricas de máquina
/// </summary>
public sealed class IngestMachineMetricsCommandHandler : IRequestHandler<IngestMachineMetricsCommand, IngestResponse>
{
    private readonly IMachineMetricsRepository _metricsRepository;
    private readonly IIdempotencyKeyRepository _idempotencyKeyRepository;
    private readonly ICurrentUserContext _currentUser;
    private readonly ILogger<IngestMachineMetricsCommandHandler> _logger;

    private const string EntityType = "MachineMetrics";

    public IngestMachineMetricsCommandHandler(
        IMachineMetricsRepository metricsRepository,
        IIdempotencyKeyRepository idempotencyKeyRepository,
        ICurrentUserContext currentUser,
        ILogger<IngestMachineMetricsCommandHandler> logger)
    {
        _metricsRepository = metricsRepository;
        _idempotencyKeyRepository = idempotencyKeyRepository;
        _currentUser = currentUser;
        _logger = logger;
    }

    public async Task<IngestResponse> Handle(
        IngestMachineMetricsCommand request,
        CancellationToken cancellationToken)
    {
        var items = request.Items.ToList();
        var processed = 0;
        var duplicates = 0;
        var errors = new List<IngestError>();

        if (!_currentUser.OrgId.HasValue || !_currentUser.UserId.HasValue)
        {
            throw new UnauthorizedAccessException("User context not available");
        }

        var orgId = _currentUser.OrgId.Value;

        var deviceIdClaim = _currentUser.DeviceId;
        if (!deviceIdClaim.HasValue)
        {
            throw new UnauthorizedAccessException("Device ID not found in token");
        }

        // Batch check idempotency keys
        var keysToCheck = items.Select(i => i.IdempotencyKey).Distinct();
        var existingKeys = await _idempotencyKeyRepository.GetExistingKeysAsync(
            keysToCheck,
            EntityType,
            cancellationToken);

        foreach (var item in items)
        {
            try
            {
                if (existingKeys.Contains(item.IdempotencyKey))
                {
                    duplicates++;
                    continue;
                }

                var metrics = MachineMetrics.Create(
                    item.Id,
                    orgId,
                    deviceIdClaim.Value,
                    item.CpuPercent,
                    item.MemoryUsedMb,
                    item.MemoryTotalMb,
                    item.DiskUsedGb,
                    item.DiskTotalGb,
                    item.SampledAt,
                    item.IdempotencyKey);

                var idempotencyKey = IdempotencyKey.Create(
                    orgId,
                    item.IdempotencyKey,
                    EntityType,
                    item.Id);

                await _metricsRepository.AddAsync(metrics, cancellationToken);
                await _idempotencyKeyRepository.AddAsync(idempotencyKey, cancellationToken);

                existingKeys.Add(item.IdempotencyKey);
                processed++;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing machine metrics item {ItemId}", item.Id);
                errors.Add(new IngestError
                {
                    ItemId = item.Id,
                    Code = "PROCESSING_ERROR",
                    Message = ex.Message
                });
            }
        }

        _logger.LogInformation(
            "Ingested machine metrics: {Processed} processed, {Duplicates} duplicates, {Errors} errors",
            processed, duplicates, errors.Count);

        return new IngestResponse
        {
            Processed = processed,
            Duplicates = duplicates,
            Errors = errors
        };
    }
}
