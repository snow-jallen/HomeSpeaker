import AppIntents
import SwiftUI
import WidgetKit

// Control Center / Lock Screen / Action button controls (iOS 18+).

@available(iOS 18.0, *)
struct SpeakerControlConfigurationIntent: ControlConfigurationIntent {
    static let title: LocalizedStringResource = "Choose Speaker"

    @Parameter(title: "Speaker")
    var server: ServerEntity?
}

@available(iOS 18.0, *)
struct SetSpeakerPlaybackIntent: SetValueIntent {
    static let title: LocalizedStringResource = "Set HomeSpeaker Playback"
    static let isDiscoverable = false

    @Parameter(title: "Server")
    var server: ServerEntity?

    @Parameter(title: "Playing")
    var value: Bool

    init() {}

    init(server: ServerEntity?) {
        self.server = server
    }

    func perform() async throws -> some IntentResult {
        let connection = try resolveServer(server)
        let api = APIClient.client(for: connection)
        if value {
            try await api.resume()
        } else {
            try await api.stop()
        }
        await SpeakerSurfaces.refresh(after: connection)
        return .result()
    }
}

@available(iOS 18.0, *)
struct SpeakerControlValue {
    var isPlaying: Bool
    var server: ServerEntity?
}

@available(iOS 18.0, *)
struct SpeakerControlValueProvider: AppIntentControlValueProvider {
    func previewValue(configuration: SpeakerControlConfigurationIntent) -> SpeakerControlValue {
        SpeakerControlValue(isPlaying: false, server: configuration.server)
    }

    func currentValue(configuration: SpeakerControlConfigurationIntent) async throws -> SpeakerControlValue {
        let connection = try resolveServer(configuration.server)
        let status = try? await APIClient.client(for: connection).getPlayerStatus()
        return SpeakerControlValue(isPlaying: status?.isPlaying ?? false,
                                   server: ServerEntity(connection: connection))
    }
}

@available(iOS 18.0, *)
struct SpeakerPlaybackControl: ControlWidget {
    static let kind = "com.homespeaker.ios.control.playback"

    var body: some ControlWidgetConfiguration {
        AppIntentControlConfiguration(kind: Self.kind, provider: SpeakerControlValueProvider()) { value in
            ControlWidgetToggle(value.server?.name ?? "HomeSpeaker",
                                isOn: value.isPlaying,
                                action: SetSpeakerPlaybackIntent(server: value.server)) { isOn in
                Label(isOn ? "Playing" : "Stopped", systemImage: isOn ? "hifispeaker.fill" : "hifispeaker")
            }
        }
        .displayName("HomeSpeaker Playback")
        .description("Start or stop music on a speaker.")
    }
}

@available(iOS 18.0, *)
struct SpeakerQuietDownControl: ControlWidget {
    static let kind = "com.homespeaker.ios.control.quietdown"

    var body: some ControlWidgetConfiguration {
        AppIntentControlConfiguration(kind: Self.kind, intent: SpeakerControlConfigurationIntent.self) { configuration in
            ControlWidgetButton(action: QuietSpeakerIntent(server: configuration.server)) {
                Label("Quiet Down", systemImage: "speaker.wave.1.fill")
            }
        }
        .displayName("Quiet Down HomeSpeaker")
        .description("Halve a speaker's volume.")
    }
}
