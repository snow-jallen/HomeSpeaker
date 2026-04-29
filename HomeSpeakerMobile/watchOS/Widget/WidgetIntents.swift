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
    static let title: LocalizedStringResource = "Skip in HomeSpeaker"
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

func widgetAPI() -> APIClient? {
    guard let defaults = UserDefaults(suiteName: "group.com.homespeaker"),
          let data = defaults.data(forKey: "hs_connections"),
          let connections = try? JSONDecoder().decode([ServerConnection].self, from: data) else { return nil }
    let selectedId = defaults.string(forKey: "hs_selectedId")
    let selected = connections.first(where: { $0.id.uuidString == selectedId }) ?? connections.first
    return selected.map { APIClient(baseURL: $0.baseURL) }
}
