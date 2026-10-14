import AppIntents
import WidgetKit

struct PlayPauseIntent: AppIntent {
    static let title: LocalizedStringResource = "Play or Pause HomeSpeaker"
    static let isDiscoverable = false

    func perform() async throws -> some IntentResult {
        if let api = widgetAPI() {
            let status = try? await api.getPlayerStatus()
            if status?.isPlaying == true {
                try? await api.stop()
            } else {
                try? await api.resume()
            }
        }
        try? await Task.sleep(for: .milliseconds(500))
        WidgetCenter.shared.reloadAllTimelines()
        return .result()
    }
}

struct SkipIntent: AppIntent {
    static let title: LocalizedStringResource = "HomeSpeaker Next Song"
    static let isDiscoverable = false

    func perform() async throws -> some IntentResult {
        try? await widgetAPI()?.skipToNext()
        try? await Task.sleep(for: .milliseconds(500))
        WidgetCenter.shared.reloadAllTimelines()
        return .result()
    }
}

struct StopIntent: AppIntent {
    static let title: LocalizedStringResource = "Stop HomeSpeaker"
    static let isDiscoverable = false

    func perform() async throws -> some IntentResult {
        try? await widgetAPI()?.stop()
        try? await Task.sleep(for: .milliseconds(500))
        WidgetCenter.shared.reloadAllTimelines()
        return .result()
    }
}

struct QuietDownIntent: AppIntent {
    static let title: LocalizedStringResource = "Quiet Down HomeSpeaker"
    static let isDiscoverable = false

    func perform() async throws -> some IntentResult {
        if let api = widgetAPI(), let status = try? await api.getPlayerStatus() {
            try? await api.setVolume(status.quietDownVolume)
        }
        try? await Task.sleep(for: .milliseconds(500))
        WidgetCenter.shared.reloadAllTimelines()
        return .result()
    }
}

func widgetAPI() -> APIClient? {
    SharedConnections.selected().map(APIClient.client(for:))
}
