using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;

namespace TimeTrack.Agent.Infrastructure.Providers.Windows;

/// <summary>
/// Wrapper for Windows event hook (SetWinEventHook)
/// Provides event-driven window change detection
/// </summary>
internal sealed class WinEventHook : IDisposable
{
    private readonly ILogger<WinEventHook> _logger;
    private readonly NativeMethods.WinEventProc _callbackDelegate;
    private IntPtr _hookHandle = IntPtr.Zero;
    private bool _disposed;
    private bool _isHookActive;

    /// <summary>
    /// Fired when the foreground window changes
    /// </summary>
    public event EventHandler<WindowChangedEventArgs>? WindowChanged;

    /// <summary>
    /// Indicates if the hook is currently active
    /// </summary>
    public bool IsActive => _isHookActive && _hookHandle != IntPtr.Zero;

    public WinEventHook(ILogger<WinEventHook> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        // Keep reference to delegate to prevent GC
        _callbackDelegate = OnWinEvent;
    }

    /// <summary>
    /// Starts listening for foreground window changes
    /// </summary>
    /// <returns>True if hook was successfully installed</returns>
    public bool Start()
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(WinEventHook));

        if (_isHookActive)
        {
            _logger.LogWarning("WinEvent hook is already active");
            return true;
        }

        try
        {
            // Hook for foreground window changes only
            _hookHandle = NativeMethods.SetWinEventHook(
                NativeMethods.EVENT_SYSTEM_FOREGROUND,
                NativeMethods.EVENT_SYSTEM_FOREGROUND,
                IntPtr.Zero,
                _callbackDelegate,
                0, // All processes
                0, // All threads
                NativeMethods.WINEVENT_OUTOFCONTEXT | NativeMethods.WINEVENT_SKIPOWNPROCESS);

            if (_hookHandle == IntPtr.Zero)
            {
                var error = Marshal.GetLastWin32Error();
                _logger.LogWarning(
                    "Failed to set WinEvent hook. Error: {Error}. Will fallback to polling.",
                    error);
                return false;
            }

            _isHookActive = true;
            _logger.LogInformation("WinEvent hook installed successfully (Handle: {Handle})", _hookHandle);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception while installing WinEvent hook");
            return false;
        }
    }

    /// <summary>
    /// Stops listening for window changes
    /// </summary>
    public void Stop()
    {
        if (!_isHookActive || _hookHandle == IntPtr.Zero)
            return;

        try
        {
            if (NativeMethods.UnhookWinEvent(_hookHandle))
            {
                _logger.LogInformation("WinEvent hook removed successfully");
            }
            else
            {
                var error = Marshal.GetLastWin32Error();
                _logger.LogWarning("Failed to remove WinEvent hook. Error: {Error}", error);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception while removing WinEvent hook");
        }
        finally
        {
            _hookHandle = IntPtr.Zero;
            _isHookActive = false;
        }
    }

    /// <summary>
    /// Callback invoked by Windows when a foreground window change occurs
    /// </summary>
    private void OnWinEvent(
        IntPtr hWinEventHook,
        uint eventType,
        IntPtr hwnd,
        int idObject,
        int idChild,
        uint dwEventThread,
        uint dwmsEventTime)
    {
        // Only process foreground window events
        if (eventType != NativeMethods.EVENT_SYSTEM_FOREGROUND)
            return;

        // Ignore non-window objects (idObject != 0 means it's not the window itself)
        if (idObject != 0)
            return;

        // Ignore child windows
        if (idChild != 0)
            return;

        // Validate window handle
        if (hwnd == IntPtr.Zero || !NativeMethods.IsWindow(hwnd))
            return;

        var processId = NativeMethods.GetProcessIdFromWindow(hwnd);
        var windowTitle = NativeMethods.GetWindowTitleText(hwnd);

        var args = new WindowChangedEventArgs(hwnd, processId, windowTitle, dwmsEventTime);

        try
        {
            WindowChanged?.Invoke(this, args);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in WindowChanged event handler");
        }
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        Stop();
        _disposed = true;
        GC.SuppressFinalize(this);
    }

    ~WinEventHook()
    {
        Dispose();
    }
}

/// <summary>
/// Event arguments for window change events
/// </summary>
internal sealed class WindowChangedEventArgs : EventArgs
{
    public IntPtr WindowHandle { get; }
    public uint ProcessId { get; }
    public string? WindowTitle { get; }
    public uint EventTimeMs { get; }
    public DateTime TimestampUtc { get; }

    public WindowChangedEventArgs(IntPtr windowHandle, uint processId, string? windowTitle, uint eventTimeMs)
    {
        WindowHandle = windowHandle;
        ProcessId = processId;
        WindowTitle = windowTitle;
        EventTimeMs = eventTimeMs;
        TimestampUtc = DateTime.UtcNow;
    }
}
