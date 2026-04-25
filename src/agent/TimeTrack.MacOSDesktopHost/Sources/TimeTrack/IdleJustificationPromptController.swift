import AppKit
import SwiftUI

struct IdleJustificationPromptPayload: Decodable {
    let idlePeriodId: String
    let startedAt: String
    let endedAt: String
    let durationSeconds: Int
}

@MainActor
final class IdleJustificationPromptController: ObservableObject {
    private let ipcClient: IpcClient
    private var panel: NSPanel?

    @Published private var currentPayload: IdleJustificationPromptPayload?

    init(ipcClient: IpcClient) {
        self.ipcClient = ipcClient
    }

    func showPrompt(_ payload: IdleJustificationPromptPayload) {
        currentPayload = payload

        if let panel {
            panel.makeKeyAndOrderFront(nil)
            panel.orderFrontRegardless()
            return
        }

        let view = IdleJustificationPromptView(controller: self, payload: payload)
        let hosting = NSHostingView(rootView: view)
        hosting.frame = NSRect(x: 0, y: 0, width: 380, height: 360)

        let panel = NSPanel(
            contentRect: hosting.frame,
            styleMask: [.borderless, .nonactivatingPanel],
            backing: .buffered,
            defer: false
        )

        panel.isFloatingPanel = true
        panel.level = .floating
        panel.collectionBehavior = [.canJoinAllSpaces, .fullScreenAuxiliary]
        panel.backgroundColor = .clear
        panel.isOpaque = false
        panel.hasShadow = true
        panel.hidesOnDeactivate = false
        panel.contentView = hosting

        position(panel)

        self.panel = panel
        panel.orderFrontRegardless()
    }

    func skip() {
        guard let payload = currentPayload else { return }
        Task {
            _ = try? await ipcClient.sendCommand("dismissIdleJustification", payload: [
                "idlePeriodId": payload.idlePeriodId,
            ])
        }
        close()
    }

    func submit(reasonCode: String, note: String) {
        guard let payload = currentPayload else { return }
        Task {
            _ = try? await ipcClient.sendCommand("submitIdleJustification", payload: [
                "idlePeriodId": payload.idlePeriodId,
                "reasonCode": reasonCode,
                "note": note.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty ? NSNull() : note.trimmingCharacters(in: .whitespacesAndNewlines),
            ])
        }
        close()
    }

    func close() {
        panel?.close()
        panel = nil
        currentPayload = nil
    }

    private func position(_ panel: NSPanel) {
        guard let screen = NSScreen.main else { return }
        let frame = screen.visibleFrame
        let x = frame.maxX - panel.frame.width - 24
        let y = frame.maxY - panel.frame.height - 24
        panel.setFrameOrigin(NSPoint(x: x, y: y))
    }
}

private struct IdleJustificationPromptView: View {
    private static let reasons: [(String, String)] = [
        ("break", "Break"),
        ("meeting", "Meeting"),
        ("personal", "Personal"),
        ("technical_issue", "Technical issue"),
        ("context_switch", "Context switch"),
        ("other", "Other"),
    ]

    @ObservedObject var controller: IdleJustificationPromptController
    let payload: IdleJustificationPromptPayload

    @State private var selectedReason: String?
    @State private var note = ""

    var body: some View {
        VStack(alignment: .leading, spacing: 14) {
            Text("You were idle for a while")
                .font(.system(size: 17, weight: .semibold))
                .foregroundStyle(Color.white)

            Text("\(formatTime(payload.startedAt)) - \(formatTime(payload.endedAt))  •  \(formatDuration(payload.durationSeconds))")
                .font(.system(size: 12))
                .foregroundStyle(Color(red: 180/255, green: 186/255, blue: 205/255))

            Text("Add a quick reason if you want. You can also skip this.")
                .font(.system(size: 12))
                .foregroundStyle(Color(red: 180/255, green: 186/255, blue: 205/255))

            LazyVGrid(columns: [GridItem(.flexible()), GridItem(.flexible()), GridItem(.flexible())], spacing: 8) {
                ForEach(Self.reasons, id: \.0) { reason in
                    Button(reason.1) {
                        selectedReason = reason.0
                    }
                    .buttonStyle(ReasonChipStyle(isSelected: selectedReason == reason.0))
                }
            }

            ZStack(alignment: .topLeading) {
                RoundedRectangle(cornerRadius: 12)
                    .fill(Color(red: 18/255, green: 21/255, blue: 31/255))
                    .overlay(
                        RoundedRectangle(cornerRadius: 12)
                            .stroke(Color.white.opacity(0.08), lineWidth: 1)
                    )

                TextEditor(text: Binding(
                    get: { note },
                    set: { note = String($0.prefix(500)) }
                ))
                .scrollContentBackground(.hidden)
                .font(.system(size: 12))
                .foregroundStyle(Color.white)
                .padding(8)
                .frame(height: 96)

                if note.isEmpty {
                    Text("Optional note")
                        .font(.system(size: 12))
                        .foregroundStyle(Color.white.opacity(0.32))
                        .padding(.horizontal, 14)
                        .padding(.vertical, 14)
                        .allowsHitTesting(false)
                }
            }
            .frame(height: 96)

            HStack {
                Text("\(note.count) / 500")
                    .font(.system(size: 11))
                    .foregroundStyle(Color.white.opacity(0.45))

                Spacer()

                Button("Skip") {
                    controller.skip()
                }
                .buttonStyle(SecondaryActionButtonStyle())

                Button("Save reason") {
                    if let selectedReason {
                        controller.submit(reasonCode: selectedReason, note: note)
                    }
                }
                .buttonStyle(PrimaryActionButtonStyle())
                .disabled(selectedReason == nil)
            }
        }
        .padding(18)
        .frame(width: 380)
        .background(
            RoundedRectangle(cornerRadius: 18)
                .fill(Color(red: 22/255, green: 25/255, blue: 36/255))
                .overlay(
                    RoundedRectangle(cornerRadius: 18)
                        .stroke(Color.white.opacity(0.08), lineWidth: 1)
                )
        )
    }

    private func formatTime(_ raw: String) -> String {
        guard let date = ISO8601DateFormatter().date(from: raw) else { return raw }
        let formatter = DateFormatter()
        formatter.timeStyle = .short
        return formatter.string(from: date)
    }

    private func formatDuration(_ seconds: Int) -> String {
        let duration = max(0, seconds)
        if duration >= 3600 {
            return "\(duration / 3600)h \((duration % 3600) / 60)m"
        }
        if duration >= 60 {
            return "\(duration / 60)m"
        }
        return "\(duration)s"
    }
}

private struct ReasonChipStyle: ButtonStyle {
    let isSelected: Bool

    func makeBody(configuration: Configuration) -> some View {
        configuration.label
            .font(.system(size: 11, weight: .medium))
            .foregroundStyle(isSelected ? Color(red: 125/255, green: 211/255, blue: 252/255) : Color.white.opacity(0.72))
            .frame(maxWidth: .infinity)
            .padding(.vertical, 9)
            .background(
                RoundedRectangle(cornerRadius: 10)
                    .fill(isSelected ? Color(red: 23/255, green: 37/255, blue: 56/255) : Color(red: 36/255, green: 41/255, blue: 56/255))
                    .overlay(
                        RoundedRectangle(cornerRadius: 10)
                            .stroke(isSelected ? Color(red: 125/255, green: 211/255, blue: 252/255) : Color.white.opacity(0.08), lineWidth: 1)
                    )
            )
            .opacity(configuration.isPressed ? 0.9 : 1)
    }
}

private struct SecondaryActionButtonStyle: ButtonStyle {
    func makeBody(configuration: Configuration) -> some View {
        configuration.label
            .font(.system(size: 12, weight: .medium))
            .foregroundStyle(Color.white.opacity(0.75))
            .padding(.horizontal, 14)
            .padding(.vertical, 8)
            .background(
                RoundedRectangle(cornerRadius: 10)
                    .fill(Color(red: 36/255, green: 41/255, blue: 56/255))
                    .overlay(
                        RoundedRectangle(cornerRadius: 10)
                            .stroke(Color.white.opacity(0.08), lineWidth: 1)
                    )
            )
            .opacity(configuration.isPressed ? 0.9 : 1)
    }
}

private struct PrimaryActionButtonStyle: ButtonStyle {
    func makeBody(configuration: Configuration) -> some View {
        configuration.label
            .font(.system(size: 12, weight: .semibold))
            .foregroundStyle(Color(red: 9/255, green: 12/255, blue: 18/255))
            .padding(.horizontal, 14)
            .padding(.vertical, 8)
            .background(
                RoundedRectangle(cornerRadius: 10)
                    .fill(Color(red: 125/255, green: 211/255, blue: 252/255))
            )
            .opacity(configuration.isPressed ? 0.9 : 1)
    }
}
