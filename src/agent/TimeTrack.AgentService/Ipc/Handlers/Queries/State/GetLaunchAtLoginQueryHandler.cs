using Microsoft.Extensions.Logging;
using TimeTrack.AgentService.Ipc.Handlers;

namespace TimeTrack.AgentService.Ipc.Handlers.Queries.State;

/// <summary>
/// Returns whether TimeTrack is configured to start automatically at login.
///
/// macOS   — checks for the LaunchAgent plist in ~/Library/LaunchAgents.
/// Windows — checks the HKCU Run registry key for a "TimeTrack" entry.
/// Other   — always returns false.
/// </summary>
public sealed class GetLaunchAtLoginQueryHandler : IpcHandlerBase, IIpcQueryHandler
{
    public string QueryName => "GetLaunchAtLogin";

    private const string PlistName       = "com.cyriusx.timetrack.agent.plist";
    private const string WinRunKeyPath   = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string WinRunValueName = "TimeTrack";

    public Task<IpcResponse> HandleAsync(IpcRequest request, CancellationToken ct)
    {
        bool enabled;

        if (OperatingSystem.IsMacOS())
        {
            var plistPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                "Library", "LaunchAgents", PlistName);
            enabled = File.Exists(plistPath);
        }
        else if (OperatingSystem.IsWindows())
        {
            enabled = IsRegisteredInRunKey();
        }
        else
        {
            enabled = false;
        }

        return Task.FromResult(SuccessResponse(request.RequestId, new { enabled }));
    }

    [System.Runtime.Versioning.SupportedOSPlatform("windows")]
    private static bool IsRegisteredInRunKey()
    {
        try
        {
            using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(WinRunKeyPath);
            return key?.GetValue(WinRunValueName) != null;
        }
        catch
        {
            return false;
        }
    }
}
