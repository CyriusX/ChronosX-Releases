using System.Diagnostics;
using System.Reflection;
using Microsoft.Extensions.Logging;
using TimeTrack.Agent.Contracts.Configuration;
using TimeTrack.Agent.Contracts.Services;
using TimeTrack.Agent.Contracts.Updates;

namespace TimeTrack.Agent.Application.Services;

/// <summary>
/// Implementation of update management
/// </summary>
public sealed class UpdateService : IUpdateService, IDisposable
{
    private readonly IUpdateHttpClient _httpClient;
    private readonly UpdateSettings _settings;
    private readonly ILogger<UpdateService> _logger;

    private readonly object _lock = new();
    private UpdateProgress? _currentProgress;
    private UpdateCheckResponse? _lastCheckResult;
    private bool _isUpdating;
    private CancellationTokenSource? _updateCts;

    public UpdateProgress? CurrentProgress
    {
        get { lock (_lock) { return _currentProgress; } }
    }

    public bool IsUpdating
    {
        get { lock (_lock) { return _isUpdating; } }
    }

    public UpdateCheckResponse? LastCheckResult
    {
        get { lock (_lock) { return _lastCheckResult; } }
    }

    public event EventHandler<UpdateProgress>? ProgressChanged;
    public event EventHandler<UpdateCheckResponse>? UpdateAvailable;
    public event EventHandler<UpdateResult>? UpdateCompleted;

    public UpdateService(
        IUpdateHttpClient httpClient,
        UpdateSettings settings,
        ILogger<UpdateService> logger)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<UpdateCheckResponse?> CheckForUpdatesAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Checking for updates...");

        SetProgress(new UpdateProgress
        {
            Stage = UpdateStage.Checking,
            Message = "Checking for updates..."
        });

        try
        {
            var currentVersion = GetCurrentVersion();
            var result = await _httpClient.CheckForUpdatesAsync(
                currentVersion,
                _settings.Channel,
                cancellationToken);

            lock (_lock) { _lastCheckResult = result; }

            if (result?.HasUpdate == true)
            {
                _logger.LogInformation("Update available: {Version}", result.LatestVersion);
                UpdateAvailable?.Invoke(this, result);
            }
            else
            {
                _logger.LogInformation("No update available");
            }

            SetProgress(new UpdateProgress
            {
                Stage = UpdateStage.Idle,
                Message = result?.HasUpdate == true ? "Update available" : "No update available"
            });

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to check for updates");
            SetProgress(new UpdateProgress { Stage = UpdateStage.Idle, Message = "Failed to check for updates" });
            return null;
        }
    }

    public async Task StartUpdateAsync(CancellationToken cancellationToken)
    {
        lock (_lock)
        {
            if (_isUpdating)
            {
                _logger.LogWarning("Update already in progress");
                return;
            }
            _isUpdating = true;
            _updateCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        }

        try
        {
            var checkResult = LastCheckResult;

            if (checkResult?.HasUpdate != true || string.IsNullOrEmpty(checkResult.DownloadUrl))
            {
                checkResult = await CheckForUpdatesAsync(cancellationToken);

                if (checkResult?.HasUpdate != true || string.IsNullOrEmpty(checkResult.DownloadUrl))
                {
                    _logger.LogWarning("No update available to install");
                    return;
                }
            }

            await PerformUpdateAsync(checkResult, _updateCts.Token);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Update cancelled");
            OnUpdateCompleted(new UpdateResult { Success = false, ErrorMessage = "Update cancelled" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Update failed");
            OnUpdateCompleted(new UpdateResult { Success = false, ErrorMessage = ex.Message });
        }
        finally
        {
            lock (_lock)
            {
                _isUpdating = false;
                _updateCts?.Dispose();
                _updateCts = null;
            }
        }
    }

    private async Task PerformUpdateAsync(UpdateCheckResponse updateInfo, CancellationToken cancellationToken)
    {
        var tempPath = Path.Combine(Path.GetTempPath(), "ChronosX-Update");
        var installerPath = Path.Combine(tempPath, $"ChronosX-Setup-{updateInfo.LatestVersion}.exe");

        try
        {
            SetProgress(new UpdateProgress
            {
                Stage = UpdateStage.Downloading,
                Message = "Downloading update...",
                TargetVersion = updateInfo.LatestVersion
            });

            var downloadProgress = new Progress<UpdateDownloadProgress>(p =>
            {
                SetProgress(new UpdateProgress
                {
                    Stage = UpdateStage.Downloading,
                    Percentage = p.Percentage,
                    BytesDownloaded = p.BytesDownloaded,
                    BytesTotal = p.BytesTotal,
                    BytesPerSecond = p.BytesPerSecond,
                    Message = $"Downloading... {p.Percentage}%",
                    TargetVersion = updateInfo.LatestVersion
                });
            });

            await _httpClient.DownloadUpdateAsync(
                updateInfo.DownloadUrl!,
                installerPath,
                downloadProgress,
                cancellationToken);

            SetProgress(new UpdateProgress
            {
                Stage = UpdateStage.Verifying,
                Message = "Verifying download...",
                TargetVersion = updateInfo.LatestVersion
            });

            SetProgress(new UpdateProgress
            {
                Stage = UpdateStage.Installing,
                Message = "Installing update...",
                TargetVersion = updateInfo.LatestVersion
            });

            var success = await LaunchUpdateExeAsync(installerPath, updateInfo, cancellationToken);

            if (success)
            {
                SetProgress(new UpdateProgress
                {
                    Stage = UpdateStage.Completed,
                    Message = "Update completed successfully!",
                    Percentage = 100,
                    TargetVersion = updateInfo.LatestVersion
                });

                OnUpdateCompleted(new UpdateResult
                {
                    Success = true,
                    Version = updateInfo.LatestVersion,
                    RestartRequired = true
                });
            }
            else
            {
                throw new InvalidOperationException("Update installer failed");
            }
        }
        finally
        {
            try
            {
                if (File.Exists(installerPath)) File.Delete(installerPath);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to cleanup temp files");
            }
        }
    }

    private async Task<bool> LaunchUpdateExeAsync(string installerPath, UpdateCheckResponse updateInfo, CancellationToken cancellationToken)
    {
        var updateExePath = FindUpdateExe();
        if (string.IsNullOrEmpty(updateExePath) || !File.Exists(updateExePath))
        {
            _logger.LogError("update.exe not found");
            return false;
        }

        var arguments = $"--install --url \"{updateInfo.DownloadUrl}\" --checksum \"{updateInfo.ChecksumSha256}\" --version \"{updateInfo.LatestVersion}\"";

        if (!_settings.VerifySignature)
        {
            arguments += " --no-signature-verify";
        }

        _logger.LogInformation("Launching update.exe: {Args}", arguments);

        var startInfo = new ProcessStartInfo
        {
            FileName = updateExePath,
            Arguments = arguments,
            UseShellExecute = true,
            Verb = "runas",
            WindowStyle = ProcessWindowStyle.Hidden
        };

        try
        {
            var process = Process.Start(startInfo);
            if (process == null)
            {
                _logger.LogError("Failed to start update.exe");
                return false;
            }

            await process.WaitForExitAsync(cancellationToken);

            _logger.LogInformation("update.exe exited with code: {Code}", process.ExitCode);
            return process.ExitCode == 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to launch update.exe");
            return false;
        }
    }

    private static string? FindUpdateExe()
    {
        var assemblyLocation = Assembly.GetExecutingAssembly().Location;
        var assemblyDir = Path.GetDirectoryName(assemblyLocation);

        if (!string.IsNullOrEmpty(assemblyDir))
        {
            var updateExe = Path.Combine(assemblyDir, "update.exe");
            if (File.Exists(updateExe)) return updateExe;
        }

        const string registryKey = @"SOFTWARE\Cyrius\TimeTrack";
        using var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(registryKey);
        var installPath = key?.GetValue("InstallPath") as string;

        if (!string.IsNullOrEmpty(installPath))
        {
            var updateExe = Path.Combine(installPath, "update.exe");
            if (File.Exists(updateExe)) return updateExe;
        }

        var defaultPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            "ChronosX",
            "update.exe");

        if (File.Exists(defaultPath)) return defaultPath;

        return null;
    }

    private static string GetCurrentVersion()
    {
        var assembly = Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly();
        var version = assembly.GetName().Version;
        return version?.ToString(3) ?? "1.0.0";
    }

    private void SetProgress(UpdateProgress progress)
    {
        lock (_lock) { _currentProgress = progress; }
        ProgressChanged?.Invoke(this, progress);
    }

    private void OnUpdateCompleted(UpdateResult result)
    {
        UpdateCompleted?.Invoke(this, result);
    }

    public void Dispose()
    {
        _updateCts?.Dispose();
    }
}
