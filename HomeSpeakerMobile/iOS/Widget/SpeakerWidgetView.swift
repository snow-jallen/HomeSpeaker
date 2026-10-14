import AppIntents
import SwiftUI
import WidgetKit

struct SpeakerWidgetView: View {
    @Environment(\.widgetFamily) private var family
    let entry: SpeakerEntry

    var body: some View {
        content
            .containerBackground(for: .widget) {
                background
            }
    }

    @ViewBuilder
    private var content: some View {
        switch family {
        case .accessoryCircular:
            circular
        case .accessoryInline:
            inline
        case .accessoryRectangular:
            rectangular
        case .systemMedium:
            medium
        default:
            small
        }
    }

    @ViewBuilder
    private var background: some View {
        switch family {
        case .systemSmall, .systemMedium:
            if let artwork = entry.artwork {
                ZStack {
                    Image(uiImage: artwork)
                        .resizable()
                        .scaledToFill()
                        .blur(radius: 24)
                    Color.black.opacity(0.45)
                }
            } else {
                LinearGradient(colors: [Color.accentColor.opacity(0.85), .black],
                               startPoint: .topLeading, endPoint: .bottomTrailing)
            }
        default:
            Color.clear
        }
    }

    // MARK: - Home Screen

    private var small: some View {
        VStack(alignment: .leading, spacing: 6) {
            HStack(alignment: .top) {
                artwork(size: 46)
                Spacer(minLength: 0)
                serverBadge
            }
            Spacer(minLength: 0)
            Text(entry.title)
                .font(.headline)
                .lineLimit(2)
            if !entry.artist.isEmpty {
                Text(entry.artist)
                    .font(.caption)
                    .foregroundStyle(.white.opacity(0.75))
                    .lineLimit(1)
            }
            if entry.content != .noServer {
                HStack(spacing: 18) {
                    playPauseButton
                    skipButton
                }
                .font(.title3)
            }
        }
        .foregroundStyle(.white)
        .frame(maxWidth: .infinity, maxHeight: .infinity, alignment: .leading)
    }

    private var medium: some View {
        HStack(spacing: 14) {
            artwork(size: 110)
            VStack(alignment: .leading, spacing: 4) {
                serverBadge
                Text(entry.title)
                    .font(.headline)
                    .lineLimit(2)
                if !entry.artist.isEmpty {
                    Text(entry.artist)
                        .font(.subheadline)
                        .foregroundStyle(.white.opacity(0.75))
                        .lineLimit(1)
                }
                if let interval = entry.songInterval {
                    ProgressView(timerInterval: interval, countsDown: false)
                        .labelsHidden()
                        .tint(.white)
                }
                Spacer(minLength: 0)
                if entry.content != .noServer {
                    HStack(spacing: 22) {
                        stopButton
                        playPauseButton
                        skipButton
                        quietButton
                    }
                    .font(.title3)
                }
            }
        }
        .foregroundStyle(.white)
        .frame(maxWidth: .infinity, maxHeight: .infinity, alignment: .leading)
    }

    // MARK: - Lock Screen

    private var rectangular: some View {
        VStack(alignment: .leading, spacing: 2) {
            HStack(spacing: 4) {
                Image(systemName: entry.isPlaying ? "hifispeaker.fill" : "hifispeaker")
                Text(entry.server?.name ?? "HomeSpeaker")
                    .lineLimit(1)
            }
            .font(.caption2)
            .widgetAccentable()
            Text(entry.title)
                .font(.headline)
                .lineLimit(1)
            if entry.content != .noServer {
                HStack(spacing: 16) {
                    playPauseButton
                    skipButton
                    Spacer(minLength: 0)
                }
                .font(.body)
            }
        }
        .frame(maxWidth: .infinity, alignment: .leading)
    }

    private var circular: some View {
        ZStack {
            AccessoryWidgetBackground()
            if entry.content == .noServer {
                Image(systemName: "hifispeaker.slash")
            } else {
                Button(intent: ToggleSpeakerPlaybackIntent(server: entry.server)) {
                    Image(systemName: entry.isPlaying ? "stop.fill" : "play.fill")
                        .font(.title3)
                }
                .buttonStyle(.plain)
            }
        }
        .widgetAccentable()
    }

    private var inline: some View {
        Label(entry.isPlaying ? entry.title : "\(entry.server?.name ?? "HomeSpeaker") idle",
              systemImage: "hifispeaker.fill")
    }

    // MARK: - Parts

    @ViewBuilder
    private func artwork(size: CGFloat) -> some View {
        Group {
            if let artwork = entry.artwork {
                Image(uiImage: artwork)
                    .resizable()
                    .scaledToFill()
            } else {
                Image(systemName: entry.isStream ? "dot.radiowaves.left.and.right" : "music.note")
                    .font(.system(size: size * 0.4))
                    .frame(maxWidth: .infinity, maxHeight: .infinity)
                    .background(.white.opacity(0.15))
            }
        }
        .frame(width: size, height: size)
        .clipShape(RoundedRectangle(cornerRadius: size * 0.14))
    }

    private var serverBadge: some View {
        Text(entry.server?.name ?? "HomeSpeaker")
            .font(.caption2.weight(.semibold))
            .foregroundStyle(.white.opacity(0.8))
            .lineLimit(1)
    }

    // Speakers stop rather than pause, so the toggle shows stop while playing.
    private var playPauseButton: some View {
        Button(intent: ToggleSpeakerPlaybackIntent(server: entry.server)) {
            Image(systemName: entry.isPlaying ? "stop.fill" : "play.fill")
        }
        .buttonStyle(.plain)
    }

    private var skipButton: some View {
        Button(intent: SkipSpeakerSongIntent(server: entry.server)) {
            Image(systemName: "forward.fill")
        }
        .buttonStyle(.plain)
    }

    private var stopButton: some View {
        Button(intent: StopSpeakerIntent(server: entry.server)) {
            Image(systemName: "stop.circle")
        }
        .buttonStyle(.plain)
    }

    private var quietButton: some View {
        Button(intent: QuietSpeakerIntent(server: entry.server)) {
            Image(systemName: "speaker.wave.1.fill")
        }
        .buttonStyle(.plain)
    }
}
