import Foundation

final class AgentLauncher {
    static let shared = AgentLauncher()

    private var launchedProcess: Process?
    private var lastLaunchAttempt: Date?

    private init() {}

    func ensureRunning() {
        // Throttle repeated spawn attempts (e.g. if the agent keeps crashing on boot).
        if let last = lastLaunchAttempt, Date().timeIntervalSince(last) < 1.0 {
            return
        }
        lastLaunchAttempt = Date()

        let bundlePath = Bundle.main.bundlePath
        let candidates = [
            bundlePath.appending("/Contents/Resources/agent/TimeTrack.MacOSAgentService"),
            bundlePath.appending("/Contents/MacOS/agent/TimeTrack.MacOSAgentService"),
        ]

        guard let agentPath = candidates.first(where: { FileManager.default.fileExists(atPath: $0) }) else {
            NSLog("[AgentLauncher] Agent executable not found. Tried: %@", candidates.joined(separator: " | "))
            return
        }

        // If we launched it and it's still running, we're done.
        if let proc = launchedProcess, proc.isRunning {
            return
        }

        // Check if something else (LaunchAgent / previous run) already has it running.
        if isAgentRunning() {
            return
        }

        launchAgent(at: agentPath)
    }

    private func isAgentRunning() -> Bool {
        let check = Process()
        check.executableURL = URL(fileURLWithPath: "/usr/bin/pgrep")
        check.arguments = ["-f", "TimeTrack.MacOSAgentService"]
        check.standardOutput = Pipe()
        check.standardError = Pipe()
        try? check.run()
        check.waitUntilExit()
        return check.terminationStatus == 0
    }

    private func launchAgent(at agentPath: String) {
        NSLog("[AgentLauncher] Agent not running — launching %@", agentPath)

        let agentDir = URL(fileURLWithPath: agentPath).deletingLastPathComponent().path
        let agent = Process()
        agent.executableURL = URL(fileURLWithPath: agentPath)
        agent.currentDirectoryURL = URL(fileURLWithPath: agentDir)
        agent.environment = ProcessInfo.processInfo.environment.merging([
            "DOTNET_ENVIRONMENT": "Production",
            "DOTNET_CONTENT_ROOT": agentDir
        ]) { _, new in new }

        // Redirect agent stdout/stderr to a user-accessible log file for troubleshooting.
        do {
            if let library = FileManager.default.urls(for: .libraryDirectory, in: .userDomainMask).first {
                let logsDir = library.appendingPathComponent("Logs/TimeTrack", isDirectory: true)
                try? FileManager.default.createDirectory(at: logsDir, withIntermediateDirectories: true)
                let logUrl = logsDir.appendingPathComponent("agent.log")
                if !FileManager.default.fileExists(atPath: logUrl.path) {
                    FileManager.default.createFile(atPath: logUrl.path, contents: nil)
                }
                let handle = try FileHandle(forWritingTo: logUrl)
                try? handle.seekToEnd()
                agent.standardOutput = handle
                agent.standardError = handle
                NSLog("[AgentLauncher] Agent logs redirected to %@", logUrl.path)
            }
        } catch {
            NSLog("[AgentLauncher] Failed to set up agent log redirection: %@", error.localizedDescription)
        }

        agent.terminationHandler = { [weak self] _ in
            guard let self else { return }
            NSLog("[AgentLauncher] Agent terminated — will attempt restart")
            DispatchQueue.main.asyncAfter(deadline: .now() + 2.0) {
                self.ensureRunning()
            }
        }

        do {
            try agent.run()
            launchedProcess = agent
            NSLog("[AgentLauncher] Agent started with PID %d", agent.processIdentifier)
        } catch {
            NSLog("[AgentLauncher] Failed to start agent: %@", error.localizedDescription)
        }
    }
}

