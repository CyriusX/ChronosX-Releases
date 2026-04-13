using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using TimeTrack.Agent.Contracts.Updates;

namespace TimeTrack.Update.Services;

/// <summary>
/// Main orchestrator for update installation
/// </summary>
public sealed class UpdateOrchestrator : IUpdateOrchestrator
{
    private const string ServiceName = "ChronosXAgent";
    private const string AppName = "ChronosX";

    private readonly ILogger<UpdateOrchestrator> _logger;
    private readonly IBackupManager _backupManager;
    private readonly ISignatureVerifier _signatureVerifier;
    private readonly IServiceController _serviceController;

    private readonly string _installPath;
    private readonly string _tempPath;

    public UpdateOrchestrator(
        ILogger<UpdateOrchestrator> logger,
        IBackupManager backupManager,
        ISignatureVerifier signatureVerifier,
        IServiceController serviceController)
    {
        _logger = logger;
        _backupManager = backupManager;
        _signatureVerifier = signatureVerifier;
        _serviceController = serviceController;

        // Determine installation path from registry or default
        _installPath = GetInstallationPath();
        _tempPath = Path.Combine(Path.GetTempPath(), "ChronosX-Update");
    }

    public async Task<UpdateResult> InstallUpdateAsync(
        string downloadUrl,
        string checksum,
        string targetVersion,
        bool verifySignature,
        IProgress<UpdateProgress>? progress,
        string? installerPath = null,
        CancellationToken cancellationToken = default)
    {
        var backupPath = string.Empty;

        try
        {
            // Ensure running as administrator
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return new UpdateResult
                {
                    Success = false,
                    ErrorMessage = "Update only supported on Windows"
                };
            }

            if (!IsRunningAsAdministrator())
            {
                return new UpdateResult
                {
                    Success = false,
                    ErrorMessage = "Update must run as administrator"
                };
            }

            // Step 1: Get installer (download or use provided path)
            if (string.IsNullOrEmpty(installerPath))
            {
                progress?.Report(new UpdateProgress
                {
                    Stage = UpdateStage.Downloading,
                    Message = "Downloading update...",
                    TargetVersion = targetVersion
                });

                installerPath = await DownloadInstallerAsync(downloadUrl, targetVersion, progress, cancellationToken);
            }
            else
            {
                // Verify the provided installer exists
                if (!File.Exists(installerPath))
                {
                    return new UpdateResult
                    {
                        Success = false,
                        ErrorMessage = $"Installer not found at: {installerPath}"
                    };
                }

                _logger.LogInformation("Using pre-downloaded installer: {Path}", installerPath);
            }

            // Step 2: Verify checksum
            progress?.Report(new UpdateProgress
            {
                Stage = UpdateStage.Verifying,
                Message = "Verifying download integrity...",
                TargetVersion = targetVersion
            });

            var checksumValid = await _signatureVerifier.VerifyChecksumAsync(installerPath, checksum, cancellationToken);
            if (!checksumValid)
            {
                return new UpdateResult
                {
                    Success = false,
                    ErrorMessage = "Checksum verification failed - file may be corrupted"
                };
            }

            // Step 3: Verify signature (optional)
            if (verifySignature)
            {
                progress?.Report(new UpdateProgress
                {
                    Stage = UpdateStage.Verifying,
                    Message = "Verifying digital signature...",
                    TargetVersion = targetVersion
                });

                var sigResult = _signatureVerifier.VerifySignature(installerPath);
                if (!sigResult.IsValid)
                {
                    return new UpdateResult
                    {
                        Success = false,
                        ErrorMessage = $"Signature verification failed: {sigResult.ErrorMessage}"
                    };
                }
                _logger.LogInformation("Signature verified: {Publisher}", sigResult.Publisher);
            }

            // Step 4: Create backup
            progress?.Report(new UpdateProgress
            {
                Stage = UpdateStage.BackingUp,
                Message = "Creating backup of current version...",
                TargetVersion = targetVersion
            });

            backupPath = await _backupManager.CreateBackupAsync(_installPath, GetCurrentVersion(), cancellationToken);
            _logger.LogInformation("Backup created at: {Path}", backupPath);

            // Step 5: Stop services
            progress?.Report(new UpdateProgress
            {
                Stage = UpdateStage.StoppingServices,
                Message = "Stopping services...",
                TargetVersion = targetVersion
            });

            await StopAllServicesAsync(cancellationToken);

            // Step 6: Install
            progress?.Report(new UpdateProgress
            {
                Stage = UpdateStage.Installing,
                Message = "Installing update...",
                TargetVersion = targetVersion
            });

            var installSuccess = await RunInstallerAsync(installerPath, cancellationToken);
            if (!installSuccess)
            {
                throw new InvalidOperationException("Installer execution failed");
            }

            // Step 7: Start services
            progress?.Report(new UpdateProgress
            {
                Stage = UpdateStage.StartingServices,
                Message = "Starting services...",
                TargetVersion = targetVersion
            });

            await StartAllServicesAsync(cancellationToken);

            // Step 8: Cleanup
            CleanupTempFiles(installerPath);
            await _backupManager.CleanupOldBackupsAsync(7, cancellationToken);

            progress?.Report(new UpdateProgress
            {
                Stage = UpdateStage.Completed,
                Message = "Update completed successfully!",
                Percentage = 100,
                TargetVersion = targetVersion
            });

            return new UpdateResult
            {
                Success = true,
                Version = targetVersion
            };
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Update was cancelled");

            // Attempt to restore services
            if (!string.IsNullOrEmpty(backupPath) && Directory.Exists(backupPath))
            {
                try
                {
                    await _backupManager.RestoreBackupAsync(backupPath, _installPath, CancellationToken.None);
                    await StartAllServicesAsync(CancellationToken.None);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to restore after cancellation");
                }
            }

            return new UpdateResult
            {
                Success = false,
                ErrorMessage = "Update was cancelled",
                CanRollback = !string.IsNullOrEmpty(backupPath),
                BackupPath = backupPath
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Update failed");

            return new UpdateResult
            {
                Success = false,
                ErrorMessage = ex.Message,
                CanRollback = !string.IsNullOrEmpty(backupPath),
                BackupPath = backupPath
            };
        }
    }

    public async Task<RollbackResult> RollbackAsync(string backupPath, IProgress<UpdateProgress>? progress, CancellationToken cancellationToken)
    {
        try
        {
            progress?.Report(new UpdateProgress
            {
                Stage = UpdateStage.RollingBack,
                Message = "Starting rollback..."
            });

            // Stop services
            await StopAllServicesAsync(cancellationToken);

            progress?.Report(new UpdateProgress
            {
                Stage = UpdateStage.RollingBack,
                Message = "Restoring previous version...",
                Percentage = 50
            });

            // Restore backup
            var success = await _backupManager.RestoreBackupAsync(backupPath, _installPath, cancellationToken);
            if (!success)
            {
                throw new InvalidOperationException("Failed to restore backup");
            }

            // Start services
            await StartAllServicesAsync(cancellationToken);

            progress?.Report(new UpdateProgress
            {
                Stage = UpdateStage.Completed,
                Message = "Rollback completed successfully!",
                Percentage = 100
            });

            // Get version from backup manifest
            var version = GetVersionFromBackup(backupPath);

            return new RollbackResult
            {
                Success = true,
                Version = version
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Rollback failed");
            return new RollbackResult
            {
                Success = false,
                ErrorMessage = ex.Message
            };
        }
    }

    public string? FindMostRecentBackup()
    {
        var backups = _backupManager.GetAvailableBackups()
            .OrderByDescending(b => b.CreatedAt)
            .ToList();

        return backups.FirstOrDefault()?.Path;
    }

    private async Task<string> DownloadInstallerAsync(
        string url,
        string version,
        IProgress<UpdateProgress>? progress,
        CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(_tempPath);

        var fileName = $"ChronosX-Setup-{version}.exe";
        var filePath = Path.Combine(_tempPath, fileName);

        using var httpClient = new HttpClient();
        httpClient.Timeout = TimeSpan.FromMinutes(30);

        using var response = await httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();

        var totalBytes = response.Content.Headers.ContentLength ?? 0;
        var totalBytesRead = 0L;

        await using var contentStream = await response.Content.ReadAsStreamAsync(cancellationToken);
        await using var fileStream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None);

        var buffer = new byte[8192];
        var bytesRead = 0;
        var lastReport = DateTime.MinValue;

        while ((bytesRead = await contentStream.ReadAsync(buffer, cancellationToken)) > 0)
        {
            await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken);
            totalBytesRead += bytesRead;

            // Report progress every 100ms
            if (DateTime.Now - lastReport > TimeSpan.FromMilliseconds(100))
            {
                var percentage = totalBytes > 0 ? (int)((totalBytesRead * 100) / totalBytes) : 0;
                progress?.Report(new UpdateProgress
                {
                    Stage = UpdateStage.Downloading,
                    Percentage = percentage,
                    BytesDownloaded = totalBytesRead,
                    BytesTotal = totalBytes,
                    Message = $"Downloading... {FormatBytes(totalBytesRead)} / {FormatBytes(totalBytes)}",
                    TargetVersion = version
                });
                lastReport = DateTime.Now;
            }
        }

        _logger.LogInformation("Downloaded installer to: {Path}", filePath);
        return filePath;
    }

    private async Task StopAllServicesAsync(CancellationToken cancellationToken)
    {
        // Step 1: Stop scheduled tasks FIRST to prevent auto-restart
        // Task Scheduler has RestartCount=10, RestartInterval=1min — kills are useless if tasks restart immediately
        StopScheduledTasks();

        // Step 2: Stop Windows service (may not exist if agent runs via Task Scheduler)
        // System.ServiceProcess may not be available in self-contained publish, so wrap in try-catch
        try
        {
            var serviceStopped = await _serviceController.StopServiceAsync(ServiceName, TimeSpan.FromSeconds(10), cancellationToken);
            if (!serviceStopped)
            {
                _logger.LogInformation("Service {Service} not found or not running — using force kill", ServiceName);
            }
        }
        catch (FileNotFoundException ex)
        {
            _logger.LogInformation("Windows Service API not available ({Message}) — skipping service stop, will use taskkill", ex.Message);
        }

        // Step 3: Force kill all processes using taskkill (more reliable than Process.Kill)
        ForceKillWithTaskKill("TimeTrack.AgentService.exe");
        ForceKillWithTaskKill("TimeTrack.DesktopHost.exe");

        // Step 4: Verify all processes are actually dead (with retry loop)
        var verified = await VerifyProcessesTerminatedAsync(TimeSpan.FromSeconds(15), cancellationToken);
        if (!verified)
        {
            throw new InvalidOperationException(
                "Failed to terminate all TimeTrack processes after 15 seconds. " +
                "Cannot safely install update while processes are running.");
        }

        _logger.LogInformation("All services stopped and verified terminated");
    }

    private void StopScheduledTasks()
    {
        try
        {
            var psCommand =
                "Stop-ScheduledTask -TaskName 'ChronosX Agent' -ErrorAction SilentlyContinue; " +
                "Stop-ScheduledTask -TaskName 'ChronosX Desktop' -ErrorAction SilentlyContinue";

            var startInfo = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = $"-NoProfile -NonInteractive -WindowStyle Hidden -ExecutionPolicy Bypass -Command \"{psCommand}\"",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            using var process = Process.Start(startInfo);
            process?.WaitForExit(TimeSpan.FromSeconds(15));

            _logger.LogInformation("Scheduled tasks stopped (exit: {Code})", process?.ExitCode ?? -1);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to stop scheduled tasks — continuing with process kill");
        }
    }

    private void ForceKillWithTaskKill(string exeName)
    {
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "taskkill.exe",
                Arguments = $"/F /IM {exeName} /T",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            using var process = Process.Start(startInfo);
            process?.WaitForExit(TimeSpan.FromSeconds(10));

            _logger.LogInformation("taskkill /F /IM {Exe} completed (exit: {Code})", exeName, process?.ExitCode ?? -1);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "taskkill failed for {Exe}, falling back to Process.Kill", exeName);

            // Fallback to Process.Kill
            var processName = Path.GetFileNameWithoutExtension(exeName);
            foreach (var proc in Process.GetProcessesByName(processName))
            {
                try { proc.Kill(); } catch { }
            }
        }
    }

    private async Task<bool> VerifyProcessesTerminatedAsync(TimeSpan timeout, CancellationToken cancellationToken)
    {
        var deadline = DateTime.UtcNow + timeout;
        var processNames = new[] { "TimeTrack.AgentService", "TimeTrack.DesktopHost" };

        while (DateTime.UtcNow < deadline)
        {
            var allDead = true;
            foreach (var name in processNames)
            {
                if (Process.GetProcessesByName(name).Length > 0)
                {
                    allDead = false;
                    break;
                }
            }

            if (allDead)
            {
                return true;
            }

            await Task.Delay(500, cancellationToken);
        }

        // Final check — log which processes are still alive
        foreach (var name in processNames)
        {
            var remaining = Process.GetProcessesByName(name);
            if (remaining.Length > 0)
            {
                _logger.LogError("Process {Name} still running after {Timeout}s (PIDs: {PIDs})",
                    name, (int)timeout.TotalSeconds, string.Join(", ", remaining.Select(p => p.Id)));
            }
        }

        return false;
    }

    private async Task StartAllServicesAsync(CancellationToken cancellationToken)
    {
        // Start via Task Scheduler (matches the real deployment architecture)
        StartScheduledTasks();

        // Give tasks a moment to launch processes
        await Task.Delay(3000, cancellationToken);

        // Verify processes started
        var agentRunning = Process.GetProcessesByName("TimeTrack.AgentService").Length > 0;
        var desktopRunning = Process.GetProcessesByName("TimeTrack.DesktopHost").Length > 0;

        if (!agentRunning)
        {
            _logger.LogWarning("AgentService not running after starting scheduled task — launching directly");
            LaunchProcessDirectly(Path.Combine(_installPath, "service", "TimeTrack.AgentService.exe"), _installPath + "\\service");
        }

        if (!desktopRunning)
        {
            _logger.LogWarning("DesktopHost not running after starting scheduled task — launching directly");
            LaunchProcessDirectly(Path.Combine(_installPath, "TimeTrack.DesktopHost.exe"), _installPath);
        }

        _logger.LogInformation("Services started (Agent: {Agent}, Desktop: {Desktop})", agentRunning, desktopRunning);
    }

    private void StartScheduledTasks()
    {
        try
        {
            var psCommand =
                "Start-ScheduledTask -TaskName 'ChronosX Agent' -ErrorAction SilentlyContinue; " +
                "Start-ScheduledTask -TaskName 'ChronosX Desktop' -ErrorAction SilentlyContinue";

            var startInfo = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = $"-NoProfile -NonInteractive -WindowStyle Hidden -ExecutionPolicy Bypass -Command \"{psCommand}\"",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            using var process = Process.Start(startInfo);
            process?.WaitForExit(TimeSpan.FromSeconds(15));

            _logger.LogInformation("Scheduled tasks started (exit: {Code})", process?.ExitCode ?? -1);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to start scheduled tasks — will try direct launch");
        }
    }

    private static void LaunchProcessDirectly(string exePath, string workingDir)
    {
        if (!File.Exists(exePath)) return;

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = exePath,
                UseShellExecute = true,
                WorkingDirectory = workingDir
            });
        }
        catch { }
    }

    private async Task<bool> RunInstallerAsync(string installerPath, CancellationToken cancellationToken)
    {
        // Run Inno Setup installer in silent mode
        var startInfo = new ProcessStartInfo
        {
            FileName = installerPath,
            Arguments = "/VERYSILENT /SUPPRESSMSGBOXES /NORESTART /NOICONS",
            UseShellExecute = true,
            Verb = "runas",
            WindowStyle = ProcessWindowStyle.Hidden
        };

        using var process = Process.Start(startInfo);
        if (process == null)
        {
            return false;
        }

        await process.WaitForExitAsync(cancellationToken);

        _logger.LogInformation("Installer exited with code: {Code}", process.ExitCode);
        return process.ExitCode == 0;
    }

    private void CleanupTempFiles(string installerPath)
    {
        try
        {
            if (File.Exists(installerPath))
            {
                File.Delete(installerPath);
            }

            if (Directory.Exists(_tempPath))
            {
                Directory.Delete(_tempPath, recursive: true);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to cleanup temp files");
        }
    }

    private static bool IsRunningAsAdministrator()
    {
        using var identity = System.Security.Principal.WindowsIdentity.GetCurrent();
        var principal = new System.Security.Principal.WindowsPrincipal(identity);
        return principal.IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator);
    }

    private static string GetInstallationPath()
    {
        // Try registry first
        const string registryKey = @"SOFTWARE\Cyrius\TimeTrack";
        const string valueName = "InstallPath";

        using var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(registryKey);
        var path = key?.GetValue(valueName) as string;

        if (!string.IsNullOrEmpty(path) && Directory.Exists(path))
        {
            return path;
        }

        // Fallback to default
        return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "ChronosX");
    }

    private static string GetCurrentVersion()
    {
        var installPath = GetInstallationPath();
        var assemblyPath = Path.Combine(installPath, "TimeTrack.DesktopHost.exe");

        if (File.Exists(assemblyPath))
        {
            var versionInfo = FileVersionInfo.GetVersionInfo(assemblyPath);
            return versionInfo.FileVersion ?? "1.0.0";
        }

        return "1.0.0";
    }

    private static string GetVersionFromBackup(string backupPath)
    {
        var manifestPath = Path.Combine(backupPath, "manifest.json");
        if (File.Exists(manifestPath))
        {
            try
            {
                var json = File.ReadAllText(manifestPath);
                var manifest = System.Text.Json.JsonSerializer.Deserialize<BackupManifest>(json);
                return manifest?.Version ?? "unknown";
            }
            catch
            {
                // Ignore
            }
        }

        // Try to extract from folder name
        var dirName = Path.GetFileName(backupPath.TrimEnd(Path.DirectorySeparatorChar));
        var parts = dirName?.Split('_');
        return parts?.FirstOrDefault() ?? "unknown";
    }

    private static string FormatBytes(long bytes)
    {
        string[] suffixes = { "B", "KB", "MB", "GB" };
        var i = 0;
        double size = bytes;

        while (size >= 1024 && i < suffixes.Length - 1)
        {
            size /= 1024;
            i++;
        }

        return $"{size:0.##} {suffixes[i]}";
    }

    private sealed class BackupManifest
    {
        public string Version { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public string[]? Files { get; set; }
    }
}
