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
        // Stop Windows service
        var serviceStopped = await _serviceController.StopServiceAsync(ServiceName, TimeSpan.FromSeconds(30), cancellationToken);
        if (!serviceStopped)
        {
            _logger.LogWarning("Failed to stop service {Service}, attempting force kill", ServiceName);
            ForceKillService(ServiceName);
        }

        // Kill any running DesktopHost processes
        KillProcess("TimeTrack.DesktopHost");
        KillProcess("ChronosX");

        // Wait for processes to fully terminate
        await Task.Delay(2000, cancellationToken);
    }

    private async Task StartAllServicesAsync(CancellationToken cancellationToken)
    {
        // Start Windows service
        var serviceStarted = await _serviceController.StartServiceAsync(ServiceName, TimeSpan.FromSeconds(30), cancellationToken);
        if (!serviceStarted)
        {
            _logger.LogError("Failed to start service {Service}", ServiceName);
            throw new InvalidOperationException($"Failed to start service {ServiceName}");
        }

        // Start DesktopHost
        var desktopHostPath = Path.Combine(_installPath, "TimeTrack.DesktopHost.exe");
        if (File.Exists(desktopHostPath))
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = desktopHostPath,
                UseShellExecute = true,
                WorkingDirectory = _installPath
            });
            _logger.LogInformation("Started DesktopHost");
        }
    }

    private async Task<bool> RunInstallerAsync(string installerPath, CancellationToken cancellationToken)
    {
        // Run Inno Setup installer in silent mode
        var startInfo = new ProcessStartInfo
        {
            FileName = installerPath,
            Arguments = "/VERYSILENT /SUPPRESSMSGBOXES /NORESTART /NOICONS",
            UseShellExecute = true,
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

    private void ForceKillService(string serviceName)
    {
        try
        {
            var process = Process.GetProcessesByName(serviceName).FirstOrDefault();
            process?.Kill();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to force kill service {Service}", serviceName);
        }
    }

    private void KillProcess(string processName)
    {
        try
        {
            foreach (var process in Process.GetProcessesByName(processName))
            {
                process.Kill();
                _logger.LogInformation("Killed process: {Process}", processName);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to kill process {Process}", processName);
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
