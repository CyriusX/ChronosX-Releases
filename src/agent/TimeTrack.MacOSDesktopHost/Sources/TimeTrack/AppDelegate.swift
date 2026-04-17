import AppKit

class AppDelegate: NSObject, NSApplicationDelegate {
    func applicationDidFinishLaunching(_ notification: Notification) {
        // When running via `swift run` (not an app bundle), macOS may treat this as a background
        // process: no Dock icon and the window may not receive keyboard focus (keystrokes go to the
        // previously active app, e.g. VS Code). Force a regular activation policy.
        NSApp.setActivationPolicy(.regular)
        NSRunningApplication.current.activate(options: [.activateAllWindows, .activateIgnoringOtherApps])

        // Ensure the background agent service is running. This covers the case
        // where the user launched the app directly (e.g. double-click, Spotlight)
        // without the LaunchAgent having started the service first.
        AgentLauncher.shared.ensureRunning()

        // Bring the main window front and give it focus once SwiftUI has created it.
        for delay in [0.1, 0.4, 1.0] {
            DispatchQueue.main.asyncAfter(deadline: .now() + delay) {
                NSRunningApplication.current.activate(options: [.activateAllWindows, .activateIgnoringOtherApps])
                NSApp.windows.first?.makeKeyAndOrderFront(nil)
            }
        }
    }

    // MARK: - Agent lifecycle

    // (Agent lifecycle moved to AgentLauncher.swift)

    func applicationShouldHandleReopen(_ sender: NSApplication, hasVisibleWindows flag: Bool) -> Bool {
        if !flag {
            for window in sender.windows {
                window.makeKeyAndOrderFront(self)
            }
        }
        sender.activate(ignoringOtherApps: true)
        return true
    }

    func applicationSupportsSecureRestorableState(_ app: NSApplication) -> Bool {
        return true
    }

    private func checkAccessibilityPermission() {
        let options = [kAXTrustedCheckOptionPrompt.takeUnretainedValue() as String: false] as CFDictionary
        let trusted = AXIsProcessTrustedWithOptions(options)

        if !trusted {
            let alert = NSAlert()
            alert.messageText = "Accessibility Permission Required"
            alert.informativeText = """
            TimeTrack needs Accessibility access to track active windows and applications.

            Please grant access in:
            System Settings → Privacy & Security → Accessibility

            The app will run in limited mode until permission is granted.
            """
            alert.addButton(withTitle: "Open System Settings")
            alert.addButton(withTitle: "Later")
            alert.alertStyle = .warning

            let response = alert.runModal()
            if response == .alertFirstButtonReturn {
                let url = URL(string: "x-apple.systempreferences:com.apple.preference.security?Privacy_Accessibility")!
                NSWorkspace.shared.open(url)
            }
        }
    }
}
