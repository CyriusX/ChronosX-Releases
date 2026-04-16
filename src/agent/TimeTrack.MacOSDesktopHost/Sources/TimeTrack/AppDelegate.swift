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
        ensureAgentRunning()

        // Bring the main window front and give it focus once SwiftUI has created it.
        for delay in [0.1, 0.4, 1.0] {
            DispatchQueue.main.asyncAfter(deadline: .now() + delay) {
                NSRunningApplication.current.activate(options: [.activateAllWindows, .activateIgnoringOtherApps])
                NSApp.windows.first?.makeKeyAndOrderFront(nil)
            }
        }
    }

    // MARK: - Agent lifecycle

    /// Launches the .NET background agent if it is not already running.
    private func ensureAgentRunning() {
        let bundlePath = Bundle.main.bundlePath
        let candidates = [
            // Preferred: resources/agent (common macOS layout)
            bundlePath.appending("/Contents/Resources/agent/TimeTrack.MacOSAgentService"),
            // Our SPM build script currently publishes to Contents/MacOS/agent
            bundlePath.appending("/Contents/MacOS/agent/TimeTrack.MacOSAgentService"),
        ]

        guard let agentPath = candidates.first(where: { FileManager.default.fileExists(atPath: $0) }) else {
            NSLog("[AppDelegate] Agent executable not found. Tried: %@", candidates.joined(separator: " | "))
            return
        }

        // pgrep exits 0 if at least one matching process is found.
        let check = Process()
        check.executableURL = URL(fileURLWithPath: "/usr/bin/pgrep")
        check.arguments = ["-f", "TimeTrack.MacOSAgentService"]
        check.standardOutput = Pipe()   // discard output
        check.standardError  = Pipe()
        try? check.run()
        check.waitUntilExit()

        guard check.terminationStatus != 0 else {
            NSLog("[AppDelegate] Agent is already running — skipping launch")
            return
        }

        NSLog("[AppDelegate] Agent not running — launching %@", agentPath)
        let agentDir = URL(fileURLWithPath: agentPath).deletingLastPathComponent().path

        let agent = Process()
        agent.executableURL = URL(fileURLWithPath: agentPath)
        agent.currentDirectoryURL = URL(fileURLWithPath: agentDir)
        agent.environment = ProcessInfo.processInfo.environment.merging([
            "DOTNET_ENVIRONMENT":   "Production",
            "DOTNET_CONTENT_ROOT":  agentDir
        ]) { _, new in new }

        do {
            try agent.run()
            NSLog("[AppDelegate] Agent started with PID %d", agent.processIdentifier)
        } catch {
            NSLog("[AppDelegate] Failed to start agent: %@", error.localizedDescription)
        }
    }

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
