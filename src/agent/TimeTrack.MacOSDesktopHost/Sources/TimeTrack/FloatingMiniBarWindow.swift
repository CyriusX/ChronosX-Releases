import AppKit
import QuartzCore

/// A floating pill-shaped status bar that appears when the main window is minimized.
/// Shows tracking status, active time, productive time, and focus score.
final class FloatingMiniBarWindow: NSPanel {
    // Theme colors matching the app
    private static let bgColor = NSColor(red: 16/255, green: 18/255, blue: 27/255, alpha: 0.94)
    private static let borderColor = NSColor(red: 45/255, green: 50/255, blue: 65/255, alpha: 1)
    private static let textPrimary = NSColor(red: 240/255, green: 243/255, blue: 255/255, alpha: 1)
    private static let textMuted = NSColor(red: 130/255, green: 140/255, blue: 160/255, alpha: 1)
    private static let cyan = NSColor(red: 74/255, green: 217/255, blue: 255/255, alpha: 1)
    private static let green = NSColor(red: 74/255, green: 222/255, blue: 128/255, alpha: 1)
    private static let amber = NSColor(red: 251/255, green: 191/255, blue: 36/255, alpha: 1)
    private static let redSoft = NSColor(red: 248/255, green: 113/255, blue: 113/255, alpha: 1)
    private static let sepColor = NSColor(red: 40/255, green: 44/255, blue: 60/255, alpha: 1)

    private static let barHeight: CGFloat = 44
    private static let cornerRadius: CGFloat = 22

    // UI elements
    private let statusDot = NSView()
    private let timeIcon = NSTextField(labelWithString: "")
    private let timeValue = NSTextField(labelWithString: "--h --m")
    private let prodIcon = NSTextField(labelWithString: "")
    private let prodValue = NSTextField(labelWithString: "--h --m")
    private let scoreIcon = NSTextField(labelWithString: "")
    private let scoreValue = NSTextField(labelWithString: "--")
    private let restoreButton = NSButton()
    private let stackView = NSStackView()

    // Pulsing animation — driven by Core Animation so the render server handles
    // interpolation; no per-frame work on the app main thread.
    private static let pulseAnimationKey = "statusDotPulse"

    var onRestoreRequested: (() -> Void)?

    init() {
        let frame = NSRect(x: 0, y: 0, width: 420, height: Self.barHeight)
        super.init(
            contentRect: frame,
            styleMask: [.borderless, .nonactivatingPanel],
            backing: .buffered,
            defer: false
        )

        title = "TimeTrack Status"
        level = .floating
        isFloatingPanel = true
        isMovableByWindowBackground = true
        hidesOnDeactivate = false
        collectionBehavior = [.canJoinAllSpaces, .fullScreenAuxiliary]
        isOpaque = false
        backgroundColor = .clear
        hasShadow = true

        setupContentView()
        restorePosition()
        startPulseAnimation()
    }

    private func setupContentView() {
        let container = NSView(frame: NSRect(x: 0, y: 0, width: 420, height: Self.barHeight))
        container.wantsLayer = true
        container.layer?.backgroundColor = Self.bgColor.cgColor
        container.layer?.cornerRadius = Self.cornerRadius
        container.layer?.borderWidth = 1
        container.layer?.borderColor = Self.borderColor.cgColor

        // Status dot (pulsing LED) — green when tracking, red when stopped/paused
        statusDot.wantsLayer = true
        statusDot.layer?.backgroundColor = Self.green.cgColor
        statusDot.layer?.cornerRadius = 5
        statusDot.translatesAutoresizingMaskIntoConstraints = false
        NSLayoutConstraint.activate([
            statusDot.widthAnchor.constraint(equalToConstant: 10),
            statusDot.heightAnchor.constraint(equalToConstant: 10),
        ])

        // Configure labels
        configureLabel(timeIcon, string: "\u{23F1}", color: Self.cyan, size: 13)
        configureLabel(timeValue, string: "--h --m", color: Self.textPrimary, size: 12, bold: true)
        configureLabel(prodIcon, string: "\u{2714}", color: Self.green, size: 11)
        configureLabel(prodValue, string: "--h --m", color: Self.green, size: 12, bold: true)
        configureLabel(scoreIcon, string: "\u{26A1}", color: Self.amber, size: 13)
        configureLabel(scoreValue, string: "--", color: Self.amber, size: 12, bold: true)

        // Separators
        let sep1 = makeSeparator()
        let sep2 = makeSeparator()
        let sep3 = makeSeparator()

        // Restore button (app icon)
        restoreButton.bezelStyle = .inline
        restoreButton.isBordered = false
        restoreButton.image = NSImage(systemSymbolName: "clock.fill", accessibilityDescription: "Restore")
        restoreButton.contentTintColor = Self.cyan
        restoreButton.target = self
        restoreButton.action = #selector(restoreClicked)
        restoreButton.toolTip = "Open TimeTrack"
        restoreButton.translatesAutoresizingMaskIntoConstraints = false
        NSLayoutConstraint.activate([
            restoreButton.widthAnchor.constraint(equalToConstant: 24),
            restoreButton.heightAnchor.constraint(equalToConstant: 24),
        ])

        // Build stack
        stackView.orientation = .horizontal
        stackView.alignment = .centerY
        stackView.distribution = .gravityAreas
        stackView.spacing = 8
        stackView.edgeInsets = NSEdgeInsets(top: 0, left: 16, bottom: 0, right: 16)
        stackView.setHuggingPriority(.required, for: .horizontal)

        stackView.addArrangedSubview(statusDot)
        stackView.addArrangedSubview(spacer(width: 4))
        stackView.addArrangedSubview(timeIcon)
        stackView.addArrangedSubview(timeValue)
        stackView.addArrangedSubview(sep1)
        stackView.addArrangedSubview(prodIcon)
        stackView.addArrangedSubview(prodValue)
        stackView.addArrangedSubview(sep2)
        stackView.addArrangedSubview(scoreIcon)
        stackView.addArrangedSubview(scoreValue)
        stackView.addArrangedSubview(sep3)
        stackView.addArrangedSubview(restoreButton)

        stackView.translatesAutoresizingMaskIntoConstraints = false
        container.addSubview(stackView)
        NSLayoutConstraint.activate([
            stackView.leadingAnchor.constraint(equalTo: container.leadingAnchor),
            stackView.topAnchor.constraint(equalTo: container.topAnchor),
            stackView.bottomAnchor.constraint(equalTo: container.bottomAnchor),
            // Pin trailing to container so container sizes to stack
            stackView.trailingAnchor.constraint(equalTo: container.trailingAnchor),
        ])

        contentView = container

        // Force layout and resize window to fit content exactly
        container.layoutSubtreeIfNeeded()
        let fitWidth = stackView.fittingSize.width
        setContentSize(NSSize(width: fitWidth, height: Self.barHeight))
        container.frame = NSRect(x: 0, y: 0, width: fitWidth, height: Self.barHeight)
    }

    // MARK: - Public API

    struct Data {
        var isTracking: Bool = false
        var isPaused: Bool = false
        var totalDurationSeconds: Int = 0
        var productiveTimeSeconds: Int = 0
        var focusScore: Int = 0
    }

    func updateData(_ data: Data) {
        // Status dot color — green when tracking, red when stopped/paused
        if data.isPaused || !data.isTracking {
            statusDot.layer?.backgroundColor = Self.redSoft.cgColor
        } else {
            statusDot.layer?.backgroundColor = Self.green.cgColor
        }

        // Active time
        timeValue.stringValue = formatDuration(data.totalDurationSeconds)

        // Productive time
        prodValue.stringValue = formatDuration(data.productiveTimeSeconds)

        // Focus score
        scoreValue.stringValue = "\(data.focusScore)"
        let scoreColor = data.focusScore >= 60 ? Self.amber : Self.redSoft
        scoreValue.textColor = scoreColor
        scoreIcon.textColor = scoreColor
    }

    // MARK: - Position Persistence

    private static let positionKey = "floatingMiniBarPosition"

    private func restorePosition() {
        if let posStr = UserDefaults.standard.string(forKey: Self.positionKey) {
            let parts = posStr.split(separator: ",")
            if parts.count == 2, let x = Double(parts[0]), let y = Double(parts[1]) {
                let origin = NSPoint(x: x, y: y)
                // Validate the saved position is visible on at least one screen.
                // A stale position from a disconnected monitor would leave the bar
                // off-screen with no way for the user to retrieve it.
                let barRect = NSRect(origin: origin, size: frame.size)
                let isOnScreen = NSScreen.screens.contains { screen in
                    barRect.intersects(screen.visibleFrame)
                }
                if isOnScreen {
                    setFrameOrigin(origin)
                    return
                }
                // Invalid — clear the stale value and fall through to default placement
                UserDefaults.standard.removeObject(forKey: Self.positionKey)
            }
        }
        // Default: center top of main screen
        if let screen = NSScreen.main {
            let screenFrame = screen.visibleFrame
            let x = screenFrame.midX - frame.width / 2
            let y = screenFrame.maxY - frame.height - 20
            setFrameOrigin(NSPoint(x: x, y: y))
        }
    }

    private func savePosition() {
        let pos = frame.origin
        UserDefaults.standard.set("\(pos.x),\(pos.y)", forKey: Self.positionKey)
    }

    override func mouseDragged(with event: NSEvent) {
        super.mouseDragged(with: event)
        savePosition()
    }

    // MARK: - Pulse Animation

    private func startPulseAnimation() {
        statusDot.wantsLayer = true
        guard let layer = statusDot.layer else { return }
        layer.removeAnimation(forKey: Self.pulseAnimationKey)

        let pulse = CABasicAnimation(keyPath: "opacity")
        pulse.fromValue = 1.0
        pulse.toValue = 0.3
        pulse.duration = 1.2
        pulse.autoreverses = true
        pulse.repeatCount = .infinity
        pulse.timingFunction = CAMediaTimingFunction(name: .easeInEaseOut)
        pulse.isRemovedOnCompletion = false
        layer.add(pulse, forKey: Self.pulseAnimationKey)
    }

    func stopAnimations() {
        statusDot.layer?.removeAnimation(forKey: Self.pulseAnimationKey)
    }

    // MARK: - Helpers

    @objc private func restoreClicked() {
        onRestoreRequested?()
    }

    private func configureLabel(_ label: NSTextField, string: String, color: NSColor, size: CGFloat, bold: Bool = false) {
        label.stringValue = string
        label.textColor = color
        label.font = bold
            ? NSFont.systemFont(ofSize: size, weight: .semibold)
            : NSFont.systemFont(ofSize: size)
        label.isBezeled = false
        label.drawsBackground = false
        label.isEditable = false
        label.isSelectable = false
        label.alignment = .center
        label.setContentHuggingPriority(.required, for: .horizontal)
    }

    private func makeSeparator() -> NSView {
        let sep = NSView()
        sep.wantsLayer = true
        sep.layer?.backgroundColor = Self.sepColor.cgColor
        sep.translatesAutoresizingMaskIntoConstraints = false
        NSLayoutConstraint.activate([
            sep.widthAnchor.constraint(equalToConstant: 1),
            sep.heightAnchor.constraint(equalToConstant: 20),
        ])
        return sep
    }

    private func spacer(width: CGFloat) -> NSView {
        let v = NSView()
        v.translatesAutoresizingMaskIntoConstraints = false
        v.widthAnchor.constraint(equalToConstant: width).isActive = true
        return v
    }

    private func formatDuration(_ totalSeconds: Int) -> String {
        let h = totalSeconds / 3600
        let m = (totalSeconds % 3600) / 60
        if h > 0 {
            return "\(h)h \(String(format: "%02d", m))m"
        }
        return "\(m)m"
    }
}
