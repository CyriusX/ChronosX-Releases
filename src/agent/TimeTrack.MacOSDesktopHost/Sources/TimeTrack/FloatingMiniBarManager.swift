import AppKit
import Foundation

/// Manages the lifecycle of the floating mini bar.
/// Shows the bar when the main window is minimized, hides it when restored.
/// Refreshes tracking data every second via IPC queries to the agent service.
@MainActor
final class FloatingMiniBarManager {
    private let ipcClient: IpcClient
    private weak var mainWindow: NSWindow?
    private var miniBar: FloatingMiniBarWindow?
    private var refreshTimer: Timer?
    private var minimizeObserver: NSObjectProtocol?
    private var deminiaturizeObserver: NSObjectProtocol?
    private var closeObserver: NSObjectProtocol?

    init(ipcClient: IpcClient, mainWindow: NSWindow) {
        self.ipcClient = ipcClient
        self.mainWindow = mainWindow

        setupObservers()
    }

    deinit {
        if let obs = minimizeObserver { NotificationCenter.default.removeObserver(obs) }
        if let obs = deminiaturizeObserver { NotificationCenter.default.removeObserver(obs) }
        if let obs = closeObserver { NotificationCenter.default.removeObserver(obs) }
        refreshTimer?.invalidate()
        refreshTimer = nil
    }

    // MARK: - Window Observation

    private func setupObservers() {
        minimizeObserver = NotificationCenter.default.addObserver(
            forName: NSWindow.willMiniaturizeNotification,
            object: mainWindow,
            queue: .main
        ) { [weak self] _ in
            Task { @MainActor in
                self?.showMiniBar()
            }
        }

        deminiaturizeObserver = NotificationCenter.default.addObserver(
            forName: NSWindow.didDeminiaturizeNotification,
            object: mainWindow,
            queue: .main
        ) { [weak self] _ in
            Task { @MainActor in
                self?.hideMiniBar()
            }
        }

        // Also handle close-to-minimize: when the user clicks the red X,
        // minimize instead of closing, which triggers the mini bar.
        closeObserver = NotificationCenter.default.addObserver(
            forName: NSWindow.willCloseNotification,
            object: mainWindow,
            queue: .main
        ) { [weak self] _ in
            Task { @MainActor in
                self?.showMiniBar()
            }
        }
    }

    // MARK: - Mini Bar Lifecycle

    private func showMiniBar() {
        guard miniBar == nil else { return }

        let bar = FloatingMiniBarWindow()
        bar.onRestoreRequested = { [weak self] in
            self?.restoreMainWindow()
        }
        bar.orderFrontRegardless()
        miniBar = bar

        startRefresh()

        // Fetch data immediately
        Task {
            await refreshData()
        }
    }

    private func hideMiniBar() {
        stopRefresh()
        miniBar?.stopAnimations()
        miniBar?.close()
        miniBar = nil
    }

    func restoreMainWindow() {
        guard let window = mainWindow else { return }

        hideMiniBar()

        window.deminiaturize(nil)
        window.makeKeyAndOrderFront(nil)
        NSApp.activate(ignoringOtherApps: true)
    }

    // MARK: - Data Refresh

    private func startRefresh() {
        refreshTimer = Timer.scheduledTimer(withTimeInterval: 1.0, repeats: true) { [weak self] _ in
            Task { @MainActor in
                await self?.refreshData()
            }
        }
    }

    private func stopRefresh() {
        refreshTimer?.invalidate()
        refreshTimer = nil
    }

    private func refreshData() async {
        guard let bar = miniBar, ipcClient.isConnected else { return }

        var data = FloatingMiniBarWindow.Data()

        // Fetch tracking state
        if let stateResponse = try? await ipcClient.sendQuery("getTrackingState"),
           stateResponse.success,
           let stateJson = stateResponse.data?.value as? String,
           let stateData = stateJson.data(using: .utf8),
           let state = try? JSONSerialization.jsonObject(with: stateData) as? [String: Any] {
            data.isTracking = state["isTracking"] as? Bool ?? false
            data.isPaused = state["isPaused"] as? Bool ?? false
        }

        // Fetch today's summary
        if let summaryResponse = try? await ipcClient.sendQuery("getTodaySummary"),
           summaryResponse.success,
           let summaryJson = summaryResponse.data?.value as? String,
           let summaryData = summaryJson.data(using: .utf8),
           let summary = try? JSONSerialization.jsonObject(with: summaryData) as? [String: Any] {
            data.totalDurationSeconds = summary["totalDuration"] as? Int ?? 0
            data.productiveTimeSeconds = summary["productiveTime"] as? Int ?? 0
            data.focusScore = summary["focusScore"] as? Int ?? 0
        }

        bar.updateData(data)
    }
}
