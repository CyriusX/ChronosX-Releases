import Foundation

/// Manages the user-level LaunchAgent that keeps the TimeTrack background
/// service running and restarts it automatically after login.
///
/// The plist is written to ~/Library/LaunchAgents/ at runtime so it always
/// reflects the actual install path of the app bundle (important after the
/// user moves the app).
@MainActor
class LaunchAtLoginManager: ObservableObject {

    static let shared = LaunchAtLoginManager()

    private let label   = "com.cyriusx.timetrack.agent"
    private let plistName = "com.cyriusx.timetrack.agent.plist"

    private var launchAgentsDir: URL {
        FileManager.default.homeDirectoryForCurrentUser
            .appendingPathComponent("Library/LaunchAgents", isDirectory: true)
    }

    private var plistURL: URL {
        launchAgentsDir.appendingPathComponent(plistName)
    }

    /// Whether the LaunchAgent plist is currently installed.
    @Published var isEnabled: Bool

    init() {
        isEnabled = FileManager.default.fileExists(atPath:
            FileManager.default.homeDirectoryForCurrentUser
                .appendingPathComponent("Library/LaunchAgents/com.cyriusx.timetrack.agent.plist").path
        )
    }

    // MARK: - Public API

    func setEnabled(_ enabled: Bool) {
        if enabled { install() } else { uninstall() }
        isEnabled = FileManager.default.fileExists(atPath: plistURL.path)
    }

    // MARK: - Private helpers

    private func agentExecutablePath() -> String {
        Bundle.main.bundlePath + "/Contents/Resources/agent/TimeTrack.MacOSAgentService"
    }

    private func agentContentRoot() -> String {
        Bundle.main.bundlePath + "/Contents/Resources/agent"
    }

    private func install() {
        let execPath    = agentExecutablePath()
        let contentRoot = agentContentRoot()

        let plist = """
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN"
  "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
    <key>Label</key>
    <string>\(label)</string>
    <key>ProgramArguments</key>
    <array>
        <string>\(execPath)</string>
    </array>
    <key>RunAtLoad</key>
    <true/>
    <key>KeepAlive</key>
    <true/>
    <key>ProcessType</key>
    <string>Background</string>
    <key>ThrottleInterval</key>
    <integer>5</integer>
    <key>StandardOutPath</key>
    <string>/tmp/timetrack-agent.log</string>
    <key>StandardErrorPath</key>
    <string>/tmp/timetrack-agent.log</string>
    <key>EnvironmentVariables</key>
    <dict>
        <key>DOTNET_ENVIRONMENT</key>
        <string>Production</string>
        <key>DOTNET_CONTENT_ROOT</key>
        <string>\(contentRoot)</string>
    </dict>
</dict>
</plist>
"""
        do {
            try FileManager.default.createDirectory(
                at: launchAgentsDir, withIntermediateDirectories: true)
            try plist.write(to: plistURL, atomically: true, encoding: .utf8)
            launchctl(["load", "-w", plistURL.path])
            NSLog("[LaunchAtLogin] Installed and loaded plist at %@", plistURL.path)
        } catch {
            NSLog("[LaunchAtLogin] Failed to install plist: %@", error.localizedDescription)
        }
    }

    private func uninstall() {
        guard FileManager.default.fileExists(atPath: plistURL.path) else { return }
        launchctl(["unload", "-w", plistURL.path])
        try? FileManager.default.removeItem(at: plistURL)
        NSLog("[LaunchAtLogin] Unloaded and removed plist")
    }

    @discardableResult
    private func launchctl(_ args: [String]) -> Int32 {
        let task = Process()
        task.executableURL = URL(fileURLWithPath: "/bin/launchctl")
        task.arguments = args
        task.standardOutput = Pipe()
        task.standardError  = Pipe()
        try? task.run()
        task.waitUntilExit()
        return task.terminationStatus
    }
}
