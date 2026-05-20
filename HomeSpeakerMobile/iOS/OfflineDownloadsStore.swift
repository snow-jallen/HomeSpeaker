import Foundation
import Observation

enum OfflineDownloadPaths {
    private static let folderName = "HomeSpeakerOffline"

    static func rootDirectory() -> URL {
        let base = FileManager.default.urls(for: .applicationSupportDirectory, in: .userDomainMask).first
            ?? FileManager.default.urls(for: .documentDirectory, in: .userDomainMask).first!
        let url = base.appendingPathComponent(folderName, isDirectory: true)
        try? FileManager.default.createDirectory(at: url, withIntermediateDirectories: true)
        return url
    }

    static func directory(for connectionId: UUID) -> URL {
        let url = rootDirectory().appendingPathComponent(connectionId.uuidString, isDirectory: true)
        try? FileManager.default.createDirectory(at: url, withIntermediateDirectories: true)
        return url
    }

    static func plannedFileURL(for song: Song, connectionId: UUID) -> URL {
        let ext = URL(fileURLWithPath: song.path ?? "").pathExtension
        let fileExtension = ext.isEmpty ? "audio" : ext
        let fileName = "\(encodedFileStem(for: song.path ?? "\(song.songId)")).\(fileExtension)"
        return directory(for: connectionId).appendingPathComponent(fileName)
    }

    static func existingFileURL(for songPath: String, connectionId: UUID) -> URL? {
        guard !songPath.isEmpty else { return nil }
        let directoryURL = directory(for: connectionId)
        guard let files = try? FileManager.default.contentsOfDirectory(
            at: directoryURL,
            includingPropertiesForKeys: nil,
            options: [.skipsHiddenFiles]
        ) else {
            return nil
        }

        let prefix = "\(encodedFileStem(for: songPath))."
        return files.first(where: { $0.lastPathComponent.hasPrefix(prefix) })
    }

    static func removeFile(for songPath: String, connectionId: UUID) {
        guard !songPath.isEmpty else { return }
        guard let url = existingFileURL(for: songPath, connectionId: connectionId) else { return }
        try? FileManager.default.removeItem(at: url)
    }

    static func migrateLegacyFile(for song: Song, legacySongId: Int, connectionId: UUID) {
        guard let legacyURL = legacyFileURL(for: legacySongId, connectionId: connectionId) else { return }
        let targetURL = plannedFileURL(for: song, connectionId: connectionId)

        if legacyURL.path == targetURL.path { return }

        if FileManager.default.fileExists(atPath: targetURL.path) {
            try? FileManager.default.removeItem(at: legacyURL)
            return
        }

        try? FileManager.default.moveItem(at: legacyURL, to: targetURL)
    }

    private static func encodedFileStem(for value: String) -> String {
        Data(value.utf8)
            .base64EncodedString()
            .replacingOccurrences(of: "/", with: "_")
            .replacingOccurrences(of: "+", with: "-")
            .replacingOccurrences(of: "=", with: "")
    }

    private static func legacyFileURL(for songId: Int, connectionId: UUID) -> URL? {
        let directoryURL = directory(for: connectionId)
        guard let files = try? FileManager.default.contentsOfDirectory(
            at: directoryURL,
            includingPropertiesForKeys: nil,
            options: [.skipsHiddenFiles]
        ) else {
            return nil
        }

        let prefix = "\(songId)."
        return files.first(where: { $0.lastPathComponent.hasPrefix(prefix) })
    }
}

enum OfflineDownloadStatus: String {
    case notTracked
    case pending
    case queued
    case downloading
    case downloaded
    case failed
}

enum OfflineCollectionStatus {
    case notTracked
    case pending
    case queued
    case downloading
    case downloaded
    case failed
}

struct OfflineSongKey: Hashable, Codable, Identifiable {
    let connectionId: UUID
    let songPath: String
    private let legacySongId: Int?

    var id: String { "\(connectionId.uuidString)-\(storageKey)" }

    private var storageKey: String {
        if !songPath.isEmpty { return "path-\(songPath)" }
        return "legacy-\(legacySongId ?? -1)"
    }

    init(connectionId: UUID, songPath: String, legacySongId: Int? = nil) {
        self.connectionId = connectionId
        self.songPath = songPath
        self.legacySongId = legacySongId
    }

    init(from decoder: Decoder) throws {
        let container = try decoder.container(keyedBy: CodingKeys.self)
        connectionId = try container.decode(UUID.self, forKey: .connectionId)
        songPath = try container.decodeIfPresent(String.self, forKey: .songPath) ?? ""
        legacySongId = try container.decodeIfPresent(Int.self, forKey: .songId)
    }

    func encode(to encoder: Encoder) throws {
        var container = encoder.container(keyedBy: CodingKeys.self)
        try container.encode(connectionId, forKey: .connectionId)
        try container.encode(songPath, forKey: .songPath)
    }

    func hash(into hasher: inout Hasher) {
        hasher.combine(connectionId)
        hasher.combine(storageKey)
    }

    static func == (lhs: OfflineSongKey, rhs: OfflineSongKey) -> Bool {
        lhs.connectionId == rhs.connectionId && lhs.storageKey == rhs.storageKey
    }

    func resolved(using songs: [Song]) -> OfflineSongKey? {
        if !songPath.isEmpty { return OfflineSongKey(connectionId: connectionId, songPath: songPath) }
        guard let legacySongId,
              let resolvedPath = songs.first(where: { $0.songId == legacySongId })?.path else { return nil }
        return OfflineSongKey(connectionId: connectionId, songPath: resolvedPath)
    }

    var legacySongIdForMigration: Int? {
        songPath.isEmpty ? legacySongId : nil
    }

    private enum CodingKeys: String, CodingKey {
        case connectionId
        case songPath
        case songId
    }
}

struct LegacyOfflineArtistSelection: Codable, Hashable, Identifiable {
    let connectionId: UUID
    let artist: String

    var id: String { "\(connectionId.uuidString)-artist-\(artist)" }
}

struct LegacyOfflineAlbumSelection: Codable, Hashable, Identifiable {
    let connectionId: UUID
    let artist: String
    let album: String

    var id: String { "\(connectionId.uuidString)-album-\(artist)-\(album)" }
}

struct LegacyOfflineTrackSelection: Codable, Hashable, Identifiable {
    let connectionId: UUID
    let songPath: String
    private let legacySongId: Int?

    var id: String {
        if !songPath.isEmpty { return "\(connectionId.uuidString)-track-\(songPath)" }
        return "\(connectionId.uuidString)-track-legacy-\(legacySongId ?? -1)"
    }

    init(connectionId: UUID, songPath: String, legacySongId: Int? = nil) {
        self.connectionId = connectionId
        self.songPath = songPath
        self.legacySongId = legacySongId
    }

    init(from decoder: Decoder) throws {
        let container = try decoder.container(keyedBy: CodingKeys.self)
        connectionId = try container.decode(UUID.self, forKey: .connectionId)
        songPath = try container.decodeIfPresent(String.self, forKey: .songPath) ?? ""
        legacySongId = try container.decodeIfPresent(Int.self, forKey: .songId)
    }

    func encode(to encoder: Encoder) throws {
        var container = encoder.container(keyedBy: CodingKeys.self)
        try container.encode(connectionId, forKey: .connectionId)
        try container.encode(songPath, forKey: .songPath)
    }

    func resolved(using songs: [Song]) -> LegacyOfflineTrackSelection? {
        if !songPath.isEmpty { return LegacyOfflineTrackSelection(connectionId: connectionId, songPath: songPath) }
        guard let legacySongId,
              let resolvedPath = songs.first(where: { $0.songId == legacySongId })?.path else { return nil }
        return LegacyOfflineTrackSelection(connectionId: connectionId, songPath: resolvedPath)
    }

    private enum CodingKeys: String, CodingKey {
        case connectionId
        case songPath
        case songId
    }
}

struct OfflineDownloadRecord: Codable, Hashable, Identifiable {
    let key: OfflineSongKey
    let title: String
    let artist: String
    let album: String
    let addedAt: Date

    var id: String { key.id }
}

private extension Song {
    func offlineSongKey(connectionId: UUID) -> OfflineSongKey? {
        guard let path, !path.isEmpty else { return nil }
        return OfflineSongKey(connectionId: connectionId, songPath: path)
    }
}

private extension OfflineDownloadRecord {
    func resolved(using songs: [Song]) -> OfflineDownloadRecord? {
        guard let resolvedKey = key.resolved(using: songs) else { return nil }
        return OfflineDownloadRecord(
            key: resolvedKey,
            title: title,
            artist: artist,
            album: album,
            addedAt: addedAt
        )
    }
}

struct OfflineArtistSelection: Hashable, Identifiable {
    let targetId: Int
    let artist: String
    let resolvedSongCount: Int

    var id: Int { targetId }
}

struct OfflineAlbumSelection: Hashable, Identifiable {
    let targetId: Int
    let artist: String
    let album: String
    let resolvedSongCount: Int

    var id: Int { targetId }
}

struct OfflineTrackSelection: Hashable, Identifiable {
    let targetId: Int
    let songPath: String
    let title: String
    let subtitle: String

    var id: Int { targetId }
}

struct OfflineManagedSong: Identifiable {
    let key: OfflineSongKey
    let title: String
    let artist: String
    let album: String
    let status: OfflineDownloadStatus
    let failureReason: String?

    var id: String { key.id }
}

private struct OfflineLocalState: Codable {
    var artists: [LegacyOfflineArtistSelection] = []
    var albums: [LegacyOfflineAlbumSelection] = []
    var tracks: [LegacyOfflineTrackSelection] = []
    var downloads: [OfflineDownloadRecord] = []
}

private enum OfflineDownloadError: LocalizedError {
    case noConnection
    case songUnavailable
    case invalidResponse

    var errorDescription: String? {
        switch self {
        case .noConnection:
            return "No HomeSpeaker server is selected."
        case .songUnavailable:
            return "That track is no longer available to download."
        case .invalidResponse:
            return "The server returned an unexpected response while downloading."
        }
    }
}

@MainActor
@Observable
final class OfflineDownloadsStore {
    static let shared = OfflineDownloadsStore()

    private let manifestURL = OfflineDownloadPaths.rootDirectory().appendingPathComponent("manifest.json")

    private(set) var currentConnection: ServerConnection?
    private(set) var librarySongs: [Song] = []
    private(set) var isLoadingLibrary = false
    private(set) var lastError: String?
    private(set) var activeDownloadKey: OfflineSongKey?
    private(set) var failedMessages: [OfflineSongKey: String] = [:]

    private var localState = OfflineLocalState()
    private var serverManifest: OfflineDownloadManifestDto?
    private var queuedKeys: [OfflineSongKey] = []
    @ObservationIgnored private var downloadTask: Task<Void, Never>?

    init() {
        loadManifest()
        repairDownloadedRecords()
    }

    var currentArtistSelections: [OfflineArtistSelection] {
        manifestTargets
            .filter { $0.targetType == .artist }
            .map {
                OfflineArtistSelection(
                    targetId: $0.id,
                    artist: $0.artistName ?? $0.displayName,
                    resolvedSongCount: $0.resolvedSongCount
                )
            }
            .sorted { $0.artist < $1.artist }
    }

    var currentAlbumSelections: [OfflineAlbumSelection] {
        manifestTargets
            .filter { $0.targetType == .album }
            .map {
                OfflineAlbumSelection(
                    targetId: $0.id,
                    artist: $0.artistName ?? "Unknown Artist",
                    album: $0.albumName ?? $0.displayName,
                    resolvedSongCount: $0.resolvedSongCount
                )
            }
            .sorted {
                if $0.artist == $1.artist { return $0.album < $1.album }
                return $0.artist < $1.artist
            }
    }

    var currentTrackSelections: [OfflineTrackSelection] {
        manifestTargets
            .filter { $0.targetType == .song }
            .map {
                let title = $0.song?.displayTitle ?? $0.displayName
                let subtitle = [
                    $0.song?.displayArtist ?? $0.artistName,
                    $0.song?.displayAlbum ?? $0.albumName
                ]
                .compactMap { value in
                    guard let value, !value.isEmpty else { return nil }
                    return value
                }
                .joined(separator: " • ")

                return OfflineTrackSelection(
                    targetId: $0.id,
                    songPath: $0.songPath ?? $0.song?.path ?? "",
                    title: title,
                    subtitle: subtitle.isEmpty ? "Saved track" : subtitle
                )
            }
            .sorted { $0.title < $1.title }
    }

    var currentDownloadRecords: [OfflineDownloadRecord] {
        let connectionId = currentConnection?.id
        return localState.downloads
            .filter { $0.key.connectionId == connectionId }
            .sorted {
                if $0.artist == $1.artist {
                    if $0.album == $1.album { return $0.title < $1.title }
                    return $0.album < $1.album
                }
                return $0.artist < $1.artist
            }
    }

    var managedSongs: [OfflineManagedSong] {
        guard let connection = currentConnection else { return [] }

        var items: [OfflineSongKey: OfflineManagedSong] = [:]
        let desired = desiredSongKeys(for: connection.id)

        for entry in manifestSongs {
            let key = OfflineSongKey(connectionId: connection.id, songPath: entry.songPath)
            items[key] = OfflineManagedSong(
                key: key,
                title: entry.song.displayTitle,
                artist: entry.song.displayArtist,
                album: entry.song.displayAlbum,
                status: status(for: key),
                failureReason: failedMessages[key]
            )
        }

        for record in currentDownloadRecords where items[record.key] == nil || desired.contains(record.key) == false {
            items[record.key] = OfflineManagedSong(
                key: record.key,
                title: record.title,
                artist: record.artist,
                album: record.album,
                status: status(for: record.key),
                failureReason: failedMessages[record.key]
            )
        }

        return items.values.sorted {
            if $0.artist == $1.artist {
                if $0.album == $1.album { return $0.title < $1.title }
                return $0.album < $1.album
            }
            return $0.artist < $1.artist
        }
    }

    var downloadCount: Int {
        managedSongs.filter { $0.status == .downloaded }.count
    }

    var pendingCount: Int {
        managedSongs.filter { [.pending, .queued, .downloading].contains($0.status) }.count
    }

    var failedCount: Int {
        managedSongs.filter { $0.status == .failed }.count
    }

    var summaryLine: String {
        if downloadCount == 0 && pendingCount == 0 && failedCount == 0 {
            return "Nothing saved"
        }

        var parts: [String] = []
        if downloadCount > 0 { parts.append("\(downloadCount) saved") }
        if pendingCount > 0 { parts.append("\(pendingCount) pending") }
        if failedCount > 0 { parts.append("\(failedCount) failed") }
        return parts.joined(separator: " · ")
    }

    var storageDescription: String {
        let byteCount = currentDownloadRecords.reduce(into: Int64(0)) { total, record in
            if let url = OfflineDownloadPaths.existingFileURL(for: record.key.songPath, connectionId: record.key.connectionId),
               let size = try? url.resourceValues(forKeys: [.fileSizeKey]).fileSize {
                total += Int64(size)
            }
        }

        return ByteCountFormatter.string(fromByteCount: byteCount, countStyle: .file)
    }

    private var manifestTargets: [OfflineDownloadTargetDto] {
        serverManifest?.targets ?? []
    }

    private var manifestSongs: [OfflineDownloadSongDto] {
        serverManifest?.songs ?? []
    }

    func updateConnection(_ connection: ServerConnection?) {
        guard currentConnection?.id != connection?.id else {
            if connection != nil, serverManifest == nil {
                Task { await refreshLibrary(force: true) }
            }
            return
        }

        downloadTask?.cancel()
        downloadTask = nil
        queuedKeys.removeAll()
        activeDownloadKey = nil
        failedMessages.removeAll()
        currentConnection = connection
        serverManifest = nil
        librarySongs = []
        lastError = nil

        guard connection != nil else { return }
        Task { await refreshLibrary(force: true) }
    }

    func refreshLibrary(force: Bool = true) async {
        guard let connection = currentConnection else { return }
        if !force && serverManifest != nil {
            syncDesiredState()
            return
        }

        isLoadingLibrary = true
        defer { isLoadingLibrary = false }

        let api = APIClient(baseURL: connection.baseURL)

        do {
            serverManifest = try await api.getOfflineDownloadManifest()
            lastError = nil

            if needsLegacySongResolution(for: connection.id), librarySongs.isEmpty {
                let songs = try await api.getSongs()
                librarySongs = sortedSongs(songs)
                migrateLegacyState(for: connection.id)
            }

            if try await migrateLegacySelectionsIfNeeded(connection: connection, api: api) {
                serverManifest = try await api.getOfflineDownloadManifest()
            }

            syncDesiredState()
        } catch {
            lastError = error.localizedDescription
        }
    }

    func updateLibrary(_ songs: [Song], connection: ServerConnection?) {
        guard let connection else {
            librarySongs = []
            return
        }

        currentConnection = connection
        librarySongs = sortedSongs(songs)
        lastError = nil
        migrateLegacyState(for: connection.id)
        syncDesiredState()
    }

    func offlineLibrarySongs(connection: ServerConnection?) -> [Song] {
        guard let connection else { return [] }

        let existingDownloads = localState.downloads.filter {
            $0.key.connectionId == connection.id &&
                !$0.key.songPath.isEmpty &&
                OfflineDownloadPaths.existingFileURL(for: $0.key.songPath, connectionId: connection.id) != nil
        }

        let songs = existingDownloads.map { record in
            Song(
                songId: offlineSongId(for: record.key),
                name: record.title,
                path: record.key.songPath,
                album: record.album.isEmpty ? nil : record.album,
                artist: record.artist.isEmpty ? nil : record.artist
            )
        }

        return sortedSongs(songs)
    }

    func isArtistSelected(_ artist: String, connection: ServerConnection?) -> Bool {
        guard connection?.id == currentConnection?.id else { return false }
        return artistTarget(named: artist) != nil
    }

    func isAlbumSelected(artist: String, album: String, connection: ServerConnection?) -> Bool {
        guard connection?.id == currentConnection?.id else { return false }
        return albumTarget(artist: artist, album: album) != nil
    }

    func isTrackSelected(_ song: Song, connection: ServerConnection?) -> Bool {
        guard connection?.id == currentConnection?.id, let path = song.path else { return false }
        return trackTarget(songPath: path) != nil
    }

    func status(for song: Song, connection: ServerConnection?) -> OfflineDownloadStatus {
        guard let connection, let key = song.offlineSongKey(connectionId: connection.id) else { return .notTracked }
        return status(for: key)
    }

    func collectionStatus(for songs: [Song], connection: ServerConnection?) -> OfflineCollectionStatus {
        guard let connection, !songs.isEmpty else { return .notTracked }

        let statuses = songs
            .compactMap { $0.offlineSongKey(connectionId: connection.id) }
            .map { status(for: $0) }
        guard !statuses.isEmpty else { return .notTracked }

        if statuses.contains(.failed) { return .failed }
        if statuses.contains(.downloading) { return .downloading }
        if statuses.contains(.queued) { return .queued }
        if statuses.allSatisfy({ $0 == .downloaded }) { return .downloaded }
        if statuses.contains(.pending) || statuses.contains(.downloaded) { return .pending }
        return .notTracked
    }

    func toggleArtist(_ artist: String, songs _: [Song], connection: ServerConnection?) {
        guard let connection else { return }
        Task { await toggleArtistSelection(artist, connection: connection) }
    }

    func toggleAlbum(artist: String, album: String, songs _: [Song], connection: ServerConnection?) {
        guard let connection else { return }
        Task { await toggleAlbumSelection(artist: artist, album: album, connection: connection) }
    }

    func toggleTrack(_ song: Song, connection: ServerConnection?) {
        guard let connection, let songPath = song.path else { return }
        Task { await toggleTrackSelection(songId: song.songId, songPath: songPath, connection: connection) }
    }

    func removeArtistSelection(_ selection: OfflineArtistSelection) {
        Task { await removeTarget(id: selection.targetId) }
    }

    func removeAlbumSelection(_ selection: OfflineAlbumSelection) {
        Task { await removeTarget(id: selection.targetId) }
    }

    func removeTrackSelection(_ selection: OfflineTrackSelection) {
        Task { await removeTarget(id: selection.targetId) }
    }

    func retryFailedDownloads() {
        failedMessages.removeAll()
        syncDesiredState()
    }

    func retry(_ song: OfflineManagedSong) {
        failedMessages.removeValue(forKey: song.key)
        if !queuedKeys.contains(song.key) {
            queuedKeys.append(song.key)
        }
        ensureDownloadLoop()
    }

    func songCount(for selection: OfflineArtistSelection) -> Int {
        selection.resolvedSongCount
    }

    func songCount(for selection: OfflineAlbumSelection) -> Int {
        selection.resolvedSongCount
    }

    func trackTitle(for selection: OfflineTrackSelection) -> String {
        selection.title
    }

    func trackSubtitle(for selection: OfflineTrackSelection) -> String {
        selection.subtitle
    }

    private func status(for key: OfflineSongKey) -> OfflineDownloadStatus {
        if activeDownloadKey == key { return .downloading }
        if queuedKeys.contains(key) { return .queued }
        if localState.downloads.contains(where: { $0.key == key }) &&
            OfflineDownloadPaths.existingFileURL(for: key.songPath, connectionId: key.connectionId) != nil {
            return .downloaded
        }
        if failedMessages[key] != nil { return .failed }
        if desiredSongKeys(for: key.connectionId).contains(key) { return .pending }
        return .notTracked
    }

    private func desiredSongKeys(for connectionId: UUID) -> Set<OfflineSongKey> {
        guard currentConnection?.id == connectionId else { return [] }
        return Set(manifestSongs.compactMap { entry in
            let path = entry.songPath
            guard !path.isEmpty else { return nil }
            return OfflineSongKey(connectionId: connectionId, songPath: path)
        })
    }

    private func syncDesiredState() {
        repairDownloadedRecords()
        guard let connection = currentConnection else {
            persistManifest()
            return
        }
        guard serverManifest != nil else {
            persistManifest()
            return
        }

        let desired = desiredSongKeys(for: connection.id)

        localState.downloads.removeAll { record in
            guard record.key.connectionId == connection.id, !desired.contains(record.key) else { return false }
            OfflineDownloadPaths.removeFile(for: record.key.songPath, connectionId: record.key.connectionId)
            return true
        }

        queuedKeys.removeAll { !desired.contains($0) }
        failedMessages = failedMessages.filter { desired.contains($0.key) }

        for key in desired {
            guard localState.downloads.contains(where: { $0.key == key }) == false else { continue }
            guard activeDownloadKey != key else { continue }
            guard queuedKeys.contains(key) == false else { continue }
            guard manifestSong(for: key) != nil else { continue }
            queuedKeys.append(key)
        }

        persistManifest()
        ensureDownloadLoop()
    }

    private func ensureDownloadLoop() {
        guard downloadTask == nil, queuedKeys.isEmpty == false else { return }
        downloadTask = Task { [weak self] in
            await self?.processQueue()
        }
    }

    private func processQueue() async {
        while Task.isCancelled == false {
            guard let key = nextQueuedKey() else {
                downloadTask = nil
                return
            }

            do {
                try await downloadSong(for: key)
                failedMessages.removeValue(forKey: key)
            } catch {
                failedMessages[key] = error.localizedDescription
            }

            activeDownloadKey = nil
            persistManifest()
        }

        downloadTask = nil
    }

    private func nextQueuedKey() -> OfflineSongKey? {
        guard let first = queuedKeys.first else { return nil }
        queuedKeys.removeFirst()
        activeDownloadKey = first
        return first
    }

    private func downloadSong(for key: OfflineSongKey) async throws {
        guard let connection = currentConnection, connection.id == key.connectionId else {
            throw OfflineDownloadError.noConnection
        }
        guard let entry = manifestSong(for: key) else {
            throw OfflineDownloadError.songUnavailable
        }

        let api = APIClient(baseURL: connection.baseURL)
        let downloadURL = api.offlineDownloadURL(entry.downloadUrl)
        let (tempURL, response) = try await URLSession.shared.download(from: downloadURL)

        guard let http = response as? HTTPURLResponse else {
            throw OfflineDownloadError.invalidResponse
        }
        guard (200..<300).contains(http.statusCode) else {
            throw APIError.serverError(http.statusCode, "Download failed")
        }

        let targetURL = OfflineDownloadPaths.plannedFileURL(for: entry.song, connectionId: connection.id)
        if FileManager.default.fileExists(atPath: targetURL.path) {
            try? FileManager.default.removeItem(at: targetURL)
        }
        try FileManager.default.moveItem(at: tempURL, to: targetURL)

        let record = OfflineDownloadRecord(
            key: key,
            title: entry.song.displayTitle,
            artist: entry.song.displayArtist,
            album: entry.song.displayAlbum,
            addedAt: Date()
        )

        localState.downloads.removeAll { $0.key == key }
        localState.downloads.append(record)
    }

    private func manifestSong(for key: OfflineSongKey) -> OfflineDownloadSongDto? {
        manifestSongs.first { songsEqual($0.songPath, key.songPath) }
    }

    private func repairDownloadedRecords() {
        let originalCount = localState.downloads.count
        localState.downloads.removeAll {
            !$0.key.songPath.isEmpty &&
                OfflineDownloadPaths.existingFileURL(for: $0.key.songPath, connectionId: $0.key.connectionId) == nil
        }
        if originalCount != localState.downloads.count {
            persistManifest()
        }
    }

    private func migrateLegacyState(for connectionId: UUID) {
        var didChange = false

        localState.tracks = localState.tracks.compactMap { selection in
            guard selection.connectionId == connectionId else { return selection }
            guard !selection.songPath.isEmpty else {
                guard let resolved = selection.resolved(using: librarySongs) else {
                    didChange = true
                    return nil
                }
                didChange = didChange || resolved.id != selection.id
                return resolved
            }
            return selection
        }

        localState.downloads = localState.downloads.compactMap { record in
            guard record.key.connectionId == connectionId else { return record }
            guard !record.key.songPath.isEmpty else {
                if let legacySongId = record.key.legacySongIdForMigration,
                   let song = librarySongs.first(where: { $0.songId == legacySongId }) {
                    OfflineDownloadPaths.migrateLegacyFile(for: song, legacySongId: legacySongId, connectionId: connectionId)
                }
                guard let resolved = record.resolved(using: librarySongs) else {
                    didChange = true
                    return nil
                }
                didChange = didChange || resolved.key != record.key
                return resolved
            }
            return record
        }

        queuedKeys = queuedKeys.compactMap { key in
            guard key.connectionId == connectionId else { return key }
            guard !key.songPath.isEmpty else {
                guard let resolved = key.resolved(using: librarySongs) else {
                    didChange = true
                    return nil
                }
                didChange = didChange || resolved != key
                return resolved
            }
            return key
        }

        var migratedFailures: [OfflineSongKey: String] = [:]
        for (key, value) in failedMessages {
            if key.connectionId == connectionId {
                if key.songPath.isEmpty {
                    guard let resolved = key.resolved(using: librarySongs) else {
                        didChange = true
                        continue
                    }
                    didChange = didChange || resolved != key
                    migratedFailures[resolved] = value
                } else {
                    migratedFailures[key] = value
                }
            } else {
                migratedFailures[key] = value
            }
        }
        failedMessages = migratedFailures

        if let activeDownloadKey, activeDownloadKey.connectionId == connectionId, activeDownloadKey.songPath.isEmpty {
            if let resolved = activeDownloadKey.resolved(using: librarySongs) {
                didChange = didChange || resolved != activeDownloadKey
                self.activeDownloadKey = resolved
            } else {
                didChange = true
                self.activeDownloadKey = nil
            }
        }

        if didChange {
            localState.tracks = Array(Set(localState.tracks))
            localState.downloads = Array(Set(localState.downloads))
            persistManifest()
        }
    }

    private func migrateLegacySelectionsIfNeeded(connection: ServerConnection, api: APIClient) async throws -> Bool {
        let connectionId = connection.id
        var didAddServerTarget = false
        var didChangeLocalState = false

        for selection in localState.artists.filter({ $0.connectionId == connectionId }) {
            _ = try await api.addOfflineDownloadTarget(targetType: .artist, artistName: selection.artist)
            localState.artists.removeAll { $0 == selection }
            didAddServerTarget = true
            didChangeLocalState = true
        }

        for selection in localState.albums.filter({ $0.connectionId == connectionId }) {
            _ = try await api.addOfflineDownloadTarget(
                targetType: .album,
                artistName: selection.artist,
                albumName: selection.album
            )
            localState.albums.removeAll { $0 == selection }
            didAddServerTarget = true
            didChangeLocalState = true
        }

        if localState.tracks.contains(where: { $0.connectionId == connectionId && $0.songPath.isEmpty }), librarySongs.isEmpty {
            let songs = try await api.getSongs()
            librarySongs = sortedSongs(songs)
            migrateLegacyState(for: connectionId)
        }

        for selection in localState.tracks.filter({ $0.connectionId == connectionId }) {
            guard let resolved = selection.resolved(using: librarySongs) else {
                localState.tracks.removeAll { $0 == selection }
                didChangeLocalState = true
                continue
            }

            _ = try await api.addOfflineDownloadTarget(targetType: .song, songPath: resolved.songPath)
            localState.tracks.removeAll { $0 == selection }
            didAddServerTarget = true
            didChangeLocalState = true
        }

        if didChangeLocalState {
            persistManifest()
        }

        return didAddServerTarget
    }

    private func toggleArtistSelection(_ artist: String, connection: ServerConnection) async {
        guard currentConnection?.id == connection.id else { return }
        let api = APIClient(baseURL: connection.baseURL)

        do {
            if let target = artistTarget(named: artist) {
                try await api.removeOfflineDownloadTarget(targetId: target.id)
            } else {
                _ = try await api.addOfflineDownloadTarget(targetType: .artist, artistName: artist)
            }
            await refreshLibrary(force: true)
        } catch {
            lastError = error.localizedDescription
        }
    }

    private func toggleAlbumSelection(artist: String, album: String, connection: ServerConnection) async {
        guard currentConnection?.id == connection.id else { return }
        let api = APIClient(baseURL: connection.baseURL)

        do {
            if let target = albumTarget(artist: artist, album: album) {
                try await api.removeOfflineDownloadTarget(targetId: target.id)
            } else {
                _ = try await api.addOfflineDownloadTarget(targetType: .album, artistName: artist, albumName: album)
            }
            await refreshLibrary(force: true)
        } catch {
            lastError = error.localizedDescription
        }
    }

    private func toggleTrackSelection(songId: Int, songPath: String, connection: ServerConnection) async {
        guard currentConnection?.id == connection.id else { return }
        let api = APIClient(baseURL: connection.baseURL)

        do {
            if let target = trackTarget(songPath: songPath) {
                try await api.removeOfflineDownloadTarget(targetId: target.id)
            } else {
                _ = try await api.addOfflineDownloadTarget(targetType: .song, songId: songId, songPath: songPath)
            }
            await refreshLibrary(force: true)
        } catch {
            lastError = error.localizedDescription
        }
    }

    private func removeTarget(id targetId: Int) async {
        guard let connection = currentConnection else { return }
        let api = APIClient(baseURL: connection.baseURL)

        do {
            try await api.removeOfflineDownloadTarget(targetId: targetId)
            await refreshLibrary(force: true)
        } catch {
            lastError = error.localizedDescription
        }
    }

    private func artistTarget(named artist: String) -> OfflineDownloadTargetDto? {
        manifestTargets.first {
            $0.targetType == .artist && stringsEqual($0.artistName ?? $0.displayName, artist)
        }
    }

    private func albumTarget(artist: String, album: String) -> OfflineDownloadTargetDto? {
        manifestTargets.first {
            $0.targetType == .album &&
                stringsEqual($0.artistName ?? "", artist) &&
                stringsEqual($0.albumName ?? $0.displayName, album)
        }
    }

    private func trackTarget(songPath: String) -> OfflineDownloadTargetDto? {
        manifestTargets.first {
            $0.targetType == .song && songsEqual($0.songPath ?? $0.song?.path ?? "", songPath)
        }
    }

    private func sortedSongs(_ songs: [Song]) -> [Song] {
        songs.sorted {
            if $0.displayArtist == $1.displayArtist {
                if $0.displayAlbum == $1.displayAlbum { return $0.displayTitle < $1.displayTitle }
                return $0.displayAlbum < $1.displayAlbum
            }
            return $0.displayArtist < $1.displayArtist
        }
    }

    private func offlineSongId(for key: OfflineSongKey) -> Int {
        let value = "\(key.connectionId.uuidString)|\(key.songPath)"
        var hash: UInt64 = 14_695_981_039_346_656_037
        for byte in value.utf8 {
            hash ^= UInt64(byte)
            hash &*= 1_099_511_628_211
        }

        let bounded = Int(hash % UInt64(Int.max - 1)) + 1
        return -bounded
    }

    private func stringsEqual(_ lhs: String, _ rhs: String) -> Bool {
        lhs.compare(rhs, options: [.caseInsensitive, .diacriticInsensitive]) == .orderedSame
    }

    private func songsEqual(_ lhs: String, _ rhs: String) -> Bool {
        lhs.compare(rhs, options: [.caseInsensitive]) == .orderedSame
    }

    private func needsLegacySongResolution(for connectionId: UUID) -> Bool {
        if localState.tracks.contains(where: { $0.connectionId == connectionId && $0.songPath.isEmpty }) {
            return true
        }

        if localState.downloads.contains(where: { $0.key.connectionId == connectionId && $0.key.songPath.isEmpty }) {
            return true
        }

        if queuedKeys.contains(where: { $0.connectionId == connectionId && $0.songPath.isEmpty }) {
            return true
        }

        if failedMessages.keys.contains(where: { $0.connectionId == connectionId && $0.songPath.isEmpty }) {
            return true
        }

        if let activeDownloadKey, activeDownloadKey.connectionId == connectionId, activeDownloadKey.songPath.isEmpty {
            return true
        }

        return false
    }

    private func loadManifest() {
        guard let data = try? Data(contentsOf: manifestURL),
              let decoded = try? JSONDecoder().decode(OfflineLocalState.self, from: data) else {
            localState = OfflineLocalState()
            return
        }

        localState = decoded
    }

    private func persistManifest() {
        let encoder = JSONEncoder()
        encoder.outputFormatting = [.prettyPrinted, .sortedKeys]
        guard let data = try? encoder.encode(localState) else { return }
        try? data.write(to: manifestURL, options: .atomic)
    }
}
