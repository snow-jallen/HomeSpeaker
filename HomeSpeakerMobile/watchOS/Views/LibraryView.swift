import SwiftUI

struct WatchLibraryView: View {
    @Environment(ConnectionStore.self) private var store
    @State private var songs: [Song] = []
    @State private var isLoading = false

    var groupedByArtist: [(artist: String, songs: [Song])] {
        let grouped = Dictionary(grouping: songs, by: \.displayArtist)
        return grouped
            .map { (artist: $0.key, songs: $0.value.sorted { $0.displayTitle < $1.displayTitle }) }
            .sorted { $0.artist < $1.artist }
    }

    var body: some View {
        NavigationStack {
            Group {
                if isLoading && songs.isEmpty {
                    ProgressView()
                } else if songs.isEmpty {
                    Text("No songs")
                        .foregroundStyle(.secondary)
                } else {
                    List(groupedByArtist, id: \.artist) { entry in
                        NavigationLink(destination: WatchArtistSongsView(artist: entry.artist, songs: entry.songs)) {
                            Text(entry.artist)
                                .font(.caption)
                                .lineLimit(1)
                        }
                    }
                }
            }
            .navigationTitle("Library")
        }
        .task { await load() }
    }

    private func load() async {
        guard let api = store.api else { return }
        isLoading = true
        songs = (try? await api.getSongs()) ?? []
        isLoading = false
    }
}

struct WatchArtistSongsView: View {
    @Environment(ConnectionStore.self) private var store
    let artist: String
    let songs: [Song]
    @State private var actionMessage: String?

    var body: some View {
        List(songs) { song in
            Button {
                Task {
                    try? await store.api?.playSong(song.songId)
                    flash("Playing")
                }
            } label: {
                VStack(alignment: .leading, spacing: 2) {
                    Text(song.displayTitle)
                        .font(.caption)
                        .lineLimit(1)
                    Text(song.displayAlbum)
                        .font(.caption2)
                        .foregroundStyle(.secondary)
                        .lineLimit(1)
                }
            }
            .swipeActions(edge: .trailing) {
                Button {
                    Task {
                        try? await store.api?.enqueueSong(song.songId)
                        flash("Queued")
                    }
                } label: {
                    Image(systemName: "text.badge.plus")
                }
                .tint(.blue)
            }
        }
        .navigationTitle(artist)
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
    }

    private func flash(_ msg: String) {
        withAnimation { actionMessage = msg }
        Task {
            try? await Task.sleep(for: .seconds(1.5))
            withAnimation { actionMessage = nil }
        }
    }
}
