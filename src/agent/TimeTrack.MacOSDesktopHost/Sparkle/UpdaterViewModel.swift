import Foundation
import Sparkle

@MainActor
class UpdaterViewModel: ObservableObject {
    private let updater: SPUUpdater

    @Published var canCheckForUpdates = false
    @Published var lastUpdateCheckDate: Date?
    @Published var isChecking = false

    init() {
        let controller = SPUStandardUpdaterController(
            startingUpdater: true,
            updaterDelegate: nil,
            userDriverDelegate: nil
        )
        updater = controller.updater
        canCheckForUpdates = updater.canCheckForUpdates
    }

    func checkForUpdates() {
        guard updater.canCheckForUpdates else { return }
        isChecking = true
        updater.checkForUpdates()
        lastUpdateCheckDate = Date()
        isChecking = false
    }
}
