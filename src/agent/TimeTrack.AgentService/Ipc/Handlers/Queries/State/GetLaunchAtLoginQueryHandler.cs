using Microsoft.Extensions.Logging;
using TimeTrack.AgentService.Ipc.Handlers;

namespace TimeTrack.AgentService.Ipc.Handlers.Queries.State;

/// <summary>
/// Returns whether TimeTrack is configured to start automatically at login.
///
/// macOS   — checks for the desktop LaunchAgent plist in ~/Library/LaunchAgents.
/// Windows — checks for the "ChronosX Desktop" Task Scheduler task.
/// Other   — always returns false.
/// </summary>
public sealed class GetLaunchAtLoginQueryHandler : IpcHandlerBase, IIpcQueryHandler
{
    public string QueryName => "GetLaunchAtLogin";

    private const string MacPlistName = "com.cyriusx.timetrack.desktop.plist";
    private const string WinTaskName  = "ChronosX Desktop";

    public Task<IpcResponse> HandleAsync(IpcRequest request, CancellationToken ct)
    {
        bool enabled;

        if (OperatingSystem.IsMacOS())
        {
            var plistPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                "Library", "LaunchAgents", MacPlistName);
            enabled = File.Exists(plistPath);
        }
        else if (OperatingSystem.IsWindows())
        {
            enabled = IsWindowsDesktopTaskEnabled();
        }
        else
        {
            enabled = false;
        }

        return Task.FromResult(SuccessResponse(request.RequestId, new { enabled }));
    }

    [System.Runtime.Versioning.SupportedOSPlatform("windows")]
    private static bool IsWindowsDesktopTaskEnabled()
    {
        try
        {
            using var p = new System.Diagnostics.Process();
            p.StartInfo = new System.Diagnostics.ProcessStartInfo("powershell.exe")
            {
                RedirectStandardOutput = true,
                RedirectStandardError  = true,
                UseShellExecute        = false,
                CreateNoWindow         = true
            };
            var script =
                $"$t = Get-ScheduledTask -TaskName '{WinTaskName}' -ErrorAction SilentlyContinue; " +
                "if ($null -eq $t) { exit 1 }; " +
                "if ($t.Enabled -ne $true) { exit 2 }; exit 0";

            var bytes = System.Text.Encoding.Unicode.GetBytes(script);
            var encoded = Convert.ToBase64String(bytes);
            p.StartInfo.ArgumentList.Add("-NonInteractive");
            p.StartInfo.ArgumentList.Add("-ExecutionPolicy");
            p.StartInfo.ArgumentList.Add("Bypass");
            p.StartInfo.ArgumentList.Add("-EncodedCommand");
            p.StartInfo.ArgumentList.Add(encoded);

            p.Start();
            p.WaitForExit(5_000);
            return p.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }
}
