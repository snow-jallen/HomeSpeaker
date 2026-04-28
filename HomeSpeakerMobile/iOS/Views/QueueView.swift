import SwiftUI

struct QueueView: View {
    @Environment(ConnectionStore.self) private var store
    @State private var queue: [Song] = []
    @State private var isLoading = false
    @State private var showSavePlaylist = false
    @State private var newPlaylistName = ""

    var body: some View {
        NavigationStack {
            Group {
                if isLoading && queue.isEmpty {
                    ProgressView("Loading queue…")
                        .frame(maxWidth: .infinity, maxHeight: .infinity)
                } else if queue.isEmpty {
                    ContentUnavailableView {
                        Label("Queue is Empty", systemImage: "list.number")
                    } description: {
                        Text("Add songs from the Library tab.")
                    }
                } else {
                    queueList
                }
            }
            .navigationTitle("Queue")
            .toolbar {
                if !queue.isEmpty {
                    ToolbarItemGroup(placement: .topBarTrailing) {
                        Button {
                            Task { await shuffle() }
                        } label: {
                            Image(systemName: "shuffle")
                        }

                        Button {
                            showSavePlaylist = true
                        } label: {
                            Image(systemName: "square.and.arrow.down")
                        }

                        Button(role: .destructive) {
                            Task { await clearQueue() }
                        } label: {
                            Image(systemName: "trash")
                        }
                    }
                }
            }
            .refreshable { await loadQueue() }
            .task { await loadQueue() }
            .alert("Save as Playlist", isPresented: $showSavePlaylist) {
                TextField("Playlist Name", text: $newPlaylistName)
                Button("Save") { Task { await saveAsPlaylist() } }
                Button("Cancel", role: .cancel) {}
            }
        }
    }

    private var queueList: some View {
        List {
            ForEach(Array(queue.enumerated()), id: \.element.id) { index, song in
                HStack {
                    Text("\(index + 1)")
                        .font(.caption.monospacedDigit())
                        .foregroundStyle(.tertiary)
                        .frame(width: 24, alignment: .trailing)
                    VStack(alignment: .leading, spacing: 2) {
                        Text(song.displayTitle)
                            .font(.body)
                            .lineLimit(1)
                        Text(song.displayArtist)
                            .font(.caption)
                            .foregroundStyle(.secondary)
                            .lineLimit(1)
                    }
                }
                .swipeActions(edge: .trailing) {
                    Button(role: .destructive) {
                        Task { await removeSong(at: index) }
                    } label: {
                        Label("Remove", systemImage: "minus.circle")
                    }
                }
            }
            .onMove { from, to in
                queue.move(fromOffsets: from, toOffset: to)
                Task { await reorderQueue() }
            }
        }
        .environment(\.editMode, .constant(.active))
    }

    private func loadQueue() async {
        guard let api = store.api else { return }
        isLoading = true
        defer { isLoading = false }
        queue = (try? await api.getQueue()) ?? []
    }

    private func shuffle() async {
        guard let api = store.api else { return }
        try? await api.shuffleQueue()
        await loadQueue()
    }

    private func clearQueue() async {
        guard let api = store.api else { return }
        try? await api.clearQueue()
        queue = []
    }

    private func removeSong(at index: Int) async {
        guard index < queue.count, let api = store.api else { return }
        let song = queue[index]
        queue.remove(at: index)
        if let path = song.path {
            try? await api.removeSongFromPlaylist(playlistName: "", songPath: path)
        }
        await reorderQueue()
    }

    private func reorderQueue() async {
        guard let api = store.api else { return }
        let paths = queue.compactMap(\.path)
        try? await api.updateQueue(songPaths: paths)
    }

    private func saveAsPlaylist() async {
        guard !newPlaylistName.isEmpty, let api = store.api else { return }
        let paths = queue.compactMap(\.path)
        for path in paths {
            try? await api.addSongToPlaylist(playlistName: newPlaylistName, songPath: path)
        }
        newPlaylistName = ""
    }
}
