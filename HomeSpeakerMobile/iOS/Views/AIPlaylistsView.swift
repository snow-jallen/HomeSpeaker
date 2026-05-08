import SwiftUI

struct AIPlaylistsView: View {
    @Environment(ConnectionStore.self) private var store
    @State private var playlists: [AiPlaylistSummaryDto] = []
    @State private var aiStatus: AiLibraryStatusDto?
    @State private var isLoading = false
    @State private var actionMessage: String?
    @State private var navigateToStatus = false

    var body: some View {
        NavigationStack {
            Group {
                if isLoading && playlists.isEmpty {
                    ProgressView("Loading AI playlists…")
                        .frame(maxWidth: .infinity, maxHeight: .infinity)
                } else if playlists.isEmpty {
                    emptyState
                } else {
                    playlistList
                }
            }
            .navigationTitle("AI Playlists")
            .navigationDestination(isPresented: $navigateToStatus) {
                AIStatusView()
            }
            .toolbar {
                ToolbarItem(placement: .topBarTrailing) {
                    NavigationLink {
                        AIStatusView()
                    } label: {
                        Image(systemName: "info.circle")
                    }
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
        }
    }

    private var emptyState: some View {
        ContentUnavailableView {
            Label("No AI Playlists", systemImage: "sparkles")
        } description: {
            if aiStatus?.isProcessing == true {
                Text("AI playlists will appear here as your library is analyzed.")
            } else {
                Text("AI playlists will appear here once your library has been analyzed.")
            }
        } actions: {
            Button("View Status") {
                navigateToStatus = true
            }
        }
    }

    private var playlistList: some View {
        List {
            if let status = aiStatus, status.isProcessing {
                Section {
                    HStack(spacing: 8) {
                        ProgressView()
                            .scaleEffect(0.8)
                        Text("Processing library… \(Int(status.percentComplete))% complete")
                            .font(.footnote)
                            .foregroundStyle(.secondary)
                    }
                }
            }

            ForEach(playlists) { playlist in
                NavigationLink {
                    AIPlaylistDetailView(playlist: playlist)
                } label: {
                    HStack {
                        Image(systemName: "sparkles")
                            .foregroundStyle(Color.accentColor)
                            .frame(width: 32)
                        VStack(alignment: .leading, spacing: 4) {
                            Text(playlist.displayName)
                                .font(.body)
                            Text(playlist.description)
                                .font(.caption)
                                .foregroundStyle(.secondary)
                                .lineLimit(2)
                            Text("\(playlist.songCount) songs")
                                .font(.caption2)
                                .foregroundStyle(.tertiary)
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
                    .frame(minHeight: 60)
                }
            }
        }
    }

    private func load() async {
        guard let api = store.api else { return }
        isLoading = true
        defer { isLoading = false }
        async let playlistsTask = api.getAiPlaylists()
        async let statusTask = api.getAiStatus()
        let fetched = try? await playlistsTask
        let status = try? await statusTask
        playlists = fetched ?? []
        playlists.sort { $0.sortOrder < $1.sortOrder }
        aiStatus = status
    }

    private func play(playlist: AiPlaylistSummaryDto) async {
        guard let api = store.api else { return }
        try? await api.playAiPlaylist(genreKey: playlist.genreKey)
        showMessage("Playing AI playlist: \(playlist.displayName)")
    }

    private func showMessage(_ msg: String) {
        withAnimation { actionMessage = msg }
        Task {
            try? await Task.sleep(for: .seconds(2))
            withAnimation { actionMessage = nil }
        }
    }
}

struct AIPlaylistDetailView: View {
    let playlist: AiPlaylistSummaryDto

    @Environment(ConnectionStore.self) private var store
    @State private var detail: AiPlaylistDto?
    @State private var isLoading = false
    @State private var actionMessage: String?

    var body: some View {
        Group {
            if isLoading && detail == nil {
                ProgressView("Loading songs…")
                    .frame(maxWidth: .infinity, maxHeight: .infinity)
            } else if let detail {
                songList(detail: detail)
            } else {
                ContentUnavailableView {
                    Label("No Songs", systemImage: "sparkles")
                } description: {
                    Text("This AI playlist has no songs yet.")
                }
            }
        }
        .navigationTitle(playlist.displayName)
        .toolbar {
            ToolbarItem(placement: .topBarTrailing) {
                Button {
                    Task { await playAll() }
                } label: {
                    Image(systemName: "play.fill")
                }
            }
        }
        .task { await load() }
        .refreshable { await load() }
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
    }

    private func songList(detail: AiPlaylistDto) -> some View {
        List {
            Section {
                Text(detail.description)
                    .font(.subheadline)
                    .foregroundStyle(.secondary)
            }

            Section("Songs") {
                ForEach(detail.songs) { song in
                    HStack {
                        VStack(alignment: .leading, spacing: 2) {
                            Text(song.displayTitle).lineLimit(1)
                            Text(song.displayArtist)
                                .font(.caption)
                                .foregroundStyle(.secondary)
                                .lineLimit(1)
                        }
                        Spacer()
                        Button {
                            Task { await playSong(song) }
                        } label: {
                            Image(systemName: "play.fill")
                                .foregroundStyle(Color.accentColor)
                        }
                        .buttonStyle(.borderless)
                    }
                }
            }
        }
    }

    private func load() async {
        guard let api = store.api else { return }
        isLoading = true
        defer { isLoading = false }
        detail = try? await api.getAiPlaylist(genreKey: playlist.genreKey)
    }

    private func playAll() async {
        guard let api = store.api else { return }
        try? await api.playAiPlaylist(genreKey: playlist.genreKey)
        showMessage("Playing: \(playlist.displayName)")
    }

    private func playSong(_ song: Song) async {
        guard let api = store.api else { return }
        try? await api.playSong(song.songId)
        showMessage("Playing: \(song.displayTitle)")
    }

    private func showMessage(_ msg: String) {
        withAnimation { actionMessage = msg }
        Task {
            try? await Task.sleep(for: .seconds(2))
            withAnimation { actionMessage = nil }
        }
    }
}
