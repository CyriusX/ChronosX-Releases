using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text;

namespace TimeTrack.Agent.Infrastructure.MacOS.Interop;

/// <summary>
/// Minimal Objective-C runtime bridge for calling AppKit / Foundation classes from .NET.
///
/// AppKit and Foundation export Objective-C classes, not C functions, so plain DllImport
/// entry points like "NSWorkspace_FrontmostApplicationPid" do not exist. All interaction
/// must go through libobjc.dylib's objc_msgSend.
///
/// Usage:
///   var workspace = ObjCRuntime.SendMessage(ObjCRuntime.GetClass("NSWorkspace"),
///                                           ObjCRuntime.GetSelector("sharedWorkspace"));
///   var frontApp = ObjCRuntime.SendMessage(workspace, ObjCRuntime.GetSelector("frontmostApplication"));
///   var pid = ObjCRuntime.SendMessageInt(frontApp, ObjCRuntime.GetSelector("processIdentifier"));
/// </summary>
[SupportedOSPlatform("macos")]
internal static class ObjCRuntime
{
    private const string LibObjC = "/usr/lib/libobjc.dylib";
    private const string LibDl = "/usr/lib/libSystem.dylib";

    [DllImport(LibDl, EntryPoint = "dlopen")]
    private static extern IntPtr dlopen([MarshalAs(UnmanagedType.LPStr)] string path, int mode);

    private const int RTLD_LAZY = 0x1;
    private const int RTLD_GLOBAL = 0x8;

    static ObjCRuntime()
    {
        // AppKit / Foundation are not linked into a plain .NET process. Without an
        // explicit dlopen, objc_getClass("NSWorkspace") returns nil because the class
        // is not yet registered with the runtime. Load the frameworks up-front so the
        // static class handles populated by MacOSActiveWindowProvider are valid.
        dlopen("/System/Library/Frameworks/Foundation.framework/Foundation", RTLD_LAZY | RTLD_GLOBAL);
        dlopen("/System/Library/Frameworks/AppKit.framework/AppKit", RTLD_LAZY | RTLD_GLOBAL);
    }

    [DllImport(LibObjC, EntryPoint = "objc_getClass")]
    private static extern IntPtr objc_getClass([MarshalAs(UnmanagedType.LPStr)] string name);

    [DllImport(LibObjC, EntryPoint = "sel_registerName")]
    private static extern IntPtr sel_registerName([MarshalAs(UnmanagedType.LPStr)] string name);

    [DllImport(LibObjC, EntryPoint = "objc_msgSend")]
    private static extern IntPtr objc_msgSend_IntPtr(IntPtr receiver, IntPtr selector);

    [DllImport(LibObjC, EntryPoint = "objc_msgSend")]
    private static extern IntPtr objc_msgSend_IntPtr_IntPtr(IntPtr receiver, IntPtr selector, IntPtr arg);

    [DllImport(LibObjC, EntryPoint = "objc_msgSend")]
    private static extern IntPtr objc_msgSend_IntPtr_Int(IntPtr receiver, IntPtr selector, int arg);

    [DllImport(LibObjC, EntryPoint = "objc_msgSend")]
    private static extern IntPtr objc_msgSend_IntPtr_IntPtr_IntPtr(
        IntPtr receiver, IntPtr selector, IntPtr arg1, IntPtr arg2);

    [DllImport(LibObjC, EntryPoint = "objc_msgSend")]
    private static extern int objc_msgSend_Int(IntPtr receiver, IntPtr selector);

    public static IntPtr GetClass(string name) => objc_getClass(name);

    public static IntPtr GetSelector(string name) => sel_registerName(name);

    public static IntPtr SendMessage(IntPtr receiver, IntPtr selector)
        => objc_msgSend_IntPtr(receiver, selector);

    public static IntPtr SendMessage(IntPtr receiver, IntPtr selector, IntPtr arg)
        => objc_msgSend_IntPtr_IntPtr(receiver, selector, arg);

    public static IntPtr SendMessage(IntPtr receiver, IntPtr selector, int arg)
        => objc_msgSend_IntPtr_Int(receiver, selector, arg);

    public static IntPtr SendMessage(IntPtr receiver, IntPtr selector, IntPtr arg1, IntPtr arg2)
        => objc_msgSend_IntPtr_IntPtr_IntPtr(receiver, selector, arg1, arg2);

    public static int SendMessageInt(IntPtr receiver, IntPtr selector)
        => objc_msgSend_Int(receiver, selector);

    // ───── CoreFoundation helpers for NSString ↔ managed string ─────
    // NSString is toll-free bridged with CFString, so CF* functions work on it directly.

    private const string CoreFoundationLib =
        "/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation";

    [DllImport(CoreFoundationLib)]
    private static extern nint CFStringGetLength(IntPtr theString);

    [DllImport(CoreFoundationLib)]
    private static extern IntPtr CFStringGetCStringPtr(IntPtr theString, uint encoding);

    [DllImport(CoreFoundationLib)]
    [return: MarshalAs(UnmanagedType.I1)]
    private static extern bool CFStringGetCString(IntPtr theString, byte[] buffer, nint bufferSize, uint encoding);

    private const uint kCFStringEncodingUTF8 = 0x08000100;

    /// <summary>
    /// Converts an NSString / CFString handle to a managed string.
    /// Returns null if the handle is zero.
    /// </summary>
    public static string? NSStringToManaged(IntPtr nsString)
    {
        if (nsString == IntPtr.Zero)
            return null;

        // Fast path: CFStringGetCStringPtr returns a direct UTF-8 pointer when available.
        var fast = CFStringGetCStringPtr(nsString, kCFStringEncodingUTF8);
        if (fast != IntPtr.Zero)
            return Marshal.PtrToStringUTF8(fast);

        // Fallback: copy into a managed buffer sized generously from the UTF-16 length.
        var len = (int)CFStringGetLength(nsString);
        if (len == 0)
            return string.Empty;

        var bufSize = (len * 4) + 1;
        var buffer = new byte[bufSize];
        if (!CFStringGetCString(nsString, buffer, bufSize, kCFStringEncodingUTF8))
            return null;

        var nullTerm = Array.IndexOf(buffer, (byte)0);
        if (nullTerm < 0) nullTerm = bufSize;
        return Encoding.UTF8.GetString(buffer, 0, nullTerm);
    }
}
