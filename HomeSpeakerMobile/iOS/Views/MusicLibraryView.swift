import SwiftUI

struct MusicLibraryView: View {
    @Environment(ConnectionStore.self) private var store
    @State private var songs: [Song] = []
    @State private var searchText = ""
    @State private var isLoading = false
    @State private var error: String?
    @State private var actionMessage: String?
    @State private var expandedArtists: Set<String> = []
    @State private var expandedAlbums: Set<String> = []

    var filteredSongs: [Song] {
        if searchText.isEmpty { return songs }
        return songs.filter {
            $0.displayTitle.localizedCaseInsensitiveContains(searchText) ||
            $0.displayArtist.localizedCaseInsensitiveContains(searchText) ||
            $0.displayAlbum.localizedCaseInsensitiveContains(searchText)
        }
    }

    var groupedByArtistAndAlbum: [(artist: String, albums: [(album: String, songs: [Song])])] {
        let byArtist = Dictionary(grouping: filteredSongs, by: \.displayArtist)
        return byArtist
            .map { artist, artistSongs in
                let byAlbum = Dictionary(grouping: artistSongs, by: \.displayAlbum)
                let albums = byAlbum
                    .map { album, albumSongs in
                        (album: album, songs: albumSongs.sorted { $0.displayTitle < $1.displayTitle })
                    }
                    .sorted { $0.album < $1.album }
                return (artist: artist, albums: albums)
            }
            .sorted { $0.artist < $1.artist }
    }

    var body: some View {
        NavigationStack {
            Group {
                if isLoading && songs.isEmpty {
                    ProgressView("Loading library…")
                        .frame(maxWidth: .infinity, maxHeight: .infinity)
                } else if let error {
                    ContentUnavailableView {
                        Label("Load Failed", systemImage: "exclamationmark.triangle")
                    } description: {
                        Text(error)
                    }
                } else if songs.isEmpty {
                    ContentUnavailableView {
                        Label("No Songs", systemImage: "music.note")
                    } description: {
                        Text("No songs found in the library.")
                    }
                } else {
                    songList
                }
            }
            .navigationTitle("Library")
            .searchable(text: $searchText, prompt: "Search songs, artists, albums")
            .onChange(of: searchText) { _, newValue in
                if !newValue.isEmpty {
                    let tree = groupedByArtistAndAlbum
                    expandedArtists = Set(tree.map(\.artist))
                    expandedAlbums = Set(tree.flatMap { artistEntry in
                        artistEntry.albums.map { "\($0.album)|\(artistEntry.artist)" }
                    })
                }
            }
            .refreshable { await loadSongs() }
            .overlay(alignment: .bottom) {
                if let msg = actionMessage {
                    Text(msg)
                        .padding(.horizontal, 16)
                        .padding(.vertical, 8)
                        .background(.regularMaterial, in: Capsule())
                        .padding(.bottom, 8)
                        .transition(.move(edge: .bottom).combined(with: .opacity))
                }
            }
            .task { await loadSongs() }
        }
    }

    private var songList: some View {
        List {
            ForEach(groupedByArtistAndAlbum, id: \.artist) { artistEntry in
                DisclosureGroup(
                    isExpanded: Binding(
                        get: { expandedArtists.contains(artistEntry.artist) },
                        set: { expanded in
                            if expanded { expandedArtists.insert(artistEntry.artist) }
                            else { expandedArtists.remove(artistEntry.artist) }
                        }
                    )
                ) {
                    ForEach(artistEntry.albums, id: \.album) { albumEntry in
                        let albumKey = "\(albumEntry.album)|\(artistEntry.artist)"
                        DisclosureGroup(
                            isExpanded: Binding(
                                get: { expandedAlbums.contains(albumKey) },
                                set: { expanded in
                                    if expanded { expandedAlbums.insert(albumKey) }
                                    else { expandedAlbums.remove(albumKey) }
                                }
                            )
                        ) {
                            ForEach(albumEntry.songs) { song in
                                SongRow(song: song) { action in
                                    await handleAction(action, song: song)
                                }
                            }
                        } label: {
                            Text(albumEntry.album)
                                .font(.subheadline)
                                .foregroundStyle(.secondary)
                        }
                    }
                } label: {
                    Text(artistEntry.artist)
                        .font(.headline)
                }
            }
        }
        .listStyle(.insetGrouped)
        .animation(.default, value: searchText)
    }

    private func handleAction(_ action: SongAction, song: Song) async {
        guard let api = store.api else { return }
        do {
            switch action {
            case .play:
                try await api.playSong(song.songId)
                showMessage("Playing: \(song.displayTitle)")
            case .enqueue:
                try await api.enqueueSong(song.songId)
                showMessage("Added to queue")
            }
        } catch {
            showMessage("Error: \(error.localizedDescription)")
        }
    }

    private func loadSongs() async {
        guard let api = store.api else { return }
        isLoading = true
        defer { isLoading = false }
        do {
            songs = try await api.getSongs()
        } catch {
            self.error = error.localizedDescription
        }
    }

    private func showMessage(_ msg: String) {
        withAnimation { actionMessage = msg }
        Task {
            try? await Task.sleep(for: .seconds(2))
            withAnimation { actionMessage = nil }
        }
    }
}

enum SongAction { case play, enqueue }

struct SongRow: View {
    let song: Song
    let onAction: (SongAction) async -> Void

    var body: some View {
        HStack {
            VStack(alignment: .leading, spacing: 2) {
                Text(song.displayTitle)
                    .font(.body)
                    .lineLimit(1)
                Text(song.displayAlbum)
                    .font(.caption)
                    .foregroundStyle(.secondary)
                    .lineLimit(1)
            }
            Spacer()
            HStack(spacing: 16) {
                Button {
                    Task { await onAction(.enqueue) }
                } label: {
                    Image(systemName: "text.badge.plus")
                        .foregroundStyle(Color.accentColor)
                }
                .buttonStyle(.borderless)

                Button {
                    Task { await onAction(.play) }
                } label: {
                    Image(systemName: "play.fill")
                        .foregroundStyle(Color.accentColor)
                }
                .buttonStyle(.borderless)
            }
        }
        .contentShape(Rectangle())
    }
}
