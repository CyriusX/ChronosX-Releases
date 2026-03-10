using System.Runtime.InteropServices;
using System.Text;

namespace TimeTrack.Agent.Infrastructure.Providers.Windows;

/// <summary>
/// P/Invoke declarations for user32.dll, kernel32.dll and psapi.dll
/// </summary>
internal static class NativeMethods
{
    #region Constants

    /// <summary>
    /// Event constant for foreground window change
    /// </summary>
    public const uint EVENT_SYSTEM_FOREGROUND = 0x0003;

    /// <summary>
    /// Out-of-context hook - callback in caller's context
    /// </summary>
    public const uint WINEVENT_OUTOFCONTEXT = 0x0000;

    /// <summary>
    /// Skip own process
    /// </summary>
    public const uint WINEVENT_SKIPOWNPROCESS = 0x0002;

    /// <summary>
    /// Max path length for Windows
    /// </summary>
    public const int MAX_PATH = 260;

    #endregion

    #region Process Access Rights

    public const uint PROCESS_QUERY_INFORMATION = 0x0400;
    public const uint PROCESS_VM_READ = 0x0010;

    #endregion

    #region Delegates

    /// <summary>
    /// Delegate for WinEvent callback
    /// </summary>
    public delegate void WinEventProc(
        IntPtr hWinEventHook,
        uint eventType,
        IntPtr hwnd,
        int idObject,
        int idChild,
        uint dwEventThread,
        uint dwmsEventTime);

    #endregion

    #region user32.dll

    [DllImport("user32.dll", SetLastError = true)]
    public static extern IntPtr SetWinEventHook(
        uint eventMin,
        uint eventMax,
        IntPtr hmodWinEventProc,
        WinEventProc pfnWinEventProc,
        uint idProcess,
        uint idThread,
        uint dwFlags);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool UnhookWinEvent(IntPtr hWinEventHook);

    [DllImport("user32.dll")]
    public static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll", SetLastError = true)]
    public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    public static extern int GetWindowTextLength(IntPtr hWnd);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    public static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool IsWindow(IntPtr hWnd);

    #endregion

    #region kernel32.dll

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern IntPtr OpenProcess(uint dwDesiredAccess, bool bInheritHandle, uint dwProcessId);

    [DllImport("kernel32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool CloseHandle(IntPtr hObject);

    #endregion

    #region psapi.dll

    [DllImport("psapi.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    public static extern int GetModuleFileNameEx(
        IntPtr hProcess,
        IntPtr hModule,
        StringBuilder lpFilename,
        int nSize);

    [DllImport("psapi.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    public static extern int GetModuleBaseName(
        IntPtr hProcess,
        IntPtr hModule,
        StringBuilder lpBaseName,
        int nSize);

    #endregion

    #region Helper Methods

    /// <summary>
    /// Gets the window title text
    /// </summary>
    public static string? GetWindowTitleText(IntPtr hWnd)
    {
        if (hWnd == IntPtr.Zero)
            return null;

        var length = GetWindowTextLength(hWnd);
        if (length == 0)
            return null;

        var sb = new StringBuilder(length + 1);
        var result = GetWindowText(hWnd, sb, sb.Capacity);

        return result > 0 ? sb.ToString() : null;
    }

    /// <summary>
    /// Gets the process ID from a window handle
    /// </summary>
    public static uint GetProcessIdFromWindow(IntPtr hWnd)
    {
        if (hWnd == IntPtr.Zero)
            return 0;

        GetWindowThreadProcessId(hWnd, out var processId);
        return processId;
    }

    /// <summary>
    /// Gets the executable path from a process handle
    /// </summary>
    public static string? GetProcessExePath(IntPtr hProcess)
    {
        if (hProcess == IntPtr.Zero)
            return null;

        var sb = new StringBuilder(MAX_PATH * 2);
        var result = GetModuleFileNameEx(hProcess, IntPtr.Zero, sb, sb.Capacity);
        return result > 0 ? sb.ToString() : null;
    }

    #endregion
}
