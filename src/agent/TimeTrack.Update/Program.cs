using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using TimeTrack.Update.Services;

namespace TimeTrack.Update;

/// <summary>
/// Update.exe - Standalone update installer for ChronosX
///
/// Usage:
///   update.exe --install --url {downloadUrl} --checksum {sha256} --version {version}
///   update.exe --rollback --backup-path {path}
///   update.exe --verify --installer-path {path} --checksum {sha256}
/// </summary>
internal class Program
{
    private static async Task<int> Main(string[] args)
    {
        // Parse arguments
        var options = ParseArguments(args);

        if (options.ShowHelp)
        {
            ShowHelp();
            return 0;
        }

        // Setup DI
        var services = new ServiceCollection();
        services.AddLogging(builder =>
        {
            builder.AddConsole();
            builder.SetMinimumLevel(LogLevel.Information);
        });
        services.AddSingleton<IUpdateOrchestrator, UpdateOrchestrator>();
        services.AddSingleton<IBackupManager, BackupManager>();
        services.AddSingleton<ISignatureVerifier, SignatureVerifier>();
        services.AddSingleton<IServiceController, WindowsServiceController>();

        var serviceProvider = services.BuildServiceProvider();
        var logger = serviceProvider.GetRequiredService<ILogger<Program>>();
        var orchestrator = serviceProvider.GetRequiredService<IUpdateOrchestrator>();

        try
        {
            if (options.VerifyOnly)
            {
                return await VerifyInstallerAsync(options, serviceProvider, logger);
            }

            if (options.Rollback)
            {
                return await RollbackAsync(options, orchestrator, logger);
            }

            if (options.Install)
            {
                return await InstallUpdateAsync(options, orchestrator, logger);
            }

            logger.LogError("No valid action specified. Use --install, --rollback, or --verify.");
            return 1;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Update failed with unexpected error");
            return 1;
        }
    }

    private static async Task<int> VerifyInstallerAsync(UpdateOptions options, ServiceProvider serviceProvider, ILogger logger)
    {
        if (string.IsNullOrEmpty(options.InstallerPath))
        {
            logger.LogError("--installer-path is required for verification");
            return 1;
        }

        if (string.IsNullOrEmpty(options.Checksum))
        {
            logger.LogError("--checksum is required for verification");
            return 1;
        }

        var verifier = serviceProvider.GetRequiredService<ISignatureVerifier>();

        logger.LogInformation("Verifying installer: {Path}", options.InstallerPath);

        // Check file exists
        if (!File.Exists(options.InstallerPath))
        {
            logger.LogError("Installer not found: {Path}", options.InstallerPath);
            return 1;
        }

        // Verify checksum
        var checksumValid = await verifier.VerifyChecksumAsync(options.InstallerPath, options.Checksum);
        if (!checksumValid)
        {
            logger.LogError("Checksum verification failed");
            return 1;
        }

        logger.LogInformation("Checksum verified successfully");

        // Verify signature (optional)
        if (options.VerifySignature)
        {
            var signatureResult = verifier.VerifySignature(options.InstallerPath);
            if (!signatureResult.IsValid)
            {
                logger.LogError("Signature verification failed: {Error}", signatureResult.ErrorMessage);
                return 1;
            }
            logger.LogInformation("Signature verified: {Publisher}", signatureResult.Publisher);
        }

        logger.LogInformation("Verification completed successfully");
        return 0;
    }

    private static async Task<int> RollbackAsync(UpdateOptions options, IUpdateOrchestrator orchestrator, ILogger logger)
    {
        logger.LogInformation("Starting rollback...");

        var backupPath = options.BackupPath;
        if (string.IsNullOrEmpty(backupPath))
        {
            // Find most recent backup
            backupPath = orchestrator.FindMostRecentBackup();
            if (backupPath == null)
            {
                logger.LogError("No backup found for rollback");
                return 1;
            }
            logger.LogInformation("Using most recent backup: {Path}", backupPath);
        }

        var result = await orchestrator.RollbackAsync(backupPath);

        if (result.Success)
        {
            logger.LogInformation("Rollback completed successfully to version {Version}", result.Version);
            return 0;
        }

        logger.LogError("Rollback failed: {Error}", result.ErrorMessage);
        return 1;
    }

    private static async Task<int> InstallUpdateAsync(UpdateOptions options, IUpdateOrchestrator orchestrator, ILogger logger)
    {
        if (string.IsNullOrEmpty(options.DownloadUrl))
        {
            logger.LogError("--url is required for installation");
            return 1;
        }

        if (string.IsNullOrEmpty(options.Checksum))
        {
            logger.LogError("--checksum is required for installation");
            return 1;
        }

        if (string.IsNullOrEmpty(options.Version))
        {
            logger.LogError("--version is required for installation");
            return 1;
        }

        logger.LogInformation("Starting update to version {Version}", options.Version);

        var progress = new Progress<UpdateProgress>(p =>
        {
            logger.LogInformation("[{Stage}] {Percentage}% - {Message}", p.Stage, p.Percentage, p.Message);
        });

        var result = await orchestrator.InstallUpdateAsync(
            options.DownloadUrl,
            options.Checksum,
            options.Version,
            options.VerifySignature,
            progress);

        if (result.Success)
        {
            logger.LogInformation("Update completed successfully to version {Version}", result.Version);
            return 0;
        }

        logger.LogError("Update failed: {Error}", result.ErrorMessage);

        if (result.CanRollback)
        {
            logger.LogWarning("Attempting automatic rollback...");
            var rollbackResult = await orchestrator.RollbackAsync(result.BackupPath!);
            if (rollbackResult.Success)
            {
                logger.LogInformation("Rollback completed successfully");
            }
            else
            {
                logger.LogError("Rollback also failed: {Error}", rollbackResult.ErrorMessage);
            }
        }

        return 1;
    }

    private static UpdateOptions ParseArguments(string[] args)
    {
        var options = new UpdateOptions();

        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i].ToLowerInvariant())
            {
                case "--help":
                case "-h":
                case "/?":
                    options.ShowHelp = true;
                    break;

                case "--install":
                case "-i":
                    options.Install = true;
                    break;

                case "--rollback":
                case "-r":
                    options.Rollback = true;
                    break;

                case "--verify":
                case "-v":
                    options.VerifyOnly = true;
                    break;

                case "--url":
                case "-u":
                    if (i + 1 < args.Length)
                        options.DownloadUrl = args[++i];
                    break;

                case "--checksum":
                case "-c":
                    if (i + 1 < args.Length)
                        options.Checksum = args[++i];
                    break;

                case "--version":
                    if (i + 1 < args.Length)
                        options.Version = args[++i];
                    break;

                case "--installer-path":
                    if (i + 1 < args.Length)
                        options.InstallerPath = args[++i];
                    break;

                case "--backup-path":
                    if (i + 1 < args.Length)
                        options.BackupPath = args[++i];
                    break;

                case "--no-signature-verify":
                    options.VerifySignature = false;
                    break;

                case "--silent":
                    options.Silent = true;
                    break;
            }
        }

        return options;
    }

    private static void ShowHelp()
    {
        Console.WriteLine(@"
ChronosX Update Installer

Usage:
  update.exe --install --url <url> --checksum <sha256> --version <version>
  update.exe --rollback [--backup-path <path>]
  update.exe --verify --installer-path <path> --checksum <sha256>

Options:
  --install, -i              Install an update
  --rollback, -r             Rollback to previous version
  --verify, -v               Verify installer integrity only

  --url, -u <url>            Download URL for the installer
  --checksum, -c <sha256>    SHA256 checksum of the installer
  --version <version>        Target version to install
  --installer-path <path>    Path to installer file (for verify)
  --backup-path <path>       Path to backup directory (for rollback)

  --no-signature-verify      Skip Authenticode signature verification
  --silent                   Run without UI prompts

  --help, -h                 Show this help message

Examples:
  update.exe --install --url https://cdn.example.com/ChronosX-Setup-1.2.0.exe --checksum abc123 --version 1.2.0
  update.exe --rollback
  update.exe --verify --installer-path C:\Temp\ChronosX-Setup-1.2.0.exe --checksum abc123
");
    }

    private class UpdateOptions
    {
        public bool ShowHelp { get; set; }
        public bool Install { get; set; }
        public bool Rollback { get; set; }
        public bool VerifyOnly { get; set; }
        public string? DownloadUrl { get; set; }
        public string? Checksum { get; set; }
        public string? Version { get; set; }
        public string? InstallerPath { get; set; }
        public string? BackupPath { get; set; }
        public bool VerifySignature { get; set; } = true;
        public bool Silent { get; set; }
    }
}
