import SwiftUI

struct WatchAIPlaylistsView: View {
    @Environment(ConnectionStore.self) private var store
    @State private var playlists: [AiPlaylistSummaryDto] = []
    @State private var isLoading = false
    @State private var actionMessage: String?

    var body: some View {
        NavigationStack {
            Group {
                if isLoading && playlists.isEmpty {
                    ProgressView()
                } else if playlists.isEmpty {
                    Text("No AI playlists")
                        .foregroundStyle(.secondary)
                } else {
                    List(playlists) { playlist in
                        Button {
                            Task {
                                try? await store.api?.playAiPlaylist(genreKey: playlist.genreKey)
                                flash("Playing \(playlist.displayName)")
                            }
                        } label: {
                            VStack(alignment: .leading, spacing: 2) {
                                Text(playlist.displayName)
                                    .font(.caption)
                                    .lineLimit(1)
                                Text("\(playlist.songCount) songs")
                                    .font(.caption2)
                                    .foregroundStyle(.secondary)
                            }
                        }
                    }
                }
            }
            .navigationTitle("AI Playlists")
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
        playlists = (try? await api.getAiPlaylists()) ?? []
        playlists.sort { $0.sortOrder < $1.sortOrder }
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
