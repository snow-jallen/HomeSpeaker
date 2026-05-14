import SwiftUI

struct OfflineDownloadsView: View {
    @Environment(ConnectionStore.self) private var store
    @Environment(OfflineDownloadsStore.self) private var offlineDownloads

    var body: some View {
        List {
            summarySection
            selectionsSection
            downloadsSection
        }
        .navigationTitle("Offline")
        .refreshable { await offlineDownloads.refreshLibrary(force: true) }
        .task {
            offlineDownloads.updateConnection(store.selectedConnection)
        }
        .toolbar {
            if offlineDownloads.managedSongs.contains(where: { $0.status == .failed }) {
                ToolbarItem(placement: .topBarTrailing) {
                    Button("Retry Failed") {
                        offlineDownloads.retryFailedDownloads()
                    }
                }
            }
        }
    }

    private var summarySection: some View {
        Section("Summary") {
            LabeledContent("Server", value: store.selectedConnection?.name ?? "No server")
            LabeledContent("Saved Tracks", value: "\(offlineDownloads.downloadCount)")
            LabeledContent("Pending", value: "\(offlineDownloads.pendingCount)")
            if offlineDownloads.failedCount > 0 {
                LabeledContent("Failed", value: "\(offlineDownloads.failedCount)")
            }
            LabeledContent("Storage", value: offlineDownloads.storageDescription)
            if offlineDownloads.isLoadingLibrary {
                HStack(spacing: 8) {
                    ProgressView()
                    Text("Refreshing download list…")
                        .foregroundStyle(.secondary)
                }
            } else if let error = offlineDownloads.lastError {
                Text(error)
                    .font(.caption)
                    .foregroundStyle(.red)
            }
        }
    }

    @ViewBuilder
    private var selectionsSection: some View {
        if offlineDownloads.currentArtistSelections.isEmpty &&
            offlineDownloads.currentAlbumSelections.isEmpty &&
            offlineDownloads.currentTrackSelections.isEmpty {
            Section("Keep Offline") {
                ContentUnavailableView(
                    "Nothing marked yet",
                    systemImage: "arrow.down.circle",
                    description: Text("Use the download buttons in Library to keep artists, albums, or tracks on this iPhone.")
                )
            }
        } else {
            if !offlineDownloads.currentArtistSelections.isEmpty {
                Section("Artists") {
                    ForEach(offlineDownloads.currentArtistSelections) { selection in
                        HStack {
                            Label(selection.artist, systemImage: "music.mic")
                            Spacer()
                            Text("\(offlineDownloads.songCount(for: selection)) songs")
                                .font(.caption)
                                .foregroundStyle(.secondary)
                        }
                        .swipeActions(edge: .trailing) {
                            Button(role: .destructive) {
                                offlineDownloads.removeArtistSelection(selection)
                            } label: {
                                Label("Remove", systemImage: "trash")
                            }
                        }
                    }
                }
            }

            if !offlineDownloads.currentAlbumSelections.isEmpty {
                Section("Albums") {
                    ForEach(offlineDownloads.currentAlbumSelections) { selection in
                        VStack(alignment: .leading, spacing: 2) {
                            Text(selection.album)
                            Text(selection.artist)
                                .font(.caption)
                                .foregroundStyle(.secondary)
                        }
                        .overlay(alignment: .trailing) {
                            Text("\(offlineDownloads.songCount(for: selection)) songs")
                                .font(.caption)
                                .foregroundStyle(.secondary)
                        }
                        .swipeActions(edge: .trailing) {
                            Button(role: .destructive) {
                                offlineDownloads.removeAlbumSelection(selection)
                            } label: {
                                Label("Remove", systemImage: "trash")
                            }
                        }
                    }
                }
            }

            if !offlineDownloads.currentTrackSelections.isEmpty {
                Section("Tracks") {
                    ForEach(offlineDownloads.currentTrackSelections) { selection in
                        VStack(alignment: .leading, spacing: 2) {
                            Text(offlineDownloads.trackTitle(for: selection))
                            Text(offlineDownloads.trackSubtitle(for: selection))
                                .font(.caption)
                                .foregroundStyle(.secondary)
                        }
                        .swipeActions(edge: .trailing) {
                            Button(role: .destructive) {
                                offlineDownloads.removeTrackSelection(selection)
                            } label: {
                                Label("Remove", systemImage: "trash")
                            }
                        }
                    }
                }
            }
        }
    }

    @ViewBuilder
    private var downloadsSection: some View {
        Section("Downloads") {
            if offlineDownloads.managedSongs.isEmpty {
                Text("No downloads in progress.")
                    .foregroundStyle(.secondary)
            } else {
                ForEach(offlineDownloads.managedSongs) { song in
                    HStack(spacing: 12) {
                        VStack(alignment: .leading, spacing: 2) {
                            Text(song.title)
                                .lineLimit(1)
                            Text("\(song.artist) • \(song.album)")
                                .font(.caption)
                                .foregroundStyle(.secondary)
                                .lineLimit(1)
                            if let failure = song.failureReason, song.status == .failed {
                                Text(failure)
                                    .font(.caption2)
                                    .foregroundStyle(.red)
                                    .lineLimit(2)
                            }
                        }
                        Spacer()
                        statusView(for: song)
                    }
                }
            }
        }
    }

    @ViewBuilder
    private func statusView(for song: OfflineManagedSong) -> some View {
        switch song.status {
        case .downloading:
            ProgressView()
                .frame(width: 24, height: 24)
        case .failed:
            Button {
                offlineDownloads.retry(song)
            } label: {
                Image(systemName: "exclamationmark.arrow.trianglehead.2.clockwise.rotate.90")
                    .foregroundStyle(.red)
            }
            .buttonStyle(.borderless)
        case .downloaded:
            Image(systemName: "checkmark.circle.fill")
                .foregroundStyle(.green)
        case .queued:
            Image(systemName: "arrow.down.circle.fill")
                .foregroundStyle(.blue)
        case .pending:
            Image(systemName: "arrow.down.circle")
                .foregroundStyle(.secondary)
        case .notTracked:
            EmptyView()
        }
    }
}
