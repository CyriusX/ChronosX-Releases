using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace TimeTrack.Update.Services;

/// <summary>
/// Manages backup and restore operations
/// </summary>
public sealed class BackupManager : IBackupManager
{
    private readonly ILogger<BackupManager> _logger;
    private readonly string _backupRoot;

    public string BackupDirectory => _backupRoot;

    public BackupManager(ILogger<BackupManager> logger)
    {
        _logger = logger;

        // Backup directory in LocalAppData
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        _backupRoot = Path.Combine(localAppData, "ChronosX", "backups");
    }

    public async Task<string> CreateBackupAsync(string installPath, string version, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(_backupRoot);

        // Create backup folder with version and timestamp
        var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        var backupName = $"{version}_{timestamp}";
        var backupPath = Path.Combine(_backupRoot, backupName);

        _logger.LogInformation("Creating backup at: {Path}", backupPath);

        Directory.CreateDirectory(backupPath);

        var manifest = new BackupManifest
        {
            Version = version,
            CreatedAt = DateTime.UtcNow,
            InstallPath = installPath
        };

        var filesCopied = 0;
        var totalSize = 0L;

        // Backup all files
        await Task.Run(() =>
        {
            // DesktopHost files
            var desktopHostPath = Path.Combine(backupPath, "DesktopHost");
            CopyDirectory(installPath, desktopHostPath, "*.exe", ref filesCopied, ref totalSize);
            CopyDirectory(installPath, desktopHostPath, "*.dll", ref filesCopied, ref totalSize);
            CopyDirectory(installPath, desktopHostPath, "*.json", ref filesCopied, ref totalSize);

            // Service files
            var servicePath = Path.Combine(installPath, "service");
            if (Directory.Exists(servicePath))
            {
                var serviceBackupPath = Path.Combine(backupPath, "service");
                CopyDirectory(servicePath, serviceBackupPath, ref filesCopied, ref totalSize);
            }

            // UI files (optional - can be large)
            var uiPath = Path.Combine(installPath, "ui", "dist");
            if (Directory.Exists(uiPath))
            {
                var uiBackupPath = Path.Combine(backupPath, "ui");
                CopyDirectory(uiPath, uiBackupPath, ref filesCopied, ref totalSize);
            }
        }, cancellationToken);

        manifest.FilesCount = filesCopied;
        manifest.TotalSizeBytes = totalSize;

        // Write manifest
        var manifestPath = Path.Combine(backupPath, "manifest.json");
        var manifestJson = JsonSerializer.Serialize(manifest, new JsonSerializerOptions
        {
            WriteIndented = true
        });
        await File.WriteAllTextAsync(manifestPath, manifestJson, cancellationToken);

        _logger.LogInformation("Backup created: {Files} files, {Size} bytes", filesCopied, totalSize);

        return backupPath;
    }

    public async Task<bool> RestoreBackupAsync(string backupPath, string installPath, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Restoring backup from: {Path}", backupPath);

        if (!Directory.Exists(backupPath))
        {
            _logger.LogError("Backup not found: {Path}", backupPath);
            return false;
        }

        // Read manifest
        var manifestPath = Path.Combine(backupPath, "manifest.json");
        BackupManifest? manifest = null;

        if (File.Exists(manifestPath))
        {
            var manifestJson = await File.ReadAllTextAsync(manifestPath, cancellationToken);
            manifest = JsonSerializer.Deserialize<BackupManifest>(manifestJson);
        }

        try
        {
            await Task.Run(() =>
            {
                // Restore DesktopHost files
                var desktopHostBackup = Path.Combine(backupPath, "DesktopHost");
                if (Directory.Exists(desktopHostBackup))
                {
                    CopyDirectory(desktopHostBackup, installPath, overwrite: true);
                }

                // Restore service files
                var serviceBackup = Path.Combine(backupPath, "service");
                var serviceTarget = Path.Combine(installPath, "service");
                if (Directory.Exists(serviceBackup))
                {
                    Directory.CreateDirectory(serviceTarget);
                    CopyDirectory(serviceBackup, serviceTarget, overwrite: true);
                }

                // Restore UI files
                var uiBackup = Path.Combine(backupPath, "ui");
                var uiTarget = Path.Combine(installPath, "ui", "dist");
                if (Directory.Exists(uiBackup))
                {
                    Directory.CreateDirectory(uiTarget);
                    CopyDirectory(uiBackup, uiTarget, overwrite: true);
                }
            }, cancellationToken);

            _logger.LogInformation("Backup restored successfully");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to restore backup");
            return false;
        }
    }

    public async Task CleanupOldBackupsAsync(int retentionDays, CancellationToken cancellationToken)
    {
        if (!Directory.Exists(_backupRoot))
        {
            return;
        }

        var cutoffDate = DateTime.UtcNow.AddDays(-retentionDays);
        var deletedCount = 0;

        await Task.Run(() =>
        {
            foreach (var dir in Directory.GetDirectories(_backupRoot))
            {
                try
                {
                    var manifestPath = Path.Combine(dir, "manifest.json");
                    DateTime createdAt;

                    if (File.Exists(manifestPath))
                    {
                        var json = File.ReadAllText(manifestPath);
                        var manifest = JsonSerializer.Deserialize<BackupManifest>(json);
                        createdAt = manifest?.CreatedAt ?? Directory.GetCreationTimeUtc(dir);
                    }
                    else
                    {
                        createdAt = Directory.GetCreationTimeUtc(dir);
                    }

                    if (createdAt < cutoffDate)
                    {
                        Directory.Delete(dir, recursive: true);
                        deletedCount++;
                        _logger.LogInformation("Deleted old backup: {Path}", dir);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to process backup directory: {Path}", dir);
                }
            }
        }, cancellationToken);

        if (deletedCount > 0)
        {
            _logger.LogInformation("Cleaned up {Count} old backups", deletedCount);
        }
    }

    public IEnumerable<BackupInfo> GetAvailableBackups()
    {
        if (!Directory.Exists(_backupRoot))
        {
            return Enumerable.Empty<BackupInfo>();
        }

        return Directory.GetDirectories(_backupRoot)
            .Select(ParseBackupInfo)
            .Where(b => b != null)
            .Cast<BackupInfo>()
            .OrderByDescending(b => b.CreatedAt);
    }

    private BackupInfo? ParseBackupInfo(string path)
    {
        try
        {
            var manifestPath = Path.Combine(path, "manifest.json");
            string version;
            DateTime createdAt;
            long size = 0;

            if (File.Exists(manifestPath))
            {
                var json = File.ReadAllText(manifestPath);
                var manifest = JsonSerializer.Deserialize<BackupManifest>(json);
                version = manifest?.Version ?? "unknown";
                createdAt = manifest?.CreatedAt ?? Directory.GetCreationTimeUtc(path);
                size = manifest?.TotalSizeBytes ?? GetDirectorySize(path);
            }
            else
            {
                // Parse from directory name
                var dirName = Path.GetFileName(path.TrimEnd(Path.DirectorySeparatorChar));
                var parts = dirName?.Split('_');

                version = parts?.FirstOrDefault() ?? "unknown";
                createdAt = Directory.GetCreationTimeUtc(path);
                size = GetDirectorySize(path);
            }

            return new BackupInfo
            {
                Path = path,
                Version = version,
                CreatedAt = createdAt,
                SizeBytes = size
            };
        }
        catch
        {
            return null;
        }
    }

    private static void CopyDirectory(string sourceDir, string destDir, ref int filesCopied, ref long totalSize)
    {
        Directory.CreateDirectory(destDir);

        foreach (var file in Directory.GetFiles(sourceDir))
        {
            var fileName = Path.GetFileName(file);
            var destFile = Path.Combine(destDir, fileName);
            File.Copy(file, destFile, overwrite: true);
            filesCopied++;
            totalSize += new FileInfo(file).Length;
        }

        foreach (var dir in Directory.GetDirectories(sourceDir))
        {
            var dirName = Path.GetFileName(dir);
            var destSubDir = Path.Combine(destDir, dirName!);
            CopyDirectory(dir, destSubDir, ref filesCopied, ref totalSize);
        }
    }

    private static void CopyDirectory(string sourceDir, string destDir, string searchPattern, ref int filesCopied, ref long totalSize)
    {
        Directory.CreateDirectory(destDir);

        foreach (var file in Directory.GetFiles(sourceDir, searchPattern))
        {
            var fileName = Path.GetFileName(file);
            var destFile = Path.Combine(destDir, fileName);
            File.Copy(file, destFile, overwrite: true);
            filesCopied++;
            totalSize += new FileInfo(file).Length;
        }
    }

    private static void CopyDirectory(string sourceDir, string destDir, bool overwrite = true)
    {
        Directory.CreateDirectory(destDir);

        foreach (var file in Directory.GetFiles(sourceDir))
        {
            var fileName = Path.GetFileName(file);
            var destFile = Path.Combine(destDir, fileName);
            File.Copy(file, destFile, overwrite);
        }

        foreach (var dir in Directory.GetDirectories(sourceDir))
        {
            var dirName = Path.GetFileName(dir);
            var destSubDir = Path.Combine(destDir, dirName!);
            CopyDirectory(dir, destSubDir, overwrite);
        }
    }

    private static long GetDirectorySize(string path)
    {
        try
        {
            return Directory.GetFiles(path, "*", SearchOption.AllDirectories)
                .Sum(f => new FileInfo(f).Length);
        }
        catch
        {
            return 0;
        }
    }

    private sealed class BackupManifest
    {
        public string Version { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public string InstallPath { get; set; } = string.Empty;
        public int FilesCount { get; set; }
        public long TotalSizeBytes { get; set; }
        public string[]? Files { get; set; }
    }
}
