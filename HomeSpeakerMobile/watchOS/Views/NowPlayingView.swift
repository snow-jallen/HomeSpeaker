import SwiftUI

struct WatchNowPlayingView: View {
    @Environment(ConnectionStore.self) private var store
    @State private var status: PlayerStatus?
    @State private var volume: Int = 50

    var body: some View {
        VStack(spacing: 8) {
            songInfo
            progressBar
            transportControls
        }
        .padding(.horizontal, 4)
        .navigationTitle("Now Playing")
        .task {
            while !Task.isCancelled {
                await refresh()
                try? await Task.sleep(for: .seconds(3))
            }
        }
    }

    private var songInfo: some View {
        VStack(spacing: 4) {
            if let song = status?.currentSong {
                Text(song.displayTitle)
                    .font(.headline)
                    .multilineTextAlignment(.center)
                    .lineLimit(2)
                Text(song.displayArtist)
                    .font(.caption)
                    .foregroundStyle(.secondary)
                    .lineLimit(1)
            } else if status?.stillPlaying == true {
                Text("Streaming")
                    .font(.headline)
            } else {
                Text("Not Playing")
                    .font(.caption)
                    .foregroundStyle(.secondary)
            }
        }
    }

    private var progressBar: some View {
        ProgressView(value: status?.percentComplete ?? 0)
            .progressViewStyle(.linear)
            .tint(.accentColor)
    }

    private var transportControls: some View {
        HStack(spacing: 16) {
            Button {
                Task { await control(.stop) }
            } label: {
                Image(systemName: "stop.fill")
                    .font(.title3)
            }
            .buttonStyle(.borderedProminent)
            .tint(.red.opacity(0.8))

            Button {
                Task {
                    if status?.isPlaying == true {
                        await control(.pause)
                    } else {
                        await control(.play)
                    }
                }
            } label: {
                Image(systemName: status?.isPlaying == true ? "pause.fill" : "play.fill")
                    .font(.title2)
            }
            .buttonStyle(.borderedProminent)

            Button {
                Task { await control(.skip) }
            } label: {
                Image(systemName: "forward.fill")
                    .font(.title3)
            }
            .buttonStyle(.borderedProminent)
            .tint(.secondary)
        }
    }

    private enum Control { case play, pause, stop, skip }

    private func control(_ action: Control) async {
        guard let api = store.api else { return }
        switch action {
        case .play: try? await api.resume()
        case .pause: try? await api.stop()
        case .stop: try? await api.stop()
        case .skip: try? await api.skipToNext()
        }
        await refresh()
    }

    private func refresh() async {
        guard let api = store.api else { return }
        status = try? await api.getPlayerStatus()
        if let vol = status?.volume { volume = vol }
    }
}
