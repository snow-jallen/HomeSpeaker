import ActivityKit
import AppIntents
import WidgetKit

// Buttons in widgets, the Live Activity and Control Center. They're
// LiveActivityIntents so the system runs them in the app's process, where the
// Live Activity can be updated straight away.

struct ToggleSpeakerPlaybackIntent: LiveActivityIntent {
    static let title: LocalizedStringResource = "Play or Stop HomeSpeaker"
    static let isDiscoverable = false

    @Parameter(title: "Server")
    var server: ServerEntity?

    init() {}

    init(server: ServerEntity?) {
        self.server = server
    }

    func perform() async throws -> some IntentResult {
        let connection = try resolveServer(server)
        let api = APIClient.client(for: connection)
        if (try? await api.getPlayerStatus())?.isPlaying == true {
            try await api.stop()
        } else {
            try await api.resume()
        }
        await SpeakerSurfaces.refresh(after: connection)
        return .result()
    }
}

struct SkipSpeakerSongIntent: LiveActivityIntent {
    static let title: LocalizedStringResource = "Skip Song on HomeSpeaker"
    static let isDiscoverable = false

    @Parameter(title: "Server")
    var server: ServerEntity?

    init() {}

    init(server: ServerEntity?) {
        self.server = server
    }

    func perform() async throws -> some IntentResult {
        let connection = try resolveServer(server)
        try await APIClient.client(for: connection).skipToNext()
        await SpeakerSurfaces.refresh(after: connection)
        return .result()
    }
}

struct StopSpeakerIntent: LiveActivityIntent {
    static let title: LocalizedStringResource = "Stop HomeSpeaker Playback"
    static let isDiscoverable = false

    @Parameter(title: "Server")
    var server: ServerEntity?

    init() {}

    init(server: ServerEntity?) {
        self.server = server
    }

    func perform() async throws -> some IntentResult {
        let connection = try resolveServer(server)
        try await APIClient.client(for: connection).stop()
        await SpeakerSurfaces.refresh(after: connection)
        return .result()
    }
}

struct QuietSpeakerIntent: LiveActivityIntent {
    static let title: LocalizedStringResource = "Quiet Down HomeSpeaker Playback"
    static let isDiscoverable = false

    @Parameter(title: "Server")
    var server: ServerEntity?

    init() {}

    init(server: ServerEntity?) {
        self.server = server
    }

    func perform() async throws -> some IntentResult {
        let connection = try resolveServer(server)
        let api = APIClient.client(for: connection)
        let status = try await api.getPlayerStatus()
        try await api.setVolume(status.quietDownVolume)
        await SpeakerSurfaces.refresh(after: connection)
        return .result()
    }
}

/// Brings widgets, controls and Live Activities up to date after a change.
enum SpeakerSurfaces {
    static func refresh(after connection: ServerConnection) async {
        // Give the player a moment to act on the command before reading it back.
        try? await Task.sleep(for: .milliseconds(500))
        if let status = try? await APIClient.client(for: connection).getPlayerStatus() {
            await updateLiveActivities(for: connection, status: status)
        }
        reloadWidgets()
    }

    static func reloadWidgets() {
        WidgetCenter.shared.reloadAllTimelines()
        if #available(iOS 18.0, *) {
            ControlCenter.shared.reloadAllControls()
        }
    }

    static func updateLiveActivities(for connection: ServerConnection, status: PlayerStatus) async {
        let state = SpeakerActivityAttributes.ContentState(status: status)
        for activity in Activity<SpeakerActivityAttributes>.activities
        where activity.attributes.serverId == connection.id.uuidString {
            await activity.update(ActivityContent(state: state, staleDate: .now.addingTimeInterval(30 * 60)))
        }
    }
}
