import SwiftUI
import AppKit

@MainActor
class MenuBarController: ObservableObject {
    private var statusItem: NSStatusItem?
    @Published var isTracking = false
    private var ipcClient: IpcClient?
    private var menuDelegate: MenuDelegate?

    func setupMenuBar(ipcClient: IpcClient) {
        self.ipcClient = ipcClient
        statusItem = NSStatusBar.system.statusItem(withLength: NSStatusItem.variableLength)

        if let button = statusItem?.button {
            button.image = NSImage(systemSymbolName: "clock.fill", accessibilityDescription: "TimeTrack")
            button.image?.isTemplate = true
        }

        let menu = NSMenu()
        let delegate = MenuDelegate(controller: self)
        menuDelegate = delegate
        menu.delegate = delegate

        menu.addItem(NSMenuItem(title: "Open TimeTrack", action: #selector(openWindow), keyEquivalent: "o"))
        let trackingItem = NSMenuItem(title: "Start Tracking", action: #selector(toggleTracking), keyEquivalent: "s")
        trackingItem.tag = 1
        menu.addItem(trackingItem)
        menu.addItem(NSMenuItem.separator())
        menu.addItem(NSMenuItem(title: "Settings...", action: #selector(openSettings), keyEquivalent: ","))
        menu.addItem(NSMenuItem.separator())
        menu.addItem(NSMenuItem(title: "Quit TimeTrack", action: #selector(quit), keyEquivalent: "q"))

        statusItem?.menu = menu
    }

    @objc func openWindow() {
        NSApplication.shared.windows.first?.makeKeyAndOrderFront(nil)
        NSApp.activate(ignoringOtherApps: true)
    }

    @objc func toggleTracking() {
        Task {
            guard let client = ipcClient else { return }
            do {
                if isTracking {
                    _ = try await client.sendCommand("stopTracking")
                    isTracking = false
                } else {
                    _ = try await client.sendCommand("startTracking")
                    isTracking = true
                }
                updateTrackingMenuItem()
            } catch {
                print("Error toggling tracking: \(error)")
            }
        }
    }

    @objc func openSettings() {
        NSApp.sendAction(Selector(("showSettingsWindow:")), to: nil, from: nil)
    }

    @objc func quit() {
        NSApplication.shared.terminate(nil)
    }

    func updateTrackingMenuItem() {
        guard let menu = statusItem?.menu else { return }
        for item in menu.items {
            if item.tag == 1 {
                item.title = isTracking ? "Stop Tracking" : "Start Tracking"
            }
        }
    }
}

private class MenuDelegate: NSObject, NSMenuDelegate {
    let controller: MenuBarController
    init(controller: MenuBarController) { self.controller = controller }

    func menuNeedsUpdate(_ menu: NSMenu) {
        controller.updateTrackingMenuItem()
    }
}
