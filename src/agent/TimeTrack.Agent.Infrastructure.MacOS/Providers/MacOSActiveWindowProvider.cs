using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TimeTrack.Agent.Contracts.Providers;
using TimeTrack.Agent.Infrastructure.MacOS.Interop;

namespace TimeTrack.Agent.Infrastructure.MacOS.Providers;

/// <summary>
/// Implementation of IActiveWindowProvider for macOS using NSWorkspace and AXUIElement API
/// </summary>
[SupportedOSPlatform("macos")]
public sealed class MacOSActiveWindowProvider : IActiveWindowProvider, IDisposable
{
    private readonly ILogger<MacOSActiveWindowProvider> _logger;
    private readonly ActiveWindowProviderOptions _options;
    private readonly IFilePathExtractor _filePathExtractor;
    private readonly int _currentProcessId;

    private ActiveWindowInfo? _cachedWindow;
    private DateTime _lastCacheUpdate = DateTime.MinValue;
    private readonly object _cacheLock = new();

    private bool _disposed;

    /// <summary>
    /// Fired when the active window changes (for subscribers)
    /// </summary>
    public event EventHandler<ActiveWindowChangedEventArgs>? ActiveWindowChanged;

    public MacOSActiveWindowProvider(
        ILogger<MacOSActiveWindowProvider> logger,
        IFilePathExtractor filePathExtractor,
        IOptions<ActiveWindowProviderOptions>? options = null)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _filePathExtractor = filePathExtractor ?? throw new ArgumentNullException(nameof(filePathExtractor));
        _options = options?.Value ?? new ActiveWindowProviderOptions();
        _currentProcessId = Environment.ProcessId;

        // Ask macOS to prompt the user for Accessibility permission if not already granted.
        // Without it, window titles come back empty; the app name and bundle path are still
        // captured so tracking remains functional.
        AccessibilityPermission.CheckAndPrompt(prompt: true, _logger);
    }

    /// <inheritdoc />
    public Task<ActiveWindowInfo?> GetActiveWindowAsync(CancellationToken cancellationToken = default)
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(MacOSActiveWindowProvider));

        if (IsCacheStale())
        {
            var info = PollActiveWindow();
            return Task.FromResult(info);
        }

        lock (_cacheLock)
        {
            return Task.FromResult(_cachedWindow);
        }
    }

    private bool IsCacheStale()
    {
        lock (_cacheLock)
        {
            return (DateTime.UtcNow - _lastCacheUpdate).TotalMilliseconds > _options.CacheValidityMs;
        }
    }

    private ActiveWindowInfo? PollActiveWindow()
    {
        try
        {
            var (pid, appName) = GetFrontmostApplication();

            if (pid == 0 || string.IsNullOrEmpty(appName))
            {
                _logger.LogDebug("Could not get frontmost application");
                return null;
            }

            var exePath = GetApplicationPath(pid);
            if (string.IsNullOrEmpty(exePath))
            {
                _logger.LogDebug("Could not get exe path for process {ProcessId}/{AppName}", pid, appName);
                return null;
            }

            _logger.LogDebug("Frontmost app: {App} (PID={Pid}, Path={Path})", appName, pid, exePath);

            if (IsOwnAppBundle(exePath))
            {
                _logger.LogDebug("Filtered as own app bundle: {Path}", exePath);
                return null;
            }

            var windowTitle = GetActiveWindowTitle(pid);
            var info = BuildActiveWindowInfo(pid, appName, exePath, windowTitle);

            if (info != null)
            {
                _logger.LogInformation(
                    "Tracked active window: {App} (PID={Pid}, Title={Title})",
                    appName, pid, windowTitle ?? "(null)");
                UpdateCache(info);
            }

            return info;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error polling active window");
            return null;
        }
    }

    private ActiveWindowInfo? BuildActiveWindowInfo(int pid, string appName, string exePath, string? windowTitle)
    {
        try
        {
            var exePathHash = ComputeSha256Hash(exePath);
            var windowHash = !string.IsNullOrEmpty(windowTitle)
                ? ComputeSha256Hash(windowTitle)
                : null;

            var processName = Path.GetFileNameWithoutExtension(exePath) ?? "";
            var filePath = _filePathExtractor.ExtractFilePath(IntPtr.Zero, processName, windowTitle);

            string? browserUrl = null;
            if (IsBrowserBundle(appName, exePath))
            {
                browserUrl = ExtractBrowserUrl(pid, windowTitle);
            }

            var displayName = GetDisplayName(appName, exePath);

            return new ActiveWindowInfo
            {
                ExePathHash = exePathHash,
                DisplayName = displayName ?? appName,
                ExePath = exePath,
                WindowTitle = windowTitle,
                WindowHash = windowHash,
                FilePath = filePath,
                BrowserUrl = browserUrl
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error building ActiveWindowInfo for process {ProcessId}", pid);
            return null;
        }
    }

    // Cached Objective-C class/selector handles — these are interned by the runtime
    // and cheap to look up, but we cache to avoid string marshalling on every call.
    private static readonly IntPtr s_nsWorkspaceClass = ObjCRuntime.GetClass("NSWorkspace");
    private static readonly IntPtr s_nsRunningAppClass = ObjCRuntime.GetClass("NSRunningApplication");
    private static readonly IntPtr s_selSharedWorkspace = ObjCRuntime.GetSelector("sharedWorkspace");
    private static readonly IntPtr s_selFrontmostApp = ObjCRuntime.GetSelector("frontmostApplication");
    private static readonly IntPtr s_selProcessIdentifier = ObjCRuntime.GetSelector("processIdentifier");
    private static readonly IntPtr s_selLocalizedName = ObjCRuntime.GetSelector("localizedName");
    private static readonly IntPtr s_selBundleURL = ObjCRuntime.GetSelector("bundleURL");
    private static readonly IntPtr s_selPath = ObjCRuntime.GetSelector("path");
    private static readonly IntPtr s_selRunningAppWithPid = ObjCRuntime.GetSelector("runningApplicationWithProcessIdentifier:");

    private (int pid, string? appName) GetFrontmostApplication()
    {
        try
        {
            if (s_nsWorkspaceClass == IntPtr.Zero)
            {
                _logger.LogWarning("NSWorkspace class not found — AppKit may not be loaded");
                return (0, null);
            }

            // [NSWorkspace sharedWorkspace]
            var workspace = ObjCRuntime.SendMessage(s_nsWorkspaceClass, s_selSharedWorkspace);
            if (workspace == IntPtr.Zero)
            {
                _logger.LogDebug("[NSWorkspace sharedWorkspace] returned nil");
                return (0, null);
            }

            // [workspace frontmostApplication] → NSRunningApplication*
            var runningApp = ObjCRuntime.SendMessage(workspace, s_selFrontmostApp);
            if (runningApp == IntPtr.Zero)
            {
                _logger.LogDebug("[workspace frontmostApplication] returned nil");
                return (0, null);
            }

            // [runningApp processIdentifier] → pid_t (int32)
            var pid = ObjCRuntime.SendMessageInt(runningApp, s_selProcessIdentifier);
            if (pid == 0)
            {
                _logger.LogDebug("[runningApp processIdentifier] returned 0");
                return (0, null);
            }

            // [runningApp localizedName] → NSString*
            var nsName = ObjCRuntime.SendMessage(runningApp, s_selLocalizedName);
            var appName = ObjCRuntime.NSStringToManaged(nsName);

            // Fallback to proc_name if localizedName wasn't available
            if (string.IsNullOrEmpty(appName))
                appName = GetProcessName(pid);

            return (pid, appName);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Error getting frontmost application");
            return (0, null);
        }
    }

    private string? GetApplicationPath(int pid)
    {
        try
        {
            // [NSRunningApplication runningApplicationWithProcessIdentifier:pid]
            var runningApp = ObjCRuntime.SendMessage(s_nsRunningAppClass, s_selRunningAppWithPid, pid);
            if (runningApp == IntPtr.Zero)
                return null;

            // [runningApp bundleURL] → NSURL*
            var bundleURL = ObjCRuntime.SendMessage(runningApp, s_selBundleURL);
            if (bundleURL == IntPtr.Zero)
                return null;

            // [bundleURL path] → NSString*
            var nsPath = ObjCRuntime.SendMessage(bundleURL, s_selPath);
            return ObjCRuntime.NSStringToManaged(nsPath);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Error getting application path for PID {Pid}", pid);
            return null;
        }
    }

    private unsafe string? GetProcessName(int pid)
    {
        try
        {
            var bufferSize = 256;
            var buffer = stackalloc byte[bufferSize];

            var length = proc_name(pid, buffer, bufferSize);
            if (length == 0)
                return null;

            return Marshal.PtrToStringUTF8((IntPtr)buffer, length);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Error getting process name for PID {Pid}", pid);
            return null;
        }
    }

    private string? GetActiveWindowTitle(int pid)
    {
        try
        {
            var appRef = AXUIElementCreateApplication(pid);
            if (appRef == IntPtr.Zero)
                return null;

            try
            {
                var focusedWindowRef = IntPtr.Zero;
                var result = AXUIElementCopyAttributeValue(
                    appRef,
                    kAXFocusedWindowAttribute,
                    out focusedWindowRef);

                if (result != 0 || focusedWindowRef == IntPtr.Zero)
                    return null;

                try
                {
                    var titleValue = IntPtr.Zero;
                    result = AXUIElementCopyAttributeValue(
                        focusedWindowRef,
                        kAXTitleAttribute,
                        out titleValue);

                    if (result != 0 || titleValue == IntPtr.Zero)
                        return null;

                    try
                    {
                        return ObjCRuntime.NSStringToManaged(titleValue);
                    }
                    finally
                    {
                        CFRelease(titleValue);
                    }
                }
                finally
                {
                    CFRelease(focusedWindowRef);
                }
            }
            finally
            {
                CFRelease(appRef);
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Error getting active window title for PID {Pid}", pid);
            return null;
        }
    }

    private static string? GetDisplayName(string appName, string exePath)
    {
        try
        {
            var bundlePath = exePath.EndsWith(".app") ? exePath : Path.GetDirectoryName(exePath);
            if (bundlePath?.EndsWith(".app") == true)
            {
                var infoPlistPath = Path.Combine(bundlePath, "Contents", "Info.plist");
                if (File.Exists(infoPlistPath))
                {
                    var content = File.ReadAllText(infoPlistPath);
                    var match = System.Text.RegularExpressions.Regex.Match(content,
                        "<key>CFBundleName</key>\\s*<string>([^<]+)</string>");
                    if (match.Success)
                    {
                        return match.Groups[1].Value;
                    }
                }
            }

            return Path.GetFileNameWithoutExtension(appName);
        }
        catch
        {
            return Path.GetFileNameWithoutExtension(appName);
        }
    }

    private static bool IsBrowserBundle(string appName, string exePath)
    {
        var lower = exePath.ToLowerInvariant();
        return lower.Contains("safari.app") ||
               lower.Contains("chrome.app") ||
               lower.Contains("firefox.app") ||
               lower.Contains("brave.app") ||
               lower.Contains("opera.app") ||
               lower.Contains("edge.app") ||
               lower.Contains("chromium.app");
    }

    private static bool IsOwnAppBundle(string exePath)
    {
        var lower = exePath.ToLowerInvariant();
        return lower.Contains("timetrack.app") ||
               lower.Contains("timetrack-desktophost") ||
               lower.Contains("timetrack.macosdesktophost") ||
               lower.Contains("chronosx timetrack");
    }

    private static string? ExtractBrowserUrl(int pid, string? windowTitle)
    {
        // Browser URL extraction via AppleScript is not yet wired up on macOS.
        // The previous implementation DllImported NSAppleScript as C functions,
        // which does not work because NSAppleScript is an Objective-C class.
        // We would need to go through the ObjC runtime or shell out to /usr/bin/osascript.
        // Returning null keeps tracking functional — window title alone is still captured.
        return null;
    }

    private static string ComputeSha256Hash(string input)
    {
        var bytes = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private void UpdateCache(ActiveWindowInfo info)
    {
        lock (_cacheLock)
        {
            _cachedWindow = info;
            _lastCacheUpdate = DateTime.UtcNow;
        }
    }

    private void RaiseWindowChangedEvent(ActiveWindowInfo info)
    {
        try
        {
            ActiveWindowChanged?.Invoke(this, new ActiveWindowChangedEventArgs(info));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in ActiveWindowChanged event handler");
        }
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        GC.SuppressFinalize(this);
    }

    #region Native Interop

    // libproc: pure C API, works fine.
    [DllImport("/usr/lib/libproc.dylib")]
    private static extern unsafe int proc_name(int pid, byte* buffer, int bufferSize);

    // Accessibility API: exported as C functions by HIServices, not the non-existent
    // "Accessibility.framework/Accessibility" path used previously.
    private const string HIServicesLib =
        "/System/Library/Frameworks/ApplicationServices.framework/Frameworks/HIServices.framework/HIServices";

    [DllImport(HIServicesLib)]
    private static extern IntPtr AXUIElementCreateApplication(int pid);

    [DllImport(HIServicesLib)]
    private static extern int AXUIElementCopyAttributeValue(IntPtr element, IntPtr attribute, out IntPtr value);

    [DllImport("/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation")]
    private static extern void CFRelease(IntPtr cf);

    private static readonly IntPtr kAXFocusedWindowAttribute = CoreFoundationNative.CFStringCreate("AXFocusedWindow");
    private static readonly IntPtr kAXTitleAttribute = CoreFoundationNative.CFStringCreate("AXTitle");

    #endregion
}

/// <summary>
/// Configuration options for ActiveWindowProvider
/// </summary>
public sealed class ActiveWindowProviderOptions
{
    public int CacheValidityMs { get; set; } = 500;
}

/// <summary>
/// Event arguments for active window change
/// </summary>
public sealed class ActiveWindowChangedEventArgs : EventArgs
{
    public ActiveWindowInfo WindowInfo { get; }
    public DateTime TimestampUtc { get; }

    public ActiveWindowChangedEventArgs(ActiveWindowInfo windowInfo)
    {
        WindowInfo = windowInfo;
        TimestampUtc = DateTime.UtcNow;
    }
}
