using System.Text.Json;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TimeTrack.Agent.Contracts.Providers;
using TimeTrack.Agent.Contracts.Repositories;
using TimeTrack.Agent.Contracts.Services;
using TimeTrack.Agent.Domain.Entities;
using TimeTrack.Agent.Domain.Services;

namespace TimeTrack.AgentService.Workers;

/// <summary>
/// Worker que coleta métricas de máquina (CPU, Memória, Disco) periodicamente
/// e enfileira no outbox para sincronização com o backend.
///
/// - Amostra a cada 10s, mantém buffer circular com as últimas 6 leituras
/// - A cada 60s, agrega e cria um OutboxItem para sync
/// </summary>
public sealed class MachineMetricsWorker : BackgroundService
{
    private static readonly TimeSpan SampleInterval = TimeSpan.FromSeconds(10);
    private const int BufferSize = 6; // 6 samples × 10s = 60s window
    private const int FlushEveryNSamples = 6; // flush every 60s

    private readonly ILogger<MachineMetricsWorker> _logger;
    private readonly IMachineMetricsProvider _metricsProvider;
    private readonly IOutboxRepository _outboxRepository;
    private readonly IIdempotencyKeyGenerator _idempotencyKeyGenerator;
    private readonly IAgentEventLogger _eventLogger;

    private readonly MachineMetricsReading[] _buffer = new MachineMetricsReading[BufferSize];
    private int _bufferIndex;
    private int _sampleCount;

    public MachineMetricsWorker(
        ILogger<MachineMetricsWorker> logger,
        IMachineMetricsProvider metricsProvider,
        IOutboxRepository outboxRepository,
        IIdempotencyKeyGenerator idempotencyKeyGenerator,
        IAgentEventLogger eventLogger)
    {
        _logger = logger;
        _metricsProvider = metricsProvider;
        _outboxRepository = outboxRepository;
        _idempotencyKeyGenerator = idempotencyKeyGenerator;
        _eventLogger = eventLogger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("MachineMetricsWorker iniciando. Intervalo de amostra: {Interval}s", SampleInterval.TotalSeconds);

        using var timer = new PeriodicTimer(SampleInterval);

        try
        {
            while (!stoppingToken.IsCancellationRequested &&
                   await timer.WaitForNextTickAsync(stoppingToken))
            {
                await SampleAndMaybeFlushAsync(stoppingToken);
            }
        }
        catch (OperationCanceledException)
        {
            // Expected on shutdown
        }
        catch (Exception ex)
        {
            _logger.LogCritical(ex, "MachineMetricsWorker crashed");
            throw;
        }

        _logger.LogInformation("MachineMetricsWorker encerrado");
    }

    private async Task SampleAndMaybeFlushAsync(CancellationToken cancellationToken)
    {
        try
        {
            var reading = await _metricsProvider.GetCurrentAsync(cancellationToken);

            _buffer[_bufferIndex] = reading;
            _bufferIndex = (_bufferIndex + 1) % BufferSize;
            _sampleCount++;

            if (_sampleCount % FlushEveryNSamples == 0)
            {
                await FlushToOutboxAsync(cancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Erro ao coletar métricas de máquina");
            await _eventLogger.LogAsync("worker.crash", AgentEventCategory.Error, AgentEventSeverity.Error,
                $"MachineMetricsWorker exception: {ex.Message}",
                new { worker = "MachineMetricsWorker", error = ex.Message }, cancellationToken);
        }
    }

    private async Task FlushToOutboxAsync(CancellationToken cancellationToken)
    {
        try
        {
            // Compute aggregated snapshot from buffer
            var validReadings = _buffer.Where(r => r != null).ToList();
            if (validReadings.Count == 0) return;

            var avgCpu = Math.Round(validReadings.Average(r => r.CpuPercent), 1);
            var latest = validReadings.OrderByDescending(r => r.SampledAtUtc).First();

            var snapshotId = Guid.NewGuid();
            var sampledAt = latest.SampledAtUtc;

            var payload = new
            {
                Id = snapshotId,
                CpuPercent = avgCpu,
                latest.MemoryUsedMb,
                latest.MemoryTotalMb,
                latest.DiskUsedGb,
                latest.DiskTotalGb,
                SampledAt = sampledAt
            };

            var payloadJson = JsonSerializer.Serialize(payload);
            var idempotencyKey = _idempotencyKeyGenerator.Generate(
                "machine_metrics", snapshotId, sampledAt);

            var outboxItem = OutboxItem.Create(
                "machine_metrics",
                snapshotId,
                payloadJson,
                idempotencyKey);

            await _outboxRepository.AddAsync(outboxItem, cancellationToken);

            _logger.LogInformation(
                "Métricas enfileiradas no outbox: CPU={Cpu}%, Mem={MemUsed}/{MemTotal}MB, Disk={DiskUsed}/{DiskTotal}GB (snapshotId={SnapshotId})",
                avgCpu, latest.MemoryUsedMb, latest.MemoryTotalMb,
                latest.DiskUsedGb, latest.DiskTotalGb, snapshotId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Erro ao enfileirar métricas no outbox");
        }
    }
}
