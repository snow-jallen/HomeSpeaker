import SwiftUI

struct WatchPlaylistsView: View {
    @Environment(ConnectionStore.self) private var store
    @State private var playlists: [Playlist] = []
    @State private var isLoading = false
    @State private var actionMessage: String?

    var body: some View {
        NavigationStack {
            Group {
                if isLoading && playlists.isEmpty {
                    ProgressView()
                } else if playlists.isEmpty {
                    Text("No playlists")
                        .foregroundStyle(.secondary)
                } else {
                    List(playlists) { playlist in
                        Button {
                            Task {
                                try? await store.api?.playPlaylist(name: playlist.name)
                                flash("Playing \(playlist.name)")
                            }
                        } label: {
                            VStack(alignment: .leading, spacing: 2) {
                                Text(playlist.name)
                                    .font(.caption)
                                    .lineLimit(1)
                                Text("\(playlist.songs.count) songs")
                                    .font(.caption2)
                                    .foregroundStyle(.secondary)
                            }
                        }
                    }
                }
            }
            .navigationTitle("Playlists")
        }
        .overlay(alignment: .bottom) {
            if let msg = actionMessage {
                Text(msg)
                    .font(.caption2)
                    .padding(.horizontal, 10)
                    .padding(.vertical, 4)
                    .background(.regularMaterial, in: Capsule())
                    .padding(.bottom, 4)
            }
        }
        .task { await load() }
    }

    private func load() async {
        guard let api = store.api else { return }
        isLoading = true
        playlists = (try? await api.getPlaylists()) ?? []
        isLoading = false
    }

    private func flash(_ msg: String) {
        withAnimation { actionMessage = msg }
        Task {
            try? await Task.sleep(for: .seconds(1.5))
            withAnimation { actionMessage = nil }
        }
    }
}
