using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Microsoft.Extensions.Logging;

namespace TimeTrack.Agent.Infrastructure.MacOS.Interop;

/// <summary>
/// Helper to check (and optionally prompt for) the macOS Accessibility permission.
///
/// Reading window titles of other applications via the AX API requires the process to be
/// listed in System Settings → Privacy & Security → Accessibility. Without this, the
/// agent can still capture the frontmost application's name and bundle path, but
/// AXUIElementCopyAttributeValue returns nil for the window title.
/// </summary>
[SupportedOSPlatform("macos")]
internal static class AccessibilityPermission
{
    private const string HIServicesLib =
        "/System/Library/Frameworks/ApplicationServices.framework/Frameworks/HIServices.framework/HIServices";

    [DllImport(HIServicesLib)]
    [return: MarshalAs(UnmanagedType.I1)]
    private static extern bool AXIsProcessTrusted();

    [DllImport(HIServicesLib)]
    [return: MarshalAs(UnmanagedType.I1)]
    private static extern bool AXIsProcessTrustedWithOptions(IntPtr options);

    /// <summary>
    /// Returns true if this process is currently trusted for Accessibility APIs.
    /// Does not prompt the user.
    /// </summary>
    public static bool IsTrusted()
    {
        try
        {
            return AXIsProcessTrusted();
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Checks whether the process is Accessibility-trusted. If not and <paramref name="prompt"/>
    /// is true, shows the macOS system dialog asking the user to grant permission.
    ///
    /// The prompt is shown at most once per process (flag is non-sticky). If the user
    /// dismisses it, they must grant access manually in System Settings.
    /// </summary>
    /// <returns>True if trusted at time of call, false otherwise.</returns>
    public static bool CheckAndPrompt(bool prompt, ILogger logger)
    {
        try
        {
            if (!prompt)
            {
                var already = AXIsProcessTrusted();
                LogResult(already, logger);
                return already;
            }

            // Build @{ "AXTrustedCheckOptionPrompt": @YES } via the Objective-C runtime.
            // NSDictionary is toll-free bridged with CFDictionary, so the handle is
            // accepted by AXIsProcessTrustedWithOptions as-is.
            var nsNumberClass = ObjCRuntime.GetClass("NSNumber");
            var numberWithBoolSel = ObjCRuntime.GetSelector("numberWithBool:");
            if (nsNumberClass == IntPtr.Zero || numberWithBoolSel == IntPtr.Zero)
            {
                logger.LogWarning("Could not resolve NSNumber class/selector for AX prompt");
                return AXIsProcessTrusted();
            }

            var yesNumber = ObjCRuntime.SendMessage(nsNumberClass, numberWithBoolSel, 1);

            // Use CoreFoundation to create the key string — NSString and CFString are
            // toll-free bridged, so the resulting handle works in an NSDictionary.
            var keyString = CoreFoundationNative.CFStringCreate("AXTrustedCheckOptionPrompt");

            var nsDictClass = ObjCRuntime.GetClass("NSDictionary");
            var dictWithObjForKeySel = ObjCRuntime.GetSelector("dictionaryWithObject:forKey:");
            if (nsDictClass == IntPtr.Zero || dictWithObjForKeySel == IntPtr.Zero)
            {
                logger.LogWarning("Could not resolve NSDictionary class/selector for AX prompt");
                return AXIsProcessTrusted();
            }

            var options = ObjCRuntime.SendMessage(
                nsDictClass, dictWithObjForKeySel, yesNumber, keyString);

            var trusted = AXIsProcessTrustedWithOptions(options);
            LogResult(trusted, logger);
            return trusted;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error checking/prompting for macOS Accessibility permission");
            return false;
        }
    }

    private static void LogResult(bool trusted, ILogger logger)
    {
        if (trusted)
        {
            logger.LogInformation("macOS Accessibility permission is granted — window titles will be captured.");
        }
        else
        {
            logger.LogWarning(
                "macOS Accessibility permission not granted. Window titles will be empty " +
                "until the user grants access in System Settings → Privacy & Security → " +
                "Accessibility. (Frontmost app name and bundle path are still captured.)");
        }
    }
}
