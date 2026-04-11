using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace TimeTrack.Agent.Infrastructure.MacOS.Interop;

/// <summary>
/// Shared CoreFoundation interop helpers used across macOS infrastructure classes.
/// </summary>
[SupportedOSPlatform("macos")]
internal static class CoreFoundationNative
{
    private const string CoreFoundationLib =
        "/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation";

    [DllImport(CoreFoundationLib)]
    private static extern unsafe IntPtr CFStringCreateWithCString(IntPtr alloc, byte* cStr, int encoding);

    /// <summary>
    /// Creates a CFStringRef from a managed string using UTF-8 encoding.
    /// The caller is responsible for releasing the returned handle with CFRelease.
    /// </summary>
    internal static unsafe IntPtr CFStringCreate(string str)
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes(str);
        fixed (byte* ptr = bytes)
        {
            return CFStringCreateWithCString(IntPtr.Zero, ptr, 0x08000100); // kCFStringEncodingUTF8
        }
    }
}
