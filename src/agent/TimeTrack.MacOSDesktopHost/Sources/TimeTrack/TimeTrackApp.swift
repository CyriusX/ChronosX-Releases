import SwiftUI

@main
struct TimeTrackApp: App {
    @NSApplicationDelegateAdaptor(AppDelegate.self) var appDelegate
    @StateObject private var ipcClient = IpcClient()
    @StateObject private var notificationManager = NotificationManager()

    var body: some Scene {
        WindowGroup {
            ContentView(ipcClient: ipcClient)
                .onAppear {
                    NSWindow.allowsAutomaticWindowTabbing = false
                    notificationManager.requestAuthorization()
                    Task {
                        // Keep retrying forever until connected — the agent may start
                        // after the UI, or may restart during a session.
                        var attempt = 0
                        while !ipcClient.isConnected {
                            attempt += 1
                            do {
                                try await ipcClient.connect()
                                NSLog("[IPC] Connected to AgentService (attempt %d)", attempt)
                                break
                            } catch {
                                NSLog("[IPC] Failed to connect (attempt %d): %@", attempt, error.localizedDescription)
                                try? await Task.sleep(nanoseconds: 2_000_000_000)
                            }
                        }
                    }
                }
        }
        .windowStyle(.hiddenTitleBar)
        .windowResizability(.contentSize)
        .commands {
            CommandGroup(replacing: .newItem) { }
            CommandGroup(replacing: .help) {
                Button("TimeTrack Documentation") {
                    if let url = URL(string: "https://timetrack.cyrius.com/docs") {
                        NSWorkspace.shared.open(url)
                    }
                }
            }
        }

        Settings {
            SettingsView()
        }
    }
}

struct SettingsView: View {
    var body: some View {
        TabView {
            GeneralSettingsView()
                .tabItem { Label("General", systemImage: "gear") }
            AboutView()
                .tabItem { Label("About", systemImage: "info.circle") }
        }
        .frame(width: 450, height: 300)
    }
}

struct GeneralSettingsView: View {
    @StateObject private var loginManager = LaunchAtLoginManager.shared
    @AppStorage("showNotifications") private var showNotifications = true
    @AppStorage("idleThresholdMinutes") private var idleThresholdMinutes = 5

    var body: some View {
        Form {
            Toggle("Start at Login", isOn: Binding(
                get: { loginManager.isEnabled },
                set: { loginManager.setEnabled($0) }
            ))

            Toggle("Show Notifications", isOn: $showNotifications)
            Stepper("Idle threshold: \(idleThresholdMinutes) min",
                    value: $idleThresholdMinutes, in: 1...30)
        }
        .padding(20)
    }
}

struct AboutView: View {
    var body: some View {
        VStack(spacing: 12) {
            Image(systemName: "clock.fill")
                .font(.system(size: 48))
                .foregroundColor(.accentColor)
            Text("ChronosX TimeTrack")
                .font(.title2)
                .fontWeight(.bold)
            Text("Version 1.0.0")
                .foregroundColor(.secondary)
            Text("© 2026 CyriusX")
                .font(.caption)
                .foregroundColor(.secondary)
        }
        .padding(40)
    }
}
