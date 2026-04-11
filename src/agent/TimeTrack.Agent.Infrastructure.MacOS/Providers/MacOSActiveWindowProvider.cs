using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TimeTrack.Agent.Contracts.Providers;

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
                _logger.LogDebug("Could not get exe path for process {ProcessId}", pid);
                return null;
            }

            var windowTitle = GetActiveWindowTitle(pid);
            var info = BuildActiveWindowInfo(pid, appName, exePath, windowTitle);

            if (info != null)
            {
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

    private (int pid, string? appName) GetFrontmostApplication()
    {
        try
        {
            var pid = NSWorkspace_FrontmostApplicationPid();
            if (pid == 0)
                return (0, null);

            var appName = GetProcessName(pid);
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
            var bufferSize = 1024;
            var buffer = stackalloc byte[bufferSize];

            var length = NSWorkspace_GetApplicationPathForPid(pid, buffer, bufferSize);
            if (length == 0)
                return null;

            return Marshal.PtrToStringUTF8((IntPtr)buffer, length);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Error getting application path for PID {Pid}", pid);
            return null;
        }
    }

    private string? GetProcessName(int pid)
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
                        var title = CFStringToString(titleValue);
                        return title;
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

    private static string? ExtractBrowserUrl(int pid, string? windowTitle)
    {
        if (string.IsNullOrEmpty(windowTitle))
            return null;

        try
        {
            var scriptSource = """
                tell application "System Events"
                    tell process pid of first application process whose frontmost is true
                        try
                            get title of first window
                        on error
                            return ""
                        end try
                    end tell
                end tell
                """;

            var script = NSAppleScript_AllocInit();
            if (script == IntPtr.Zero)
                return null;

            try
            {
                var source = Marshal.StringToHGlobalUni(scriptSource);
                NSAppleScript_InitWithSource(script, source);
                Marshal.FreeHGlobal(source);

                var errorDict = IntPtr.Zero;
                var result = NSAppleScript_ExecuteAndReturnError(script, ref errorDict);

                if (errorDict != IntPtr.Zero)
                    CFRelease(errorDict);

                if (result != IntPtr.Zero)
                {
                    var urlString = CFStringToString(result);
                    CFRelease(result);
                    return urlString;
                }
            }
            finally
            {
                CFRelease(script);
            }
        }
        catch
        {
            return null;
        }

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

    [DllImport("/System/Library/Frameworks/AppKit.framework/AppKit")]
    private static extern int NSWorkspace_FrontmostApplicationPid();

    [DllImport("/System/Library/Frameworks/AppKit.framework/AppKit")]
    private static extern int NSWorkspace_GetApplicationPathForPid(int pid, byte* buffer, int bufferSize);

    [DllImport("/usr/lib/libproc.dylib")]
    private static extern int proc_name(int pid, byte* buffer, int bufferSize);

    [DllImport("/System/Library/Frameworks/ApplicationServices.framework/Frameworks/Accessibility.framework/Accessibility")]
    private static extern IntPtr AXUIElementCreateApplication(int pid);

    [DllImport("/System/Library/Frameworks/ApplicationServices.framework/Frameworks/Accessibility.framework/Accessibility")]
    private static extern int AXUIElementCopyAttributeValue(IntPtr element, IntPtr attribute, out IntPtr value);

    [DllImport("/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation")]
    private static extern void CFRelease(IntPtr cf);

    [DllImport("/System/Library/Frameworks/Foundation.framework/Foundation")]
    private static extern IntPtr CFStringCreateWithCString(IntPtr alloc, byte* cStr, int encoding);

    [DllImport("/System/Library/Frameworks/Foundation.framework/Foundation")]
    private static extern int CFStringGetLength(IntPtr theString);

    [DllImport("/System/Library/Frameworks/Foundation.framework/Foundation")]
    private static extern IntPtr CFStringGetCharactersPtr(IntPtr theString);

    [DllImport("/System/Library/Frameworks/Foundation.framework/Foundation")]
    private static extern void CFStringGetCharacters(IntPtr theString, nint range, char* buffer);

    [DllImport("/System/Library/Frameworks/Foundation.framework/Foundation")]
    private static extern IntPtr NSAppleScript_AllocInit();

    [DllImport("/System/Library/Frameworks/Foundation.framework/Foundation")]
    private static extern void NSAppleScript_InitWithSource(IntPtr script, IntPtr source);

    [DllImport("/System/Library/Frameworks/Foundation.framework/Foundation")]
    private static extern IntPtr NSAppleScript_ExecuteAndReturnError(IntPtr script, ref IntPtr errorInfo);

    private static readonly IntPtr kAXFocusedWindowAttribute = CFStringCreate("AXFocusedWindow");
    private static readonly IntPtr kAXTitleAttribute = CFStringCreate("AXTitle");

    private static IntPtr CFStringCreate(string str)
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes(str);
        unsafe
        {
            fixed (byte* ptr = bytes)
            {
                return CFStringCreateWithCString(IntPtr.Zero, ptr, 0x08000100); // kCFStringEncodingUTF8
            }
        }
    }

    private static string? CFStringToString(IntPtr cfString)
    {
        if (cfString == IntPtr.Zero)
            return null;

        try
        {
            var length = CFStringGetLength(cfString);
            if (length == 0)
                return string.Empty;

            var buffer = new char[length];
            unsafe
            {
                fixed (char* ptr = buffer)
                {
                    CFStringGetCharacters(cfString, length, ptr);
                }
            }

            return new string(buffer);
        }
        catch
        {
            return null;
        }
    }

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
