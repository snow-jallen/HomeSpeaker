import ActivityKit
import AppIntents
import SwiftUI
import WidgetKit

struct SpeakerLiveActivityWidget: Widget {
    var body: some WidgetConfiguration {
        ActivityConfiguration(for: SpeakerActivityAttributes.self) { context in
            SpeakerLockScreenView(attributes: context.attributes, state: context.state)
                .activityBackgroundTint(Color.black.opacity(0.75))
                .activitySystemActionForegroundColor(.white)
        } dynamicIsland: { context in
            let state = context.state
            let server = context.attributes.server
            return DynamicIsland {
                DynamicIslandExpandedRegion(.leading) {
                    Label(context.attributes.serverName, systemImage: "hifispeaker.fill")
                        .font(.caption)
                        .lineLimit(1)
                }
                DynamicIslandExpandedRegion(.trailing) {
                    if let ends = state.sleepTimerEndsAt, ends > state.updatedAt {
                        Label {
                            Text(timerInterval: state.updatedAt...ends, countsDown: true)
                                .monospacedDigit()
                        } icon: {
                            Image(systemName: "moon.fill")
                        }
                        .font(.caption)
                        .foregroundStyle(.orange)
                    } else {
                        Label("\(state.volume)", systemImage: "speaker.wave.2.fill")
                            .font(.caption)
                    }
                }
                DynamicIslandExpandedRegion(.center) {
                    VStack(spacing: 2) {
                        Text(state.title)
                            .font(.headline)
                            .lineLimit(1)
                        if !state.artist.isEmpty {
                            Text(state.artist)
                                .font(.caption)
                                .foregroundStyle(.secondary)
                                .lineLimit(1)
                        }
                    }
                }
                DynamicIslandExpandedRegion(.bottom) {
                    SpeakerActivityControls(server: server, isPlaying: state.isPlaying)
                        .padding(.top, 4)
                }
            } compactLeading: {
                Image(systemName: "hifispeaker.fill")
                    .foregroundStyle(Color.accentColor)
            } compactTrailing: {
                if let ends = state.sleepTimerEndsAt, ends > state.updatedAt {
                    Text(timerInterval: state.updatedAt...ends, countsDown: true)
                        .monospacedDigit()
                        .font(.caption2)
                        .frame(maxWidth: 44)
                        .foregroundStyle(.orange)
                } else {
                    Image(systemName: state.isPlaying ? "waveform" : "stop.fill")
                }
            } minimal: {
                Image(systemName: state.isPlaying ? "hifispeaker.fill" : "hifispeaker")
            }
        }
    }
}

struct SpeakerLockScreenView: View {
    let attributes: SpeakerActivityAttributes
    let state: SpeakerActivityAttributes.ContentState

    var body: some View {
        VStack(alignment: .leading, spacing: 10) {
            HStack(spacing: 6) {
                Image(systemName: "hifispeaker.fill")
                Text(attributes.serverName)
                    .fontWeight(.semibold)
                Spacer()
                if let ends = state.sleepTimerEndsAt, ends > state.updatedAt {
                    Image(systemName: "moon.fill")
                    Text(timerInterval: state.updatedAt...ends, countsDown: true)
                        .monospacedDigit()
                        .frame(maxWidth: 60, alignment: .trailing)
                }
            }
            .font(.caption)
            .foregroundStyle(.white.opacity(0.8))

            HStack(spacing: 12) {
                Image(systemName: state.isStream ? "dot.radiowaves.left.and.right" : "music.note")
                    .font(.title2)
                    .frame(width: 44, height: 44)
                    .background(.white.opacity(0.15), in: RoundedRectangle(cornerRadius: 8))
                VStack(alignment: .leading, spacing: 2) {
                    Text(state.title)
                        .font(.headline)
                        .lineLimit(1)
                    if !state.artist.isEmpty {
                        Text(state.artist)
                            .font(.subheadline)
                            .foregroundStyle(.white.opacity(0.7))
                            .lineLimit(1)
                    }
                }
                Spacer(minLength: 0)
            }

            SpeakerActivityControls(server: attributes.server, isPlaying: state.isPlaying)
        }
        .foregroundStyle(.white)
        .padding()
    }
}

struct SpeakerActivityControls: View {
    let server: ServerEntity
    let isPlaying: Bool

    var body: some View {
        HStack {
            Spacer()
            Button(intent: QuietSpeakerIntent(server: server)) {
                Image(systemName: "speaker.wave.1.fill")
            }
            Spacer()
            Button(intent: ToggleSpeakerPlaybackIntent(server: server)) {
                Image(systemName: isPlaying ? "stop.fill" : "play.fill")
                    .font(.title2)
            }
            Spacer()
            Button(intent: SkipSpeakerSongIntent(server: server)) {
                Image(systemName: "forward.fill")
            }
            Spacer()
        }
        .buttonStyle(.plain)
        .font(.title3)
        .foregroundStyle(.white)
    }
}
