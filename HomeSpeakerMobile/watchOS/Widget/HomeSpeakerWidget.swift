import WidgetKit
import SwiftUI
import AppIntents

// MARK: - Timeline Entry

struct PlayerEntry: TimelineEntry {
    let date: Date
    let title: String
    let artist: String
    let isPlaying: Bool
    let hasServer: Bool
}

// MARK: - Timeline Provider

struct HomeSpeakerTimelineProvider: TimelineProvider {
    func placeholder(in context: Context) -> PlayerEntry {
        PlayerEntry(date: .now, title: "Song Title", artist: "Artist", isPlaying: true, hasServer: true)
    }

    func getSnapshot(in context: Context, completion: @escaping (PlayerEntry) -> Void) {
        Task { completion(await fetchEntry()) }
    }

    func getTimeline(in context: Context, completion: @escaping (Timeline<PlayerEntry>) -> Void) {
        Task {
            let entry = await fetchEntry()
            let refreshInterval = entry.isPlaying ? 5 : 30
            let next = Calendar.current.date(byAdding: .minute, value: refreshInterval, to: .now)!
            completion(Timeline(entries: [entry], policy: .after(next)))
        }
    }

    private func fetchEntry() async -> PlayerEntry {
        guard let api = widgetAPI() else {
            return PlayerEntry(date: .now, title: "No Server", artist: "", isPlaying: false, hasServer: false)
        }
        guard let status = try? await api.getPlayerStatus() else {
            return PlayerEntry(date: .now, title: "Not Playing", artist: "", isPlaying: false, hasServer: true)
        }
        let title = status.currentSong?.displayTitle ?? (status.stillPlaying ? "Streaming" : "Not Playing")
        let artist = status.currentSong?.displayArtist ?? ""
        return PlayerEntry(date: .now, title: title, artist: artist, isPlaying: status.isPlaying, hasServer: true)
    }
}

// MARK: - Widget View

struct HomeSpeakerWidgetView: View {
    let entry: PlayerEntry

    var body: some View {
        if !entry.hasServer {
            Label("Open HomeSpeaker to configure", systemImage: "hifispeaker.slash")
                .font(.caption2)
                .foregroundStyle(.secondary)
        } else {
            VStack(alignment: .leading, spacing: 3) {
                HStack(spacing: 4) {
                    Image(systemName: "hifispeaker.fill")
                        .font(.caption2)
                        .foregroundStyle(.secondary)
                    Text(entry.title)
                        .font(.caption.bold())
                        .lineLimit(1)
                }
                Text(entry.artist.isEmpty ? " " : entry.artist)
                    .font(.caption2)
                    .foregroundStyle(.secondary)
                    .lineLimit(1)
                HStack(spacing: 14) {
                    Button(intent: StopIntent()) {
                        Image(systemName: "stop.fill")
                    }
                    Button(intent: PlayPauseIntent()) {
                        Image(systemName: entry.isPlaying ? "pause.fill" : "play.fill")
                    }
                    Button(intent: SkipIntent()) {
                        Image(systemName: "forward.fill")
                    }
                    Spacer(minLength: 0)
                }
                .buttonStyle(.plain)
                .font(.caption2)
            }
            .frame(maxWidth: .infinity, alignment: .leading)
        }
    }
}

// MARK: - Widget + Bundle

struct HomeSpeakerWidget: Widget {
    let kind = "HomeSpeakerWidget"

    var body: some WidgetConfiguration {
        StaticConfiguration(kind: kind, provider: HomeSpeakerTimelineProvider()) { entry in
            HomeSpeakerWidgetView(entry: entry)
                .containerBackground(.black, for: .widget)
        }
        .configurationDisplayName("HomeSpeaker")
        .description("Playback controls for your HomeSpeaker server.")
        .supportedFamilies([.accessoryRectangular])
    }
}

@main
struct HomeSpeakerWidgetBundle: WidgetBundle {
    var body: some Widget {
        HomeSpeakerWidget()
    }
}
