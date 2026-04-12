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
                        // Retry connection up to 10 times on startup — the agent
                        // service may not have created the IPC socket yet.
                        for attempt in 1...10 {
                            do {
                                try await ipcClient.connect()
                                print("Connected to AgentService (attempt \(attempt))")
                                break
                            } catch {
                                print("Failed to connect to AgentService (attempt \(attempt)): \(error)")
                                if attempt < 10 {
                                    try? await Task.sleep(nanoseconds: 1_000_000_000)
                                }
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
    @AppStorage("startAtLogin") private var startAtLogin = false
    @AppStorage("showNotifications") private var showNotifications = true
    @AppStorage("idleThresholdMinutes") private var idleThresholdMinutes = 5

    var body: some View {
        Form {
            Toggle("Start at Login", isOn: $startAtLogin)
            Toggle("Show Notifications", isOn: $showNotifications)
            Stepper("Idle threshold: \(idleThresholdMinutes) min", value: $idleThresholdMinutes, in: 1...30)
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
