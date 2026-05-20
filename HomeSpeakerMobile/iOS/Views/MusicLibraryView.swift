import SwiftUI
import PhotosUI

struct MusicLibraryView: View {
    @Environment(ConnectionStore.self) private var store
    @Environment(LocalPlayer.self) private var localPlayer
    @Environment(OfflineDownloadsStore.self) private var offlineDownloads
    @State private var songs: [Song] = []
    @State private var searchText = ""
    @State private var isLoading = false
    @State private var error: String?
    @State private var actionMessage: String?
    @State private var expandedArtists: Set<String> = []
    @State private var expandedAlbums: Set<String> = []
    @State private var editingSong: Song?
    @State private var editingAlbumArt: AlbumArtEditTarget?

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
            .toolbar {
                ToolbarItemGroup(placement: .topBarTrailing) {
                    NavigationLink {
                        OfflineDownloadsView()
                    } label: {
                        Image(systemName: "arrow.down.circle")
                    }
                    destinationToggle
                }
            }
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
            .task(id: store.selectedConnection?.id) {
                offlineDownloads.updateConnection(store.selectedConnection)
                await loadSongs()
            }
            .sheet(item: $editingSong) { song in
                if let api = store.api {
                    EditSongSheet(song: song, api: api) { updated in
                        if let idx = songs.firstIndex(where: { $0.songId == updated.songId }) {
                            songs[idx] = updated
                        }
                    }
                }
            }
            .sheet(item: $editingAlbumArt) { target in
                if let api = store.api {
                    EditAlbumArtSheet(target: target, api: api) {
                        await loadSongs()
                    }
                }
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

    private var songList: some View {
        List {
            ForEach(groupedByArtistAndAlbum, id: \.artist) { artistEntry in
                let artistSongs = artistEntry.albums.flatMap(\.songs)
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
                                SongRow(
                                    song: song,
                                    downloadStatus: offlineDownloads.status(for: song, connection: store.selectedConnection),
                                    onToggleOffline: {
                                        offlineDownloads.toggleTrack(song, connection: store.selectedConnection)
                                    }
                                ) { action in
                                    await handleAction(action, song: song)
                                }
                            }
                        } label: {
                            HStack(spacing: 8) {
                                if let firstSong = albumEntry.songs.first, let api = store.api {
                                    AsyncImage(url: api.albumArtURL(songId: firstSong.songId)) { image in
                                        image.resizable().scaledToFill()
                                    } placeholder: {
                                        RoundedRectangle(cornerRadius: 4).fill(Color(.systemGray5))
                                    }
                                    .frame(width: 32, height: 32)
                                    .clipShape(RoundedRectangle(cornerRadius: 4))
                                }
                                Text(albumEntry.album)
                                    .font(.subheadline)
                                    .foregroundStyle(.secondary)
                                Spacer()
                                OfflineCollectionButton(
                                    status: offlineDownloads.collectionStatus(
                                        for: albumEntry.songs,
                                        connection: store.selectedConnection
                                    )
                                ) {
                                    offlineDownloads.toggleAlbum(
                                        artist: artistEntry.artist,
                                        album: albumEntry.album,
                                        songs: albumEntry.songs,
                                        connection: store.selectedConnection
                                    )
                                }
                            }
                            .contextMenu {
                                Button {
                                    Task { await handleAlbumAction(.play, album: albumEntry.album) }
                                } label: {
                                    Label("Play Album", systemImage: "play.fill")
                                }
                                Button {
                                    Task { await handleAlbumAction(.enqueue, album: albumEntry.album) }
                                } label: {
                                    Label("Add Album to Queue", systemImage: "text.badge.plus")
                                }
                                Button {
                                    offlineDownloads.toggleAlbum(
                                        artist: artistEntry.artist,
                                        album: albumEntry.album,
                                        songs: albumEntry.songs,
                                        connection: store.selectedConnection
                                    )
                                } label: {
                                    Label(
                                        offlineDownloads.isAlbumSelected(
                                            artist: artistEntry.artist,
                                            album: albumEntry.album,
                                            connection: store.selectedConnection
                                        ) ? "Remove Album Download" : "Keep Album Offline",
                                        systemImage: "arrow.down.circle"
                                    )
                                }
                                if let firstSong = albumEntry.songs.first {
                                    Button {
                                        editingAlbumArt = AlbumArtEditTarget(
                                            album: albumEntry.album,
                                            representativeSongId: firstSong.songId
                                        )
                                    } label: {
                                        Label("Edit Album Art", systemImage: "photo")
                                    }
                                }
                            }
                        }
                    }
                } label: {
                    HStack {
                        Text(artistEntry.artist)
                            .font(.headline)
                        Spacer()
                        OfflineCollectionButton(
                            status: offlineDownloads.collectionStatus(
                                for: artistSongs,
                                connection: store.selectedConnection
                            )
                        ) {
                            offlineDownloads.toggleArtist(
                                artistEntry.artist,
                                songs: artistSongs,
                                connection: store.selectedConnection
                            )
                        }
                    }
                    .contextMenu {
                        Button {
                            Task { await handleArtistAction(.play, artist: artistEntry.artist) }
                        } label: {
                            Label("Play Artist", systemImage: "play.fill")
                        }
                        Button {
                            Task { await handleArtistAction(.enqueue, artist: artistEntry.artist) }
                        } label: {
                            Label("Add Artist to Queue", systemImage: "text.badge.plus")
                        }
                        Button {
                            offlineDownloads.toggleArtist(
                                artistEntry.artist,
                                songs: artistSongs,
                                connection: store.selectedConnection
                            )
                        } label: {
                            Label(
                                offlineDownloads.isArtistSelected(artistEntry.artist, connection: store.selectedConnection)
                                    ? "Remove Artist Download"
                                    : "Keep Artist Offline",
                                systemImage: "arrow.down.circle"
                            )
                        }
                    }
                }
            }
        }
        .listStyle(.insetGrouped)
        .animation(.default, value: searchText)
    }

    private func handleAction(_ action: SongAction, song: Song) async {
        if action == .editInfo {
            editingSong = song
            return
        }
        if localPlayer.destination == .device {
            guard let connection = store.selectedConnection else { return }
            switch action {
            case .play:
                localPlayer.play(songs: [song], from: 0, connection: connection)
                showMessage("Playing on this iPhone: \(song.displayTitle)")
            case .enqueue:
                localPlayer.enqueue(songs: [song], connection: connection)
                showMessage("Added to iPhone queue")
            case .editInfo: break
            }
            return
        }
        guard let api = store.api else { return }
        do {
            switch action {
            case .play:
                try await api.playSong(song.songId)
                showMessage("Playing: \(song.displayTitle)")
            case .enqueue:
                try await api.enqueueSong(song.songId)
                showMessage("Added to queue")
            case .editInfo: break
            }
        } catch {
            showMessage("Error: \(error.localizedDescription)")
        }
    }

    private func handleArtistAction(_ action: SongAction, artist: String) async {
        if localPlayer.destination == .device {
            guard let connection = store.selectedConnection else { return }
            let artistSongs = groupedByArtistAndAlbum
                .first(where: { $0.artist == artist })?
                .albums.flatMap(\.songs) ?? []
            switch action {
            case .play:
                localPlayer.play(songs: artistSongs, from: 0, connection: connection)
                showMessage("Playing on this iPhone: \(artist)")
            case .enqueue:
                localPlayer.enqueue(songs: artistSongs, connection: connection)
                showMessage("Added \(artist) to iPhone queue")
            case .editInfo: break
            }
            return
        }
        guard let api = store.api else { return }
        do {
            switch action {
            case .play:
                try await api.playArtist(artist)
                showMessage("Playing: \(artist)")
            case .enqueue:
                try await api.enqueueArtist(artist)
                showMessage("Added \(artist) to queue")
            case .editInfo: break
            }
        } catch {
            showMessage("Error: \(error.localizedDescription)")
        }
    }

    private func handleAlbumAction(_ action: SongAction, album: String) async {
        if localPlayer.destination == .device {
            guard let connection = store.selectedConnection else { return }
            let albumSongs = groupedByArtistAndAlbum
                .flatMap(\.albums)
                .first(where: { $0.album == album })?
                .songs ?? []
            switch action {
            case .play:
                localPlayer.play(songs: albumSongs, from: 0, connection: connection)
                showMessage("Playing on this iPhone: \(album)")
            case .enqueue:
                localPlayer.enqueue(songs: albumSongs, connection: connection)
                showMessage("Added \(album) to iPhone queue")
            case .editInfo: break
            }
            return
        }
        guard let api = store.api else { return }
        do {
            switch action {
            case .play:
                try await api.playAlbum(album)
                showMessage("Playing: \(album)")
            case .enqueue:
                try await api.enqueueAlbum(album)
                showMessage("Added \(album) to queue")
            case .editInfo: break
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
            let fetchedSongs = try await api.getSongs()
            error = nil
            songs = fetchedSongs
            offlineDownloads.updateLibrary(fetchedSongs, connection: store.selectedConnection)
        } catch {
            let offlineSongs = offlineDownloads.offlineLibrarySongs(connection: store.selectedConnection)
            if offlineSongs.isEmpty {
                songs = []
                self.error = error.localizedDescription
                return
            }

            songs = offlineSongs
            self.error = nil
            showMessage("Unable to load library. Showing downloaded media.")
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

enum SongAction { case play, enqueue, editInfo }

struct SongRow: View {
    let song: Song
    let downloadStatus: OfflineDownloadStatus
    let onToggleOffline: () -> Void
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
            Button {
                onToggleOffline()
            } label: {
                downloadLabel
            }
            .buttonStyle(.borderless)
            .disabled(downloadStatus == .downloading)
            Button {
                Task { await onAction(.play) }
            } label: {
                Image(systemName: "play.fill")
                    .foregroundStyle(Color.accentColor)
            }
            .buttonStyle(.borderless)
        }
        .contentShape(Rectangle())
        .contextMenu {
            Button {
                Task { await onAction(.enqueue) }
            } label: {
                Label("Add to Queue", systemImage: "text.badge.plus")
            }
            Button {
                Task { await onAction(.editInfo) }
            } label: {
                Label("Edit Song Info", systemImage: "pencil")
            }
            Button {
                onToggleOffline()
            } label: {
                Label(
                    downloadStatus == .notTracked ? "Keep Offline" : "Remove Offline Download",
                    systemImage: "arrow.down.circle"
                )
            }
        }
    }

    @ViewBuilder
    private var downloadLabel: some View {
        switch downloadStatus {
        case .downloading:
            ProgressView()
                .frame(width: 24, height: 24)
        case .downloaded:
            Image(systemName: "checkmark.circle.fill")
                .foregroundStyle(.green)
        case .queued:
            Image(systemName: "arrow.down.circle.fill")
                .foregroundStyle(.blue)
        case .pending:
            Image(systemName: "arrow.down.circle")
                .foregroundStyle(.secondary)
        case .failed:
            Image(systemName: "exclamationmark.circle.fill")
                .foregroundStyle(.red)
        case .notTracked:
            Image(systemName: "arrow.down.circle")
                .foregroundStyle(.secondary)
        }
    }
}

struct OfflineCollectionButton: View {
    let status: OfflineCollectionStatus
    let action: () -> Void

    var body: some View {
        Button(action: action) {
            label
                .frame(width: 24, height: 24)
        }
        .buttonStyle(.borderless)
        .accessibilityLabel(accessibilityText)
    }

    @ViewBuilder
    private var label: some View {
        switch status {
        case .downloading:
            ProgressView()
        case .downloaded:
            Image(systemName: "checkmark.circle.fill")
                .foregroundStyle(.green)
        case .queued:
            Image(systemName: "arrow.down.circle.fill")
                .foregroundStyle(.blue)
        case .pending:
            Image(systemName: "arrow.down.circle")
                .foregroundStyle(.secondary)
        case .failed:
            Image(systemName: "exclamationmark.circle.fill")
                .foregroundStyle(.red)
        case .notTracked:
            Image(systemName: "arrow.down.circle")
                .foregroundStyle(.secondary)
        }
    }

    private var accessibilityText: String {
        switch status {
        case .downloaded:
            return "Remove offline download"
        default:
            return "Keep offline"
        }
    }
}

struct EditSongSheet: View {
    let song: Song
    let api: APIClient
    let onSaved: (Song) -> Void

    @Environment(\.dismiss) private var dismiss
    @State private var name: String
    @State private var artist: String
    @State private var album: String
    @State private var isSaving = false
    @State private var error: String?

    init(song: Song, api: APIClient, onSaved: @escaping (Song) -> Void) {
        self.song = song
        self.api = api
        self.onSaved = onSaved
        _name = State(initialValue: song.name)
        _artist = State(initialValue: song.artist ?? "")
        _album = State(initialValue: song.album ?? "")
    }

    var body: some View {
        NavigationStack {
            Form {
                Section("Song Details") {
                    TextField("Title", text: $name)
                    TextField("Artist", text: $artist)
                    TextField("Album", text: $album)
                }
                if let error {
                    Section { Text(error).foregroundStyle(.red) }
                }
            }
            .navigationTitle("Edit Song")
            .navigationBarTitleDisplayMode(.inline)
            .toolbar {
                ToolbarItem(placement: .cancellationAction) {
                    Button("Cancel") { dismiss() }
                }
                ToolbarItem(placement: .confirmationAction) {
                    if isSaving {
                        ProgressView()
                    } else {
                        Button("Save") { Task { await save() } }
                            .disabled(name.isEmpty)
                    }
                }
            }
        }
    }

    private func save() async {
        isSaving = true
        do {
            try await api.updateSong(id: song.songId, name: name, artist: artist, album: album)
            let updated = Song(
                songId: song.songId,
                name: name,
                path: song.path,
                album: album.isEmpty ? nil : album,
                artist: artist.isEmpty ? nil : artist
            )
            onSaved(updated)
            dismiss()
        } catch {
            self.error = error.localizedDescription
        }
        isSaving = false
    }
}

struct EditAlbumArtSheet: View {
    let target: AlbumArtEditTarget
    let api: APIClient
    let onSaved: () async -> Void

    @Environment(\.dismiss) private var dismiss
    @State private var selectedItem: PhotosPickerItem?
    @State private var previewImage: Image?
    @State private var imageData: Data?
    @State private var isSaving = false
    @State private var error: String?

    var body: some View {
        NavigationStack {
            VStack(spacing: 20) {
                if let previewImage {
                    previewImage
                        .resizable()
                        .scaledToFit()
                        .frame(maxWidth: 300, maxHeight: 300)
                        .clipShape(RoundedRectangle(cornerRadius: 12))
                } else {
                    AsyncImage(url: api.albumArtURL(songId: target.representativeSongId)) { image in
                        image.resizable().scaledToFit()
                    } placeholder: {
                        RoundedRectangle(cornerRadius: 12)
                            .fill(Color(.systemGray5))
                            .overlay {
                                Image(systemName: "photo")
                                    .font(.largeTitle)
                                    .foregroundStyle(.secondary)
                            }
                    }
                    .frame(maxWidth: 300, maxHeight: 300)
                    .clipShape(RoundedRectangle(cornerRadius: 12))
                }

                PhotosPicker(selection: $selectedItem, matching: .images) {
                    Label("Choose Photo", systemImage: "photo.badge.plus")
                }
                .onChange(of: selectedItem) { _, item in
                    Task { await loadImage(from: item) }
                }

                if let error {
                    Text(error).foregroundStyle(.red).font(.caption)
                }

                Spacer()
            }
            .padding()
            .navigationTitle(target.album)
            .navigationBarTitleDisplayMode(.inline)
            .toolbar {
                ToolbarItem(placement: .cancellationAction) {
                    Button("Cancel") { dismiss() }
                }
                ToolbarItem(placement: .confirmationAction) {
                    if isSaving {
                        ProgressView()
                    } else {
                        Button("Save") { Task { await save() } }
                            .disabled(imageData == nil)
                    }
                }
            }
        }
    }

    private func loadImage(from item: PhotosPickerItem?) async {
        guard let item else { return }
        if let data = try? await item.loadTransferable(type: Data.self) {
            imageData = data
            if let uiImage = UIImage(data: data) {
                previewImage = Image(uiImage: uiImage)
            }
        }
    }

    private func save() async {
        guard let data = imageData else { return }
        let jpegData = UIImage(data: data).flatMap { $0.jpegData(compressionQuality: 0.85) } ?? data
        isSaving = true
        do {
            try await api.updateAlbumArt(album: target.album, imageData: jpegData)
            await onSaved()
            dismiss()
        } catch {
            self.error = error.localizedDescription
        }
        isSaving = false
    }
}

struct AlbumArtEditTarget: Identifiable {
    let album: String
    let representativeSongId: Int
    var id: String { album }
}
