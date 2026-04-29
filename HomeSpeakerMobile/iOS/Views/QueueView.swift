import SwiftUI

private enum QueueTab { case speaker, device }

struct QueueView: View {
    @Environment(ConnectionStore.self) private var store
    @Environment(LocalPlayer.self) private var localPlayer
    @State private var serverQueue: [Song] = []
    @State private var isLoading = false
    @State private var showSavePlaylist = false
    @State private var newPlaylistName = ""
    @State private var selectedTab: QueueTab = .speaker

    private var showTabs: Bool { !localPlayer.songs.isEmpty }

    var body: some View {
        NavigationStack {
            Group {
                if showTabs {
                    tabbedQueue
                } else {
                    serverQueueContent
                }
            }
            .navigationTitle(showTabs ? "" : "Queue")
            .toolbar { toolbarContent }
            .refreshable { await loadServerQueue() }
            .task { await loadServerQueue() }
            .alert("Save as Playlist", isPresented: $showSavePlaylist) {
                TextField("Playlist Name", text: $newPlaylistName)
                Button("Save") { Task { await saveAsPlaylist() } }
                Button("Cancel", role: .cancel) {}
            }
        }
    }

    // MARK: - Tabbed layout (shown when local queue has content)

    private var tabbedQueue: some View {
        VStack(spacing: 0) {
            Picker("Queue", selection: $selectedTab) {
                Label("Speaker", systemImage: "hifispeaker.fill").tag(QueueTab.speaker)
                Label("This iPhone", systemImage: "iphone").tag(QueueTab.device)
            }
            .pickerStyle(.segmented)
            .padding(.horizontal)
            .padding(.vertical, 8)

            Divider()

            if selectedTab == .speaker {
                serverQueueContent
            } else {
                localQueueContent
            }
        }
    }

    // MARK: - Server queue

    private var serverQueueContent: some View {
        Group {
            if isLoading && serverQueue.isEmpty {
                ProgressView("Loading queue…")
                    .frame(maxWidth: .infinity, maxHeight: .infinity)
            } else if serverQueue.isEmpty {
                ContentUnavailableView {
                    Label("Queue is Empty", systemImage: "list.number")
                } description: {
                    Text("Add songs from the Library tab.")
                }
            } else {
                serverQueueList
            }
        }
    }

    private var serverQueueList: some View {
        List {
            ForEach(Array(serverQueue.enumerated()), id: \.element.id) { index, song in
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
                        Task { await removeServerSong(at: index) }
                    } label: {
                        Label("Remove", systemImage: "minus.circle")
                    }
                }
            }
            .onMove { from, to in
                serverQueue.move(fromOffsets: from, toOffset: to)
                Task { await reorderServerQueue() }
            }
        }
        .environment(\.editMode, .constant(.active))
    }

    // MARK: - Local queue

    private var localQueueContent: some View {
        Group {
            if localPlayer.songs.isEmpty {
                ContentUnavailableView {
                    Label("Queue is Empty", systemImage: "iphone")
                } description: {
                    Text("Add songs from the Library or Playlists tab while set to This iPhone.")
                }
            } else {
                localQueueList
            }
        }
    }

    private var localQueueList: some View {
        List {
            ForEach(Array(localPlayer.songs.enumerated()), id: \.offset) { index, song in
                HStack {
                    if index == localPlayer.currentIndex {
                        Image(systemName: localPlayer.isPlaying ? "waveform" : "pause.fill")
                            .foregroundStyle(Color.accentColor)
                            .font(.caption)
                            .frame(width: 24, alignment: .center)
                    } else {
                        Text("\(index + 1)")
                            .font(.caption.monospacedDigit())
                            .foregroundStyle(.tertiary)
                            .frame(width: 24, alignment: .trailing)
                    }
                    VStack(alignment: .leading, spacing: 2) {
                        Text(song.displayTitle)
                            .font(.body)
                            .lineLimit(1)
                            .fontWeight(index == localPlayer.currentIndex ? .semibold : .regular)
                        Text(song.displayArtist)
                            .font(.caption)
                            .foregroundStyle(.secondary)
                            .lineLimit(1)
                    }
                }
                .swipeActions(edge: .trailing) {
                    Button(role: .destructive) {
                        localPlayer.remove(at: index)
                    } label: {
                        Label("Remove", systemImage: "minus.circle")
                    }
                }
            }
        }
    }

    // MARK: - Toolbar

    @ToolbarContentBuilder
    private var toolbarContent: some ToolbarContent {
        ToolbarItemGroup(placement: .topBarTrailing) {
            if !showTabs || selectedTab == .speaker {
                if !serverQueue.isEmpty {
                    Button {
                        Task { await shuffleServerQueue() }
                    } label: {
                        Image(systemName: "shuffle")
                    }

                    Button {
                        showSavePlaylist = true
                    } label: {
                        Image(systemName: "square.and.arrow.down")
                    }

                    Button(role: .destructive) {
                        Task { await clearServerQueue() }
                    } label: {
                        Image(systemName: "trash")
                    }
                }
            } else {
                if !localPlayer.songs.isEmpty {
                    Button(role: .destructive) {
                        localPlayer.clearQueue()
                    } label: {
                        Image(systemName: "trash")
                    }
                }
            }
        }
    }

    // MARK: - Server queue actions

    private func loadServerQueue() async {
        guard let api = store.api else { return }
        isLoading = true
        defer { isLoading = false }
        serverQueue = (try? await api.getQueue()) ?? []
    }

    private func shuffleServerQueue() async {
        guard let api = store.api else { return }
        try? await api.shuffleQueue()
        await loadServerQueue()
    }

    private func clearServerQueue() async {
        guard let api = store.api else { return }
        try? await api.clearQueue()
        serverQueue = []
    }

    private func removeServerSong(at index: Int) async {
        guard index < serverQueue.count, let api = store.api else { return }
        let song = serverQueue[index]
        serverQueue.remove(at: index)
        if let path = song.path {
            try? await api.removeSongFromPlaylist(playlistName: "", songPath: path)
        }
        await reorderServerQueue()
    }

    private func reorderServerQueue() async {
        guard let api = store.api else { return }
        let paths = serverQueue.compactMap(\.path)
        try? await api.updateQueue(songPaths: paths)
    }

    private func saveAsPlaylist() async {
        guard !newPlaylistName.isEmpty, let api = store.api else { return }
        let paths = serverQueue.compactMap(\.path)
        for path in paths {
            try? await api.addSongToPlaylist(playlistName: newPlaylistName, songPath: path)
        }
        newPlaylistName = ""
    }
}
