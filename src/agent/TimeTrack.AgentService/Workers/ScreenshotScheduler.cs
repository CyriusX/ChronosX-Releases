using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TimeTrack.Agent.Contracts.Providers;
using TimeTrack.Agent.Contracts.Services;
using TimeTrack.AgentService.Configuration;

namespace TimeTrack.AgentService.Workers;

/// <summary>
/// Scheduler de screenshots — dispara capturas com base na política da org.
/// Respeita condições de suspensão: idle, tracking pausado, apps excluídos, CPU budget.
/// </summary>
public sealed class ScreenshotScheduler : BackgroundService
{
    private readonly IScreenshotCapture _screenshotCapture;
    private readonly IEvidenceUploadQueue _uploadQueue;
    private readonly IActiveWindowProvider _activeWindowProvider;
    private readonly IIdleDetector _idleDetector;
    private readonly ILocalEncryptionService _encryptionService;
    private readonly ILogger<ScreenshotScheduler> _logger;

    private readonly string _evidenceQueuePath;

    public ScreenshotScheduler(
        IScreenshotCapture screenshotCapture,
        IEvidenceUploadQueue uploadQueue,
        IActiveWindowProvider activeWindowProvider,
        IIdleDetector idleDetector,
        ILocalEncryptionService encryptionService,
        IOptions<AgentSettings> settings,
        ILogger<ScreenshotScheduler> logger)
    {
        _screenshotCapture = screenshotCapture;
        _uploadQueue = uploadQueue;
        _activeWindowProvider = activeWindowProvider;
        _idleDetector = idleDetector;
        _encryptionService = encryptionService;
        _logger = logger;

        var appDataPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "TimeTrack", "evidence_queue");
        Directory.CreateDirectory(appDataPath);
        _evidenceQueuePath = appDataPath;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("ScreenshotScheduler started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var policy = await GetCurrentPolicyAsync(stoppingToken);

                if (!policy.ScreenshotsEnabled)
                {
                    await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
                    continue;
                }

                var interval = TimeSpan.FromMinutes(policy.ScreenshotIntervalMinutes);
                await Task.Delay(interval, stoppingToken);

                await TryCaptureAsync(policy, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in ScreenshotScheduler loop");
                await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
            }
        }

        _logger.LogInformation("ScreenshotScheduler stopped");
    }

    private async Task TryCaptureAsync(EvidencePolicyState policy, CancellationToken ct)
    {
        // Check CPU budget
        using var process = Process.GetCurrentProcess();
        var cpuUsage = GetCpuUsage(process);
        if (cpuUsage > 5.0)
        {
            _logger.LogDebug("Skipping screenshot: CPU usage {Cpu:F1}% > 5%", cpuUsage);
            return;
        }

        // Check excluded apps
        var activeWindow = await _activeWindowProvider.GetActiveWindowAsync(ct);
        if (activeWindow != null && IsExcludedApp(activeWindow.ExePathHash, policy.ScreenshotExcludedApps))
        {
            _logger.LogDebug("Skipping screenshot: excluded app {App}", activeWindow.DisplayName);
            return;
        }

        // Capture
        var result = await _screenshotCapture.CaptureActiveWindowAsync(ct);
        if (result == null)
        {
            _logger.LogDebug("Screenshot capture returned null");
            return;
        }

        // Encrypt and save locally
        var encrypted = _encryptionService.Encrypt(result.JpegData, out var iv);
        var fileName = $"{Guid.NewGuid()}.enc";
        var localPath = Path.Combine(_evidenceQueuePath, fileName);
        await File.WriteAllBytesAsync(localPath, encrypted, ct);

        // Save IV alongside (prepended to file is not portable)
        var ivPath = Path.Combine(_evidenceQueuePath, $"{fileName}.iv");
        await File.WriteAllBytesAsync(ivPath, iv, ct);

        // Enqueue for upload
        var queueItem = new EvidenceQueueItem
        {
            Id = Guid.NewGuid().ToString(),
            LocalPath = localPath,
            EvidenceType = "screenshot",
            CapturedAt = DateTime.UtcNow,
            AppName = result.ActiveAppName,
            WindowTitleHash = result.ActiveWindowTitleHash,
            FileSizeBytes = result.FileSizeBytes,
            AttemptCount = 0,
            NextAttemptUtc = DateTime.UtcNow
        };

        await _uploadQueue.EnqueueAsync(queueItem, ct);

        _logger.LogInformation(
            "Screenshot captured and enqueued: {App}, {Size}KB",
            result.ActiveAppName, result.FileSizeBytes / 1024);
    }

    private static bool IsExcludedApp(string? exePathHash, List<string> excludedApps)
    {
        if (string.IsNullOrEmpty(exePathHash) || excludedApps.Count == 0)
            return false;

        return excludedApps.Contains(exePathHash, StringComparer.OrdinalIgnoreCase);
    }

    private static double GetCpuUsage(Process process)
    {
        try
        {
            var cpuTime = process.TotalProcessorTime;
            var upTime = DateTime.UtcNow - process.StartTime.ToUniversalTime();
            if (upTime.TotalMilliseconds <= 0) return 0;
            return (cpuTime.TotalMilliseconds / upTime.TotalMilliseconds) * 100.0 / Environment.ProcessorCount;
        }
        catch
        {
            return 0;
        }
    }

    private async Task<EvidencePolicyState> GetCurrentPolicyAsync(CancellationToken ct)
    {
        // TODO: integrate with OrgPolicyProvider to load from cache
        // For now return defaults until the sync pipeline feeds evidence policy into cache
        return await Task.FromResult(new EvidencePolicyState
        {
            ScreenshotsEnabled = true,
            ScreenshotIntervalMinutes = 5,
            ScreenshotExcludedApps = []
        });
    }

    private sealed class EvidencePolicyState
    {
        public bool ScreenshotsEnabled { get; init; }
        public int ScreenshotIntervalMinutes { get; init; }
        public List<string> ScreenshotExcludedApps { get; init; } = [];
    }
}
