import AppIntents
import SwiftUI
import WidgetKit

// MARK: - Configuration

struct SpeakerWidgetConfigurationIntent: WidgetConfigurationIntent {
    static let title: LocalizedStringResource = "Choose Speaker"
    static let description = IntentDescription("Pick which HomeSpeaker this widget shows. Leave it empty to follow the speaker selected in the app.")

    @Parameter(title: "Speaker")
    var server: ServerEntity?
}

// MARK: - Timeline entry

struct SpeakerEntry: TimelineEntry {
    enum Content {
        case noServer
        case unreachable
        case status
    }

    let date: Date
    let content: Content
    let server: ServerEntity?
    let title: String
    let artist: String
    let isPlaying: Bool
    let isStream: Bool
    let artwork: UIImage?
    /// When the current song started and will end, for a self-updating progress bar.
    let songInterval: ClosedRange<Date>?

    static let placeholder = SpeakerEntry(
        date: .now, content: .status, server: ServerEntity(id: "", name: "Kitchen"),
        title: "Song Title", artist: "Artist", isPlaying: true, isStream: false,
        artwork: nil, songInterval: nil)

    static func noServer() -> SpeakerEntry {
        SpeakerEntry(date: .now, content: .noServer, server: nil, title: "No Speaker",
                     artist: "", isPlaying: false, isStream: false, artwork: nil, songInterval: nil)
    }
}

// MARK: - Timeline provider

struct SpeakerTimelineProvider: AppIntentTimelineProvider {
    func placeholder(in context: Context) -> SpeakerEntry {
        .placeholder
    }

    func snapshot(for configuration: SpeakerWidgetConfigurationIntent, in context: Context) async -> SpeakerEntry {
        context.isPreview ? .placeholder : await entry(for: configuration, family: context.family)
    }

    func timeline(for configuration: SpeakerWidgetConfigurationIntent, in context: Context) async -> Timeline<SpeakerEntry> {
        let entry = await entry(for: configuration, family: context.family)
        // Refresh just after the song ends so the next title shows up; iOS
        // still rations widget reloads, so this is best effort.
        let next: Date
        if entry.isPlaying, let end = entry.songInterval?.upperBound, end > .now {
            next = min(end.addingTimeInterval(3), .now.addingTimeInterval(15 * 60))
        } else if entry.isPlaying {
            next = .now.addingTimeInterval(10 * 60)
        } else {
            next = .now.addingTimeInterval(30 * 60)
        }
        return Timeline(entries: [entry], policy: .after(next))
    }

    private func entry(for configuration: SpeakerWidgetConfigurationIntent, family: WidgetFamily) async -> SpeakerEntry {
        guard let connection = try? resolveServer(configuration.server) else {
            return .noServer()
        }
        let server = ServerEntity(connection: connection)
        let api = APIClient.client(for: connection)
        guard let status = try? await api.getPlayerStatus() else {
            return SpeakerEntry(date: .now, content: .unreachable, server: server, title: "Can't Reach Speaker",
                                artist: connection.name, isPlaying: false, isStream: false,
                                artwork: nil, songInterval: nil)
        }

        var artwork: UIImage?
        let showsArt = family == .systemSmall || family == .systemMedium || family == .systemLarge
        if showsArt, let song = status.currentSong,
           let data = await api.data(at: api.albumArtURL(songId: song.songId)) {
            artwork = UIImage(data: data)?.preparingThumbnail(of: CGSize(width: 240, height: 240))
        }

        var interval: ClosedRange<Date>?
        let elapsed = status.elapsedSeconds
        let remaining = status.remainingSeconds
        if status.isPlaying, status.currentSong != nil, remaining > 0 {
            let now = Date.now
            interval = now.addingTimeInterval(-elapsed)...now.addingTimeInterval(remaining)
        }

        return SpeakerEntry(date: .now, content: .status, server: server,
                            title: status.displayTitle, artist: status.displaySubtitle,
                            isPlaying: status.isPlaying, isStream: status.isStream ?? false,
                            artwork: artwork, songInterval: interval)
    }
}

// MARK: - Widget

struct SpeakerWidget: Widget {
    static let kind = "HomeSpeakerSpeakerWidget"

    var body: some WidgetConfiguration {
        AppIntentConfiguration(kind: Self.kind, intent: SpeakerWidgetConfigurationIntent.self,
                               provider: SpeakerTimelineProvider()) { entry in
            SpeakerWidgetView(entry: entry)
        }
        .configurationDisplayName("HomeSpeaker")
        .description("See what's playing and control a speaker.")
        .supportedFamilies([.systemSmall, .systemMedium, .accessoryRectangular, .accessoryCircular, .accessoryInline])
    }
}
