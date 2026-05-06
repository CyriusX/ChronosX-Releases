using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using TimeTrack.AgentService.Ipc.Handlers;

namespace TimeTrack.AgentService.Ipc.Handlers.Commands.Settings;

/// <summary>
/// Installs or removes auto-start so TimeTrack launches at every login.
///
/// macOS   — creates/removes a user-level LaunchAgent plist that launches the
///          Desktop app (best-effort, minimized) via /usr/bin/open.
/// Windows — creates/removes a Task Scheduler task ("ChronosX Desktop") that
///          runs TimeTrack.DesktopHost.exe --start-minimized at user logon.
/// </summary>
public sealed class SetLaunchAtLoginCommandHandler : IpcHandlerBase, IIpcCommandHandler
{
    public string CommandName => "SetLaunchAtLogin";

    private readonly ILogger<SetLaunchAtLoginCommandHandler> _logger;

    private static readonly string MacLabel     = "com.cyriusx.timetrack.desktop";
    private static readonly string MacPlistName = "com.cyriusx.timetrack.desktop.plist";
    private const string WinTaskName = "ChronosX Desktop";

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

        bool isNowEnabled = File.Exists(MacPlistPath());
        _logger.LogInformation("SetLaunchAtLogin (macOS): enabled={Enabled}", isNowEnabled);
        return SuccessResponse(requestId, new { enabled = isNowEnabled });
    }

    // -------------------------------------------------------------------------
    // Windows
    // -------------------------------------------------------------------------

    [System.Runtime.Versioning.SupportedOSPlatform("windows")]
    private IpcResponse HandleWindows(int requestId, bool enabled)
    {
        try
        {
            if (enabled)
                RegisterWindowsDesktopTask();
            else
                UnregisterWindowsDesktopTask();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "SetLaunchAtLogin (Windows): failed to update scheduled task");
            return SuccessResponse(requestId, new { enabled = false });
        }

        bool isNowEnabled = IsWindowsDesktopTaskEnabled();
        return SuccessResponse(requestId, new { enabled = isNowEnabled });
    }

    /// <summary>
    /// Returns the path of the executable to register in the Windows Scheduled Task.
    /// Prefers TimeTrack.DesktopHost.exe (the UI + tray) alongside the agent.
    /// </summary>
    private static string WindowsLaunchPath()
    {
        var agentPath = Process.GetCurrentProcess().MainModule?.FileName ?? string.Empty;
        var agentDir  = Path.GetDirectoryName(agentPath) ?? string.Empty;

        // Most installs place AgentService under {app}\service\ and DesktopHost at {app}\.
        var candidates = new[]
        {
            Path.Combine(agentDir, "TimeTrack.DesktopHost.exe"),
            Path.GetFullPath(Path.Combine(agentDir, "..", "TimeTrack.DesktopHost.exe")),
        };
        foreach (var c in candidates)
        {
            if (File.Exists(c))
                return c;
        }

        return agentPath;
    }

    [System.Runtime.Versioning.SupportedOSPlatform("windows")]
    private void RegisterWindowsDesktopTask()
    {
        var exePath = WindowsLaunchPath();
        var workDir = Path.GetDirectoryName(exePath) ?? AppContext.BaseDirectory;

        // Mirror the installer behavior: task at logon, highest, ignore new instances, small delay.
        var script =
            "$u = $env:USERNAME; " +
            $"$exe = '{EscapePwshSingleQuoted(exePath)}'; " +
            $"$wd = '{EscapePwshSingleQuoted(workDir)}'; " +
            "$a = New-ScheduledTaskAction -Execute $exe -Argument '--start-minimized' -WorkingDirectory $wd; " +
            "$t = New-ScheduledTaskTrigger -AtLogOn -User $u; $t.Delay = 'PT5S'; " +
            "$s = New-ScheduledTaskSettingsSet -AllowStartIfOnBatteries -DontStopIfGoingOnBatteries -ExecutionTimeLimit 0 -MultipleInstances IgnoreNew -StartWhenAvailable -RestartCount 10 -RestartInterval (New-TimeSpan -Minutes 1); " +
            "$p = New-ScheduledTaskPrincipal -UserId $u -LogonType Interactive -RunLevel Highest; " +
            $"Register-ScheduledTask -TaskName '{WinTaskName}' -Action $a -Trigger $t -Settings $s -Principal $p -Force | Out-Null";

        var (exitCode, stdout, stderr) = RunPowerShellEncoded(script);
        _logger.LogInformation("RegisterDesktopTask exit={Code} stdout={Out} stderr={Err}", exitCode, stdout, stderr);
    }

    [System.Runtime.Versioning.SupportedOSPlatform("windows")]
    private void UnregisterWindowsDesktopTask()
    {
        var script =
            $"Stop-ScheduledTask -TaskName '{WinTaskName}' -ErrorAction SilentlyContinue; " +
            $"Unregister-ScheduledTask -TaskName '{WinTaskName}' -Confirm:$false -ErrorAction SilentlyContinue";
        var (exitCode, stdout, stderr) = RunPowerShellEncoded(script);
        _logger.LogInformation("UnregisterDesktopTask exit={Code} stdout={Out} stderr={Err}", exitCode, stdout, stderr);
    }

    [System.Runtime.Versioning.SupportedOSPlatform("windows")]
    private static bool IsWindowsDesktopTaskEnabled()
    {
        try
        {
            var script =
                $"$t = Get-ScheduledTask -TaskName '{WinTaskName}' -ErrorAction SilentlyContinue; " +
                "if ($null -eq $t) { exit 1 }; " +
                "if ($t.Enabled -ne $true) { exit 2 }; exit 0";
            var (exitCode, _, _) = RunPowerShellEncoded(script);
            return exitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    // -------------------------------------------------------------------------
    // macOS LaunchAgent
    // -------------------------------------------------------------------------

    private void InstallLaunchAgent()
    {
        var execPath  = AgentExecutablePath();
        var plistPath = MacPlistPath();

        Directory.CreateDirectory(Path.GetDirectoryName(plistPath)!);

        // Best-effort: launch the desktop app minimized so the tray/menubar is available at login.
        // If we can't find the .app bundle, fall back to launching the agent itself.
        var bundlePath = TryResolveMacDesktopBundleFromAgent(execPath);
        var programArgs = bundlePath != null
            ? $"""
    <array>
        <string>/usr/bin/open</string>
        <string>-g</string>
        <string>-a</string>
        <string>{bundlePath}</string>
        <string>--args</string>
        <string>--start-minimized</string>
    </array>
"""
            : $"""
    <array>
        <string>{execPath}</string>
    </array>
""";

        var label = bundlePath != null ? MacLabel : "com.cyriusx.timetrack.agent";

        var plist = $"""
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN"
  "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
    <key>Label</key>
    <string>{label}</string>
    <key>ProgramArguments</key>
{programArgs}
    <key>RunAtLoad</key>
    <true/>
    <key>ThrottleInterval</key>
    <integer>5</integer>
    <key>StandardOutPath</key>
    <string>/tmp/timetrack-agent.log</string>
    <key>StandardErrorPath</key>
    <string>/tmp/timetrack-agent.log</string>
</dict>
</plist>
""";

        File.WriteAllText(plistPath, plist, Encoding.UTF8);
        Launchctl("load", "-w", plistPath);
        _logger.LogInformation("LaunchAgent installed at {Path} (bundle={Bundle})", plistPath, bundlePath ?? "agent-only");
    }

    private void UninstallLaunchAgent()
    {
        var plistPath = MacPlistPath();
        if (File.Exists(plistPath))
        {
            Launchctl("unload", "-w", plistPath);
            File.Delete(plistPath);
            _logger.LogInformation("LaunchAgent removed from {Path}", plistPath);
        }
    }

    private static string MacPlistPath()
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return Path.Combine(home, "Library", "LaunchAgents", MacPlistName);
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

    private static string? TryResolveMacDesktopBundleFromAgent(string agentExePath)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(agentExePath))
                return null;

            var dir = new DirectoryInfo(Path.GetDirectoryName(agentExePath) ?? ".");
            while (dir != null)
            {
                if (dir.FullName.EndsWith(".app", StringComparison.OrdinalIgnoreCase))
                    return dir.FullName;
                dir = dir.Parent;
            }
        }
        catch
        {
            // ignore
        }
        return null;
    }

    private static string EscapePwshSingleQuoted(string s) => s.Replace("'", "''");

    [System.Runtime.Versioning.SupportedOSPlatform("windows")]
    private static (int exitCode, string stdout, string stderr) RunPowerShellEncoded(string script)
    {
        var bytes = Encoding.Unicode.GetBytes(script);
        var encoded = Convert.ToBase64String(bytes);

        using var p = new Process();
        p.StartInfo = new ProcessStartInfo("powershell.exe")
        {
            RedirectStandardOutput = true,
            RedirectStandardError  = true,
            UseShellExecute        = false,
            CreateNoWindow         = true
        };
        p.StartInfo.ArgumentList.Add("-NonInteractive");
        p.StartInfo.ArgumentList.Add("-WindowStyle");
        p.StartInfo.ArgumentList.Add("Hidden");
        p.StartInfo.ArgumentList.Add("-ExecutionPolicy");
        p.StartInfo.ArgumentList.Add("Bypass");
        p.StartInfo.ArgumentList.Add("-EncodedCommand");
        p.StartInfo.ArgumentList.Add(encoded);

        p.Start();
        p.WaitForExit(10_000);

        var stdout = p.StandardOutput.ReadToEnd();
        var stderr = p.StandardError.ReadToEnd();
        return (p.ExitCode, stdout.Trim(), stderr.Trim());
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
