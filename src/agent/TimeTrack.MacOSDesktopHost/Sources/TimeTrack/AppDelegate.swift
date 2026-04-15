import AppKit

class AppDelegate: NSObject, NSApplicationDelegate {
    func applicationDidFinishLaunching(_ notification: Notification) {
        // Ensure the background agent service is running. This covers the case
        // where the user launched the app directly (e.g. double-click, Spotlight)
        // without the LaunchAgent having started the service first.
        ensureAgentRunning()
    }

    // MARK: - Agent lifecycle

    /// Launches the .NET background agent if it is not already running.
    private func ensureAgentRunning() {
        let agentPath = Bundle.main.bundlePath
            .appending("/Contents/Resources/agent/TimeTrack.MacOSAgentService")

        guard FileManager.default.fileExists(atPath: agentPath) else {
            NSLog("[AppDelegate] Agent executable not found at: %@", agentPath)
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
