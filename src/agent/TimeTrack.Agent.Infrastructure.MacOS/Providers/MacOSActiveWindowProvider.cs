using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Diagnostics;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TimeTrack.Agent.Contracts.Providers;
using TimeTrack.Agent.Infrastructure.MacOS.Interop;
using TimeTrack.Agent.Infrastructure.Providers.Windows;

namespace TimeTrack.Agent.Infrastructure.MacOS.Providers;

/// <summary>
/// Implementation of IActiveWindowProvider for macOS using NSWorkspace and AXUIElement API
/// </summary>
[SupportedOSPlatform("macos")]
public sealed class MacOSActiveWindowProvider : IActiveWindowProvider, IDisposable
{
    private readonly ILogger<MacOSActiveWindowProvider> _logger;
    private readonly ActiveWindowProviderOptions _options;
    private readonly int _currentProcessId;

    private ActiveWindowInfo? _cachedWindow;
    private DateTime _lastCacheUpdate = DateTime.MinValue;
    private readonly object _cacheLock = new();

    // Memoized lookups — these are stable for the lifetime of a given PID/path, so
    // we avoid re-reading Info.plist / re-hashing / re-bridging Obj-C on every poll.
    private readonly ConcurrentDictionary<string, string> _hashByInput = new();
    private readonly ConcurrentDictionary<string, string> _displayNameByExePath = new();
    private readonly ConcurrentDictionary<int, string?> _exePathByPid = new();

    private string? _finderFolderCache;
    private DateTime _finderFolderCacheAtUtc = DateTime.MinValue;
    private DateTime _finderPermissionDeniedLoggedAtUtc = DateTime.MinValue;

    // Compiled once — the previous code recompiled this regex on every poll cycle.
    private static readonly Regex s_bundleNameRegex = new(
        @"<key>CFBundleName</key>\s*<string>([^<]+)</string>",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private bool _disposed;

    /// <summary>
    /// Fired when the active window changes (for subscribers)
    /// </summary>
    public event EventHandler<ActiveWindowChangedEventArgs>? ActiveWindowChanged;

    public MacOSActiveWindowProvider(
        ILogger<MacOSActiveWindowProvider> logger,
        IOptions<ActiveWindowProviderOptions>? options = null)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _options = options?.Value ?? new ActiveWindowProviderOptions();
        _currentProcessId = Environment.ProcessId;

        // DO NOT auto-trigger the macOS Accessibility system prompt on every launch.
        // It can show repeatedly in dev/bundled scenarios and is a poor UX.
        // We only *check* here; the UI should guide the user to System Settings when needed.
        // Without permission, window titles can be empty; app name and bundle path are still captured.
        AccessibilityPermission.CheckAndPrompt(prompt: false, _logger);
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
                _logger.LogDebug(
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
            var exePathHash = GetOrComputeHash(exePath);
            var windowHash = !string.IsNullOrEmpty(windowTitle)
                ? GetOrComputeHash(windowTitle)
                : null;

            var processName = Path.GetFileNameWithoutExtension(exePath) ?? "";
            var isFinder = IsFinderBundle(exePath, appName, processName);

            // Top Folders: only Finder should produce a FilePath. Do not capture file paths for other apps.
            string? filePath = null;

            if (isFinder)
            {
                // Prefer AXDocument when it provides a real file:// or absolute POSIX path.
                filePath = NormalizeFinderFolderPath(GetActiveWindowDocumentPath(pid));

                // AXDocument may be empty on some macOS versions / permission states.
                // Use AppleScript fallback to get the target folder of the front window.
                if (string.IsNullOrWhiteSpace(filePath))
                {
                    var finderFolder = TryGetFinderFrontWindowFolderPath();
                    if (!string.IsNullOrWhiteSpace(finderFolder))
                    {
                        filePath = finderFolder;
                    }
                }
            }

            var displayName = GetDisplayName(appName, exePath);

            string? browserUrl = null;
            if (IsBrowserBundle(appName, exePath))
            {
                browserUrl = BrowserUrlExtractor.ExtractSiteFromTitle(windowTitle, displayName);
            }

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

    private static string? NormalizeFinderFolderPath(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return null;

        var value = raw.Trim();

        if (value.StartsWith("file://", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                value = new Uri(value).LocalPath;
            }
            catch
            {
                // fall through to raw
            }
        }

        if (!LooksLikePosixAbsolutePath(value))
            return null;

        // Finder should report a folder; if we got a file path, normalize to its directory.
        try
        {
            if (File.Exists(value) && !Directory.Exists(value))
            {
                value = Path.GetDirectoryName(value) ?? value;
            }
        }
        catch
        {
            // best-effort
        }

        if (!value.EndsWith("/", StringComparison.Ordinal))
            value += "/";

        return value;
    }

    private static bool LooksLikePosixAbsolutePath(string? value)
        => !string.IsNullOrWhiteSpace(value) && value.TrimStart().StartsWith("/", StringComparison.Ordinal);

    private static bool IsFinderBundle(string exePath, string appName, string processName)
    {
        if (!string.IsNullOrWhiteSpace(processName) &&
            processName.Contains("finder", StringComparison.OrdinalIgnoreCase))
            return true;

        if (!string.IsNullOrWhiteSpace(appName) &&
            string.Equals(appName.Trim(), "Finder", StringComparison.OrdinalIgnoreCase))
            return true;

        return exePath.Contains("Finder.app", StringComparison.OrdinalIgnoreCase);
    }

    private string? TryGetFinderFrontWindowFolderPath()
    {
        var now = DateTime.UtcNow;
        if ((now - _finderFolderCacheAtUtc) <= TimeSpan.FromSeconds(2))
            return _finderFolderCache;

        var previous = _finderFolderCache;
        var (path, permissionDenied) = TryGetFinderFrontWindowFolderPathViaAppleScript(timeoutMs: 500);
        _finderFolderCache = path;
        _finderFolderCacheAtUtc = now;

        if (!string.IsNullOrWhiteSpace(path) && !string.Equals(previous, path, StringComparison.Ordinal))
        {
            _logger.LogDebug("Resolved Finder folder path via AppleScript: {FolderPath}", path);
        }

        if (permissionDenied && (now - _finderPermissionDeniedLoggedAtUtc) > TimeSpan.FromMinutes(10))
        {
            _finderPermissionDeniedLoggedAtUtc = now;
            _logger.LogWarning(
                "Finder folder tracking requires Automation permission. macOS denied Apple Events to Finder; Top Folders may be empty until allowed in System Settings.");
        }

        return _finderFolderCache;
    }

    private static (string? Path, bool PermissionDenied) TryGetFinderFrontWindowFolderPathViaAppleScript(int timeoutMs)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "/usr/bin/osascript",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            // Returns empty output when no Finder windows are open.
            psi.ArgumentList.Add("-e");
            psi.ArgumentList.Add("tell application \"Finder\"");
            psi.ArgumentList.Add("-e");
            psi.ArgumentList.Add("if (count of windows) is 0 then return \"\"");
            psi.ArgumentList.Add("-e");
            psi.ArgumentList.Add("return POSIX path of (target of front window as alias)");
            psi.ArgumentList.Add("-e");
            psi.ArgumentList.Add("end tell");

            using var proc = Process.Start(psi);
            if (proc == null)
                return (null, false);

            if (!proc.WaitForExit(timeoutMs))
            {
                try { proc.Kill(entireProcessTree: true); } catch { }
                return (null, false);
            }

            var stdout = proc.StandardOutput.ReadToEnd().Trim();
            var stderr = proc.StandardError.ReadToEnd().Trim();

            if (!string.IsNullOrWhiteSpace(stderr) &&
                (stderr.Contains("Not authorized", StringComparison.OrdinalIgnoreCase) ||
                 stderr.Contains("not authorised", StringComparison.OrdinalIgnoreCase) ||
                 stderr.Contains("AppleEvent", StringComparison.OrdinalIgnoreCase)))
            {
                return (null, true);
            }

            if (string.IsNullOrWhiteSpace(stdout))
                return (null, false);

            // Ensure directory marker
            if (!stdout.EndsWith("/", StringComparison.Ordinal))
                stdout += "/";

            // Only accept absolute POSIX paths
            return LooksLikePosixAbsolutePath(stdout) ? (stdout, false) : (null, false);
        }
        catch
        {
            return (null, false);
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

    // CGWindowList API — doesn't depend on AppKit run loop, always returns fresh data
    [DllImport("/System/Library/Frameworks/CoreGraphics.framework/CoreGraphics")]
    private static extern IntPtr CGWindowListCopyWindowInfo(int option, uint relativeToWindow);

    private const int kCGWindowListOptionOnScreenOnly = (1 << 0);
    private const int kCGWindowListExcludeDesktopElements = (1 << 4);

    // CoreFoundation helpers for reading CFDictionary/CFArray/CFNumber/CFString
    [DllImport("/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation")]
    private static extern nint CFArrayGetCount(IntPtr theArray);

    [DllImport("/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation")]
    private static extern IntPtr CFArrayGetValueAtIndex(IntPtr theArray, nint idx);

    [DllImport("/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation")]
    private static extern IntPtr CFDictionaryGetValue(IntPtr theDict, IntPtr key);

    [DllImport("/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation")]
    [return: MarshalAs(UnmanagedType.I1)]
    private static extern bool CFNumberGetValue(IntPtr number, int theType, out int value);

    private const int kCFNumberSInt32Type = 3;

    private (int pid, string? appName) GetFrontmostApplication()
    {
        try
        {
            // Use CGWindowListCopyWindowInfo to find the frontmost on-screen window.
            // Unlike NSWorkspace.frontmostApplication, this queries the window server
            // directly and always returns fresh data — no NSRunLoop required.
            var windowList = CGWindowListCopyWindowInfo(
                kCGWindowListOptionOnScreenOnly | kCGWindowListExcludeDesktopElements, 0);

            if (windowList == IntPtr.Zero)
            {
                _logger.LogDebug("CGWindowListCopyWindowInfo returned null");
                return (0, null);
            }

            try
            {
                var count = CFArrayGetCount(windowList);
                var kOwnerPID = CoreFoundationNative.CFStringCreate("kCGWindowOwnerPID");
                var kOwnerName = CoreFoundationNative.CFStringCreate("kCGWindowOwnerName");
                var kWindowLayer = CoreFoundationNative.CFStringCreate("kCGWindowLayer");

                try
                {
                    // The window list is ordered front-to-back. Find the first window
                    // at layer 0 (normal windows) that isn't our own agent process.
                    for (nint i = 0; i < count; i++)
                    {
                        var dict = CFArrayGetValueAtIndex(windowList, i);
                        if (dict == IntPtr.Zero) continue;

                        // Check window layer — we only want normal windows (layer 0)
                        var layerRef = CFDictionaryGetValue(dict, kWindowLayer);
                        if (layerRef != IntPtr.Zero)
                        {
                            CFNumberGetValue(layerRef, kCFNumberSInt32Type, out int layer);
                            if (layer != 0) continue;
                        }

                        // Get owner PID
                        var pidRef = CFDictionaryGetValue(dict, kOwnerPID);
                        if (pidRef == IntPtr.Zero) continue;
                        CFNumberGetValue(pidRef, kCFNumberSInt32Type, out int pid);
                        if (pid == 0 || pid == _currentProcessId) continue;

                        // Get owner name
                        var nameRef = CFDictionaryGetValue(dict, kOwnerName);
                        var appName = ObjCRuntime.NSStringToManaged(nameRef);

                        if (!string.IsNullOrEmpty(appName))
                            return (pid, appName);
                    }
                }
                finally
                {
                    CFRelease(kOwnerPID);
                    CFRelease(kOwnerName);
                    CFRelease(kWindowLayer);
                }
            }
            finally
            {
                CFRelease(windowList);
            }

            _logger.LogDebug("No frontmost application found via CGWindowList");
            return (0, null);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Error getting frontmost application");
            return (0, null);
        }
    }

    private string? GetApplicationPath(int pid)
    {
        // The bundle path is stable for the lifetime of a given PID; memoizing avoids
        // three Obj-C bridge calls (NSRunningApplication → bundleURL → path) per poll.
        if (_exePathByPid.TryGetValue(pid, out var cached))
            return cached;

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
            var path = ObjCRuntime.NSStringToManaged(nsPath);
            if (!string.IsNullOrEmpty(path))
            {
                // Bounded cache — PID values are reused as processes recycle; clear
                // periodically to avoid unbounded growth on long-running agents.
                if (_exePathByPid.Count > 1024) _exePathByPid.Clear();
                _exePathByPid[pid] = path;
            }
            return path;
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

    private string? GetActiveWindowDocumentPath(int pid)
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
                    var docValue = IntPtr.Zero;
                    result = AXUIElementCopyAttributeValue(
                        focusedWindowRef,
                        kAXDocumentAttribute,
                        out docValue);

                    if (result != 0 || docValue == IntPtr.Zero)
                        return null;

                    try
                    {
                        var raw = ObjCRuntime.NSStringToManaged(docValue);
                        if (string.IsNullOrWhiteSpace(raw))
                            return null;

                        if (raw.StartsWith("file://", StringComparison.OrdinalIgnoreCase))
                        {
                            try
                            {
                                var uri = new Uri(raw);
                                return uri.LocalPath;
                            }
                            catch
                            {
                                // fall through to raw
                            }
                        }

                        return raw;
                    }
                    finally
                    {
                        CFRelease(docValue);
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
            _logger.LogDebug(ex, "Error getting active window document path for PID {Pid}", pid);
            return null;
        }
    }

    private string GetDisplayName(string appName, string exePath)
    {
        // The display name for a given bundle path never changes at runtime, so
        // memoize it — the previous implementation read Info.plist from disk and
        // matched a freshly compiled regex on every poll.
        if (_displayNameByExePath.TryGetValue(exePath, out var cached))
            return cached;

        var resolved = ResolveDisplayName(appName, exePath);
        _displayNameByExePath[exePath] = resolved;
        return resolved;
    }

    private static string ResolveDisplayName(string appName, string exePath)
    {
        try
        {
            // Prefer the localized name from NSRunningApplication / CGWindowOwnerName —
            // it's already correct in almost all cases and avoids disk I/O entirely.
            if (!string.IsNullOrWhiteSpace(appName))
                return appName;

            var bundlePath = exePath.EndsWith(".app") ? exePath : Path.GetDirectoryName(exePath);
            if (bundlePath?.EndsWith(".app") == true)
            {
                var infoPlistPath = Path.Combine(bundlePath, "Contents", "Info.plist");
                if (File.Exists(infoPlistPath))
                {
                    var content = File.ReadAllText(infoPlistPath);
                    var match = s_bundleNameRegex.Match(content);
                    if (match.Success)
                        return match.Groups[1].Value;
                }
            }

            return Path.GetFileNameWithoutExtension(appName) ?? appName;
        }
        catch
        {
            return Path.GetFileNameWithoutExtension(appName) ?? appName;
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

    private string GetOrComputeHash(string input)
    {
        // Hashes are deterministic — exe paths and window titles repeat for thousands
        // of cycles. Memoize to avoid SHA-256 + UTF-8 encoding every poll.
        if (_hashByInput.TryGetValue(input, out var cached))
            return cached;

        var hash = ComputeSha256Hash(input);

        if (_hashByInput.Count > 4096) _hashByInput.Clear();
        _hashByInput[input] = hash;
        return hash;
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
    private static readonly IntPtr kAXDocumentAttribute = CoreFoundationNative.CFStringCreate("AXDocument");

    #endregion
}

/// <summary>
/// Configuration options for ActiveWindowProvider
/// </summary>
public sealed class ActiveWindowProviderOptions
{
    public int CacheValidityMs { get; set; } = 1500;
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
