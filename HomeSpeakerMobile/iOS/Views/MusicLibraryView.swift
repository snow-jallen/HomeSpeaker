import SwiftUI

struct MusicLibraryView: View {
    @Environment(ConnectionStore.self) private var store
    @State private var songs: [Song] = []
    @State private var searchText = ""
    @State private var isLoading = false
    @State private var error: String?
    @State private var actionMessage: String?

    var filteredSongs: [Song] {
        if searchText.isEmpty { return songs }
        return songs.filter {
            $0.displayTitle.localizedCaseInsensitiveContains(searchText) ||
            $0.displayArtist.localizedCaseInsensitiveContains(searchText) ||
            $0.displayAlbum.localizedCaseInsensitiveContains(searchText)
        }
    }

    var groupedByArtist: [(String, [Song])] {
        let grouped = Dictionary(grouping: filteredSongs, by: \.displayArtist)
        return grouped.sorted { $0.key < $1.key }
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
            ForEach(groupedByArtist, id: \.0) { artist, artistSongs in
                Section(header: Text(artist).bold()) {
                    ForEach(artistSongs) { song in
                        SongRow(song: song) { action in
                            await handleAction(action, song: song)
                        }
                    }
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
