import SwiftUI

struct PlaylistsView: View {
    @Environment(ConnectionStore.self) private var store
    @Environment(LocalPlayer.self) private var localPlayer
    @State private var playlists: [Playlist] = []
    @State private var isLoading = false
    @State private var renamingPlaylist: Playlist?
    @State private var newName = ""
    @State private var actionMessage: String?

    var body: some View {
        NavigationStack {
            Group {
                if isLoading && playlists.isEmpty {
                    ProgressView("Loading playlists…")
                        .frame(maxWidth: .infinity, maxHeight: .infinity)
                } else if playlists.isEmpty {
                    ContentUnavailableView {
                        Label("No Playlists", systemImage: "music.quarternote.3")
                    } description: {
                        Text("Create playlists by saving the current queue.")
                    }
                } else {
                    playlistList
                }
            }
            .navigationTitle("Playlists")
            .toolbar {
                ToolbarItem(placement: .topBarTrailing) {
                    destinationToggle
                }
            }
            .refreshable { await load() }
            .task { await load() }
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
            .alert("Rename Playlist", isPresented: Binding(
                get: { renamingPlaylist != nil },
                set: { if !$0 { renamingPlaylist = nil } }
            )) {
                TextField("Name", text: $newName)
                Button("Rename") { Task { await rename() } }
                Button("Cancel", role: .cancel) { renamingPlaylist = nil }
            }
        }
    }

    private var destinationToggle: some View {
        Menu {
            Button {
                localPlayer.destination = .speaker
            } label: {
                Label("Speaker", systemImage: "hifispeaker.fill")
            }
            Button {
                localPlayer.destination = .device
            } label: {
                Label("This iPhone", systemImage: "iphone")
            }
        } label: {
            Image(systemName: localPlayer.destination == .speaker ? "hifispeaker.fill" : "iphone")
        }
    }

    private var playlistList: some View {
        List {
            ForEach(playlists) { playlist in
                NavigationLink {
                    PlaylistDetailView(playlist: playlist) {
                        Task { await load() }
                    }
                } label: {
                    HStack {
                        Image(systemName: "music.quarternote.3")
                            .foregroundStyle(Color.accentColor)
                            .frame(width: 32)
                        VStack(alignment: .leading) {
                            Text(playlist.name)
                                .font(.body)
                            Text("\(playlist.songs.count) songs")
                                .font(.caption)
                                .foregroundStyle(.secondary)
                        }
                        Spacer()
                        Button {
                            Task { await play(playlist: playlist) }
                        } label: {
                            Image(systemName: "play.fill")
                                .foregroundStyle(Color.accentColor)
                        }
                        .buttonStyle(.borderless)
                    }
                }
                .swipeActions(edge: .trailing) {
                    Button(role: .destructive) {
                        Task { await delete(playlist: playlist) }
                    } label: {
                        Label("Delete", systemImage: "trash")
                    }
                    Button {
                        newName = playlist.name
                        renamingPlaylist = playlist
                    } label: {
                        Label("Rename", systemImage: "pencil")
                    }
                    .tint(.orange)
                }
            }
        }
    }

    private func load() async {
        guard let api = store.api else { return }
        isLoading = true
        defer { isLoading = false }
        playlists = (try? await api.getPlaylists()) ?? []
    }

    private func play(playlist: Playlist) async {
        if localPlayer.destination == .device {
            guard let baseURL = store.selectedConnection?.baseURL else { return }
            localPlayer.play(songs: playlist.songs, from: 0, baseURL: baseURL)
            showMessage("Playing on this iPhone: \(playlist.name)")
            return
        }
        guard let api = store.api else { return }
        try? await api.playPlaylist(name: playlist.name)
        showMessage("Playing: \(playlist.name)")
    }

    private func delete(playlist: Playlist) async {
        guard let api = store.api else { return }
        try? await api.deletePlaylist(name: playlist.name)
        await load()
    }

    private func rename() async {
        guard let playlist = renamingPlaylist, !newName.isEmpty, let api = store.api else { return }
        try? await api.renamePlaylist(from: playlist.name, to: newName)
        renamingPlaylist = nil
        await load()
    }

    private func showMessage(_ msg: String) {
        withAnimation { actionMessage = msg }
        Task {
            try? await Task.sleep(for: .seconds(2))
            withAnimation { actionMessage = nil }
        }
    }
}

struct PlaylistDetailView: View {
    let playlist: Playlist
    let onChanged: () -> Void

    @Environment(ConnectionStore.self) private var store
    @Environment(LocalPlayer.self) private var localPlayer
    @State private var songs: [Song]
    @State private var isEditingOrder = false

    init(playlist: Playlist, onChanged: @escaping () -> Void) {
        self.playlist = playlist
        self.onChanged = onChanged
        _songs = State(initialValue: playlist.songs)
    }

    var body: some View {
        List {
            ForEach(songs) { song in
                HStack {
                    VStack(alignment: .leading, spacing: 2) {
                        Text(song.displayTitle).lineLimit(1)
                        Text(song.displayArtist)
                            .font(.caption)
                            .foregroundStyle(.secondary)
                            .lineLimit(1)
                    }
                    Spacer()
                    if !isEditingOrder {
                        Button {
                            Task { await enqueueSong(song) }
                        } label: {
                            Image(systemName: "text.badge.plus")
                                .foregroundStyle(Color.accentColor)
                        }
                        .buttonStyle(.borderless)
                    }
                }
                .swipeActions(edge: .trailing) {
                    Button(role: .destructive) {
                        Task { await removeSong(song) }
                    } label: {
                        Label("Remove", systemImage: "minus.circle")
                    }
                }
            }
            .onMove { from, to in
                songs.move(fromOffsets: from, toOffset: to)
                Task { await saveOrder() }
            }
        }
        .navigationTitle(playlist.name)
        .environment(\.editMode, .constant(isEditingOrder ? .active : .inactive))
        .toolbar {
            ToolbarItemGroup(placement: .topBarTrailing) {
                Button {
                    Task { await play() }
                } label: {
                    Image(systemName: "play.fill")
                }

                Button {
                    isEditingOrder.toggle()
                } label: {
                    Text(isEditingOrder ? "Done" : "Reorder")
                }
            }
        }
    }

    private func play() async {
        if localPlayer.destination == .device {
            guard let baseURL = store.selectedConnection?.baseURL else { return }
            localPlayer.play(songs: songs, from: 0, baseURL: baseURL)
            return
        }
        guard let api = store.api else { return }
        try? await api.playPlaylist(name: playlist.name)
    }

    private func enqueueSong(_ song: Song) async {
        if localPlayer.destination == .device {
            guard let baseURL = store.selectedConnection?.baseURL else { return }
            localPlayer.enqueue(songs: [song], baseURL: baseURL)
            return
        }
        guard let api = store.api else { return }
        try? await api.enqueueSong(song.songId)
    }

    private func removeSong(_ song: Song) async {
        guard let api = store.api, let path = song.path else { return }
        songs.removeAll { $0.id == song.id }
        try? await api.removeSongFromPlaylist(playlistName: playlist.name, songPath: path)
        onChanged()
    }

    private func saveOrder() async {
        guard let api = store.api else { return }
        let paths = songs.compactMap(\.path)
        try? await api.reorderPlaylist(name: playlist.name, songPaths: paths)
        onChanged()
    }
}
