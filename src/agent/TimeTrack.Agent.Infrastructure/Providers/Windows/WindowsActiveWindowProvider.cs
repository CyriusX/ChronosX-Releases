using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TimeTrack.Agent.Contracts.Providers;

namespace TimeTrack.Agent.Infrastructure.Providers.Windows;

/// <summary>
/// Implementation of IActiveWindowProvider using WinEvent Hook with polling fallback
/// </summary>
public sealed class WindowsActiveWindowProvider : IActiveWindowProvider, IDisposable
{
    private readonly ILogger<WindowsActiveWindowProvider> _logger;
    private readonly ActiveWindowProviderOptions _options;
    private readonly WinEventHook _winEventHook;
    private readonly IFilePathExtractor _filePathExtractor;
    private readonly int _currentProcessId;

    private ActiveWindowInfo? _cachedWindow;
    private DateTime _lastCacheUpdate = DateTime.MinValue;
    private readonly object _cacheLock = new();

    private bool _usePollingFallback;
    private bool _disposed;

    /// <summary>
    /// Fired when the active window changes (for subscribers)
    /// </summary>
    public event EventHandler<ActiveWindowChangedEventArgs>? ActiveWindowChanged;

    public WindowsActiveWindowProvider(
        ILogger<WindowsActiveWindowProvider> logger,
        IFilePathExtractor filePathExtractor,
        IOptions<ActiveWindowProviderOptions>? options = null)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _filePathExtractor = filePathExtractor ?? throw new ArgumentNullException(nameof(filePathExtractor));
        _options = options?.Value ?? new ActiveWindowProviderOptions();
        _currentProcessId = Environment.ProcessId;

        // Create internal hook with wrapper logger
        var hookLogger = new LoggerWrapper<WinEventHook>(logger);
        _winEventHook = new WinEventHook(hookLogger);

        InitializeHook();
    }

    private void InitializeHook()
    {
        _winEventHook.WindowChanged += OnWindowChanged;

        var success = _winEventHook.Start();
        if (!success)
        {
            _logger.LogWarning("WinEvent hook failed to start. Enabling polling fallback.");
            _usePollingFallback = true;
        }
        else
        {
            _logger.LogInformation("WinEvent hook started successfully. Event-driven mode active.");
        }
    }

    /// <inheritdoc />
    public Task<ActiveWindowInfo?> GetActiveWindowAsync(CancellationToken cancellationToken = default)
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(WindowsActiveWindowProvider));

        // If using polling fallback or cache is stale, poll now
        if (_usePollingFallback || IsCacheStale())
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

    private void OnWindowChanged(object? sender, WindowChangedEventArgs e)
    {
        // Skip own process windows
        if (e.ProcessId == _currentProcessId)
        {
            _logger.LogDebug("Skipping own process window change");
            return;
        }

        var info = BuildActiveWindowInfo(e.WindowHandle, e.ProcessId, e.WindowTitle);
        if (info == null)
            return;

        UpdateCache(info);
        RaiseWindowChangedEvent(info);
    }

    private ActiveWindowInfo? PollActiveWindow()
    {
        try
        {
            var hWnd = NativeMethods.GetForegroundWindow();
            if (hWnd == IntPtr.Zero)
                return null;

            var processId = NativeMethods.GetProcessIdFromWindow(hWnd);
            if (processId == 0)
                return null;

            // Skip own process
            if (processId == _currentProcessId)
            {
                _logger.LogDebug("Skipping own process window in polling");
                return null;
            }

            var windowTitle = NativeMethods.GetWindowTitleText(hWnd);
            var info = BuildActiveWindowInfo(hWnd, processId, windowTitle);

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

    private ActiveWindowInfo? BuildActiveWindowInfo(IntPtr hWnd, uint processId, string? windowTitle)
    {
        try
        {
            // Get process path and name
            var (exePath, displayName) = GetProcessInfo(processId);

            if (string.IsNullOrEmpty(exePath))
            {
                _logger.LogDebug("Could not get exe path for process {ProcessId}", processId);
                return null;
            }

            // Generate hashes
            var exePathHash = ComputeSha256Hash(exePath);
            var windowHash = !string.IsNullOrEmpty(windowTitle)
                ? ComputeSha256Hash(windowTitle)
                : null;

            // Extract file path from the active window
            var processName = Path.GetFileNameWithoutExtension(exePath) ?? "";
            var filePath = _filePathExtractor.ExtractFilePath(hWnd, processName, windowTitle);

            // Extract site name from browser window title
            string? browserUrl = null;
            var resolvedDisplayName = displayName ?? Path.GetFileNameWithoutExtension(exePath) ?? "Unknown";
            if (BrowserUrlExtractor.IsBrowserExe(exePath))
            {
                browserUrl = BrowserUrlExtractor.ExtractSiteFromTitle(windowTitle, resolvedDisplayName);
            }

            return new ActiveWindowInfo
            {
                ExePathHash = exePathHash,
                DisplayName = resolvedDisplayName,
                ExePath = exePath,
                WindowTitle = windowTitle,
                WindowHash = windowHash,
                FilePath = filePath,
                BrowserUrl = browserUrl
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error building ActiveWindowInfo for process {ProcessId}", processId);
            return null;
        }
    }

    private (string? ExePath, string? DisplayName) GetProcessInfo(uint processId)
    {
        IntPtr hProcess = IntPtr.Zero;
        try
        {
            // Try to open process with query + vm_read
            hProcess = NativeMethods.OpenProcess(
                NativeMethods.PROCESS_QUERY_INFORMATION | NativeMethods.PROCESS_VM_READ,
                false,
                processId);

            if (hProcess == IntPtr.Zero)
            {
                // Try with just query (elevated processes)
                hProcess = NativeMethods.OpenProcess(
                    NativeMethods.PROCESS_QUERY_INFORMATION,
                    false,
                    processId);

                if (hProcess == IntPtr.Zero)
                {
                    var error = Marshal.GetLastWin32Error();
                    _logger.LogDebug("Could not open process {ProcessId}. Error: {Error}", processId, error);
                    return FallbackToProcessGetById((int)processId);
                }
            }

            // Get full path using helper
            var exePath = NativeMethods.GetProcessExePath(hProcess);

            // Get display name (product name)
            var displayName = GetProductDisplayName(exePath);

            return (exePath, displayName);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Error getting process info for {ProcessId}", processId);
            return FallbackToProcessGetById((int)processId);
        }
        finally
        {
            if (hProcess != IntPtr.Zero)
            {
                NativeMethods.CloseHandle(hProcess);
            }
        }
    }

    private (string? ExePath, string? DisplayName) FallbackToProcessGetById(int processId)
    {
        try
        {
            var process = Process.GetProcessById(processId);
            return (process.MainModule?.FileName, process.MainModule?.FileVersionInfo?.ProductName);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Fallback failed for process {ProcessId}", processId);
            return (null, null);
        }
    }

    private string? GetProductDisplayName(string? exePath)
    {
        if (string.IsNullOrEmpty(exePath) || !File.Exists(exePath))
            return null;

        try
        {
            var versionInfo = FileVersionInfo.GetVersionInfo(exePath);
            return !string.IsNullOrEmpty(versionInfo.ProductName)
                ? versionInfo.ProductName
                : !string.IsNullOrEmpty(versionInfo.FileDescription)
                    ? versionInfo.FileDescription
                    : null;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Could not get product name for {ExePath}", exePath);
            return null;
        }
    }

    private static string ComputeSha256Hash(string input)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
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

        _winEventHook.WindowChanged -= OnWindowChanged;
        _winEventHook.Dispose();
        _disposed = true;

        GC.SuppressFinalize(this);
    }
}

/// <summary>
/// Configuration options for ActiveWindowProvider
/// </summary>
public sealed class ActiveWindowProviderOptions
{
    /// <summary>
    /// Polling interval in milliseconds when fallback is active
    /// </summary>
    public int PollingIntervalMs { get; set; } = 250;

    /// <summary>
    /// Cache validity duration in milliseconds
    /// </summary>
    public int CacheValidityMs { get; set; } = 500;

    /// <summary>
    /// Debounce interval in milliseconds to avoid duplicate events
    /// </summary>
    public int DebounceMs { get; set; } = 200;
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

/// <summary>
/// Logger wrapper to adapt ILogger to ILogger{T} for internal classes
/// </summary>
file sealed class LoggerWrapper<T> : ILogger<T>
{
    private readonly ILogger _logger;

    public LoggerWrapper(ILogger logger)
    {
        _logger = logger;
    }

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull
        => _logger.BeginScope(state);

    public bool IsEnabled(LogLevel logLevel)
        => _logger.IsEnabled(logLevel);

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        => _logger.Log(logLevel, eventId, state, exception, formatter);
}
