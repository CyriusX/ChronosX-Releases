using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using TimeTrack.AgentService.Ipc.Handlers;

namespace TimeTrack.AgentService.Ipc.Handlers.Commands.Settings;

/// <summary>
/// Installs or removes auto-start so TimeTrack launches at every login.
///
/// macOS  — creates/removes a user-level LaunchAgent plist that runs the
///          background agent service via launchctl.
/// Windows — adds/removes a Run registry value (HKCU) that launches the
///           TimeTrack.DesktopHost.exe (or the agent if the desktop host
///           is not found alongside the agent executable).
/// </summary>
public sealed class SetLaunchAtLoginCommandHandler : IpcHandlerBase, IIpcCommandHandler
{
    public string CommandName => "SetLaunchAtLogin";

    private readonly ILogger<SetLaunchAtLoginCommandHandler> _logger;

    private static readonly string Label    = "com.cyriusx.timetrack.agent";
    private static readonly string PlistName = "com.cyriusx.timetrack.agent.plist";
    private const string WinRunKeyPath       = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string WinRunValueName     = "TimeTrack";

    public SetLaunchAtLoginCommandHandler(ILogger<SetLaunchAtLoginCommandHandler> logger)
    {
        _logger = logger;
    }

    public Task<IpcResponse> HandleAsync(IpcRequest request, CancellationToken ct)
    {
        bool enabled = false;
        if (request.Payload.HasValue &&
            request.Payload.Value.ValueKind == JsonValueKind.Object &&
            request.Payload.Value.TryGetProperty("enabled", out var prop))
        {
            enabled = prop.GetBoolean();
        }

        try
        {
            if (OperatingSystem.IsMacOS())
                return Task.FromResult(HandleMacOS(request.RequestId, enabled));

            if (OperatingSystem.IsWindows())
                return Task.FromResult(HandleWindows(request.RequestId, enabled));

            // Unsupported platform — report as disabled
            return Task.FromResult(SuccessResponse(request.RequestId, new { enabled = false }));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting launch-at-login");
            return Task.FromResult(UnknownErrorResponse(request.RequestId, ex));
        }
    }

    // -------------------------------------------------------------------------
    // macOS
    // -------------------------------------------------------------------------

    private IpcResponse HandleMacOS(int requestId, bool enabled)
    {
        if (enabled)
            InstallLaunchAgent();
        else
            UninstallLaunchAgent();

        bool isNowEnabled = File.Exists(PlistPath());
        _logger.LogInformation("SetLaunchAtLogin (macOS): enabled={Enabled}", isNowEnabled);
        return SuccessResponse(requestId, new { enabled = isNowEnabled });
    }

    // -------------------------------------------------------------------------
    // Windows
    // -------------------------------------------------------------------------

    [System.Runtime.Versioning.SupportedOSPlatform("windows")]
    private IpcResponse HandleWindows(int requestId, bool enabled)
    {
        using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(WinRunKeyPath, writable: true);
        if (key == null)
        {
            _logger.LogWarning("SetLaunchAtLogin (Windows): could not open Run registry key");
            return SuccessResponse(requestId, new { enabled = false });
        }

        if (enabled)
        {
            var launchPath = WindowsLaunchPath();
            key.SetValue(WinRunValueName, $"\"{launchPath}\"");
            _logger.LogInformation("SetLaunchAtLogin (Windows): registered \"{Path}\"", launchPath);
        }
        else
        {
            key.DeleteValue(WinRunValueName, throwOnMissingValue: false);
            _logger.LogInformation("SetLaunchAtLogin (Windows): removed registry value");
        }

        bool isNowEnabled = key.GetValue(WinRunValueName) != null;
        return SuccessResponse(requestId, new { enabled = isNowEnabled });
    }

    /// <summary>
    /// Returns the path of the executable to register in the Windows Run key.
    /// Prefers TimeTrack.DesktopHost.exe (the UI + tray) alongside the agent;
    /// falls back to the agent executable itself.
    /// </summary>
    private static string WindowsLaunchPath()
    {
        var agentPath = Process.GetCurrentProcess().MainModule?.FileName ?? string.Empty;
        var agentDir  = Path.GetDirectoryName(agentPath) ?? string.Empty;

        var desktopHost = Path.Combine(agentDir, "TimeTrack.DesktopHost.exe");
        if (File.Exists(desktopHost))
            return desktopHost;

        return agentPath;
    }

    // -------------------------------------------------------------------------
    // macOS LaunchAgent
    // -------------------------------------------------------------------------

    private void InstallLaunchAgent()
    {
        var execPath    = AgentExecutablePath();
        var contentRoot = Path.GetDirectoryName(execPath) ?? string.Empty;
        var plistPath   = PlistPath();

        Directory.CreateDirectory(Path.GetDirectoryName(plistPath)!);

        var plist = $"""
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN"
  "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
    <key>Label</key>
    <string>{Label}</string>
    <key>ProgramArguments</key>
    <array>
        <string>{execPath}</string>
    </array>
    <key>RunAtLoad</key>
    <true/>
    <key>KeepAlive</key>
    <true/>
    <key>ProcessType</key>
    <string>Background</string>
    <key>ThrottleInterval</key>
    <integer>5</integer>
    <key>StandardOutPath</key>
    <string>/tmp/timetrack-agent.log</string>
    <key>StandardErrorPath</key>
    <string>/tmp/timetrack-agent.log</string>
    <key>EnvironmentVariables</key>
    <dict>
        <key>DOTNET_ENVIRONMENT</key>
        <string>Production</string>
        <key>DOTNET_CONTENT_ROOT</key>
        <string>{contentRoot}</string>
    </dict>
</dict>
</plist>
""";
        File.WriteAllText(plistPath, plist, Encoding.UTF8);
        Launchctl("load", "-w", plistPath);
        _logger.LogInformation("LaunchAgent installed at {Path}", plistPath);
    }

    private void UninstallLaunchAgent()
    {
        var plistPath = PlistPath();
        if (File.Exists(plistPath))
        {
            Launchctl("unload", "-w", plistPath);
            File.Delete(plistPath);
            _logger.LogInformation("LaunchAgent removed from {Path}", plistPath);
        }
    }

    private static string PlistPath()
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return Path.Combine(home, "Library", "LaunchAgents", PlistName);
    }

    private static string AgentExecutablePath()
    {
        // With single-file publish, MainModule.FileName is the on-disk path of the
        // executable itself (not the temp extraction dir).
        var fromProcess = Process.GetCurrentProcess().MainModule?.FileName;
        if (!string.IsNullOrEmpty(fromProcess) && File.Exists(fromProcess))
            return fromProcess;

        // Fallback: use DOTNET_CONTENT_ROOT env var set by our LaunchAgent / build script
        var contentRoot = Environment.GetEnvironmentVariable("DOTNET_CONTENT_ROOT")
            ?? AppContext.BaseDirectory;
        return Path.Combine(contentRoot, "TimeTrack.MacOSAgentService");
    }

    private void Launchctl(params string[] args)
    {
        try
        {
            using var p = new Process();
            p.StartInfo = new ProcessStartInfo("/bin/launchctl")
            {
                RedirectStandardOutput = true,
                RedirectStandardError  = true,
                UseShellExecute        = false,
                CreateNoWindow         = true
            };
            foreach (var a in args) p.StartInfo.ArgumentList.Add(a);
            p.Start();
            p.WaitForExit(5_000);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "launchctl {Args} failed", string.Join(" ", args));
        }
    }
}
