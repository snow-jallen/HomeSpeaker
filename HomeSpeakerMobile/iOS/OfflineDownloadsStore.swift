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

    static func fileStem(for songPath: String) -> String {
        encodedFileStem(for: songPath)
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

struct StoredArtistSelection: Codable, Hashable, Identifiable {
    let connectionId: UUID
    let artist: String

    var id: String { "\(connectionId.uuidString)-artist-\(artist)" }
}

struct StoredAlbumSelection: Codable, Hashable, Identifiable {
    let connectionId: UUID
    let artist: String
    let album: String

    var id: String { "\(connectionId.uuidString)-album-\(artist)-\(album)" }
}

struct StoredTrackSelection: Codable, Hashable, Identifiable {
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

    func resolved(using songs: [Song]) -> StoredTrackSelection? {
        if !songPath.isEmpty { return StoredTrackSelection(connectionId: connectionId, songPath: songPath) }
        guard let legacySongId,
              let resolvedPath = songs.first(where: { $0.songId == legacySongId })?.path else { return nil }
        return StoredTrackSelection(connectionId: connectionId, songPath: resolvedPath)
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
    let artist: String
    let resolvedSongCount: Int

    var id: String { artist }
}

struct OfflineAlbumSelection: Hashable, Identifiable {
    let artist: String
    let album: String
    let resolvedSongCount: Int

    var id: String { "\(artist)|\(album)" }
}

struct OfflineTrackSelection: Hashable, Identifiable {
    let songPath: String
    let title: String
    let subtitle: String

    var id: String { songPath }
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
    var artists: [StoredArtistSelection] = []
    var albums: [StoredAlbumSelection] = []
    var tracks: [StoredTrackSelection] = []
    var downloads: [OfflineDownloadRecord] = []
    // One-time upgrade marker: versions that kept selections on the server
    // left the local arrays empty, so their downloads are re-seeded as track
    // selections the first time this device-local build runs.
    var selectionsSeeded: Bool?
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
    private static let fnv1aOffsetBasis: UInt64 = 14_695_981_039_346_656_037
    private static let fnv1aPrime: UInt64 = 1_099_511_628_211

    private let manifestURL = OfflineDownloadPaths.rootDirectory().appendingPathComponent("manifest.json")

    private(set) var currentConnection: ServerConnection?
    private(set) var librarySongs: [Song] = []
    private(set) var isLoadingLibrary = false
    private(set) var lastError: String?
    private(set) var activeDownloadKey: OfflineSongKey?
    private(set) var failedMessages: [OfflineSongKey: String] = [:]

    private var localState = OfflineLocalState()
    private var queuedKeys: [OfflineSongKey] = []
    @ObservationIgnored private var downloadTask: Task<Void, Never>?

    // status(for:) runs for every visible row on every render. Without these
    // caches each call re-listed the downloads directory and rebuilt key sets
    // from scratch, which froze the main thread for seconds on large
    // libraries. All are invalidated together at the mutation chokepoints.
    @ObservationIgnored private var downloadedStemsCache: [UUID: Set<String>] = [:]
    @ObservationIgnored private var desiredKeysCache: [UUID: Set<OfflineSongKey>] = [:]
    @ObservationIgnored private var downloadKeysCache: Set<OfflineSongKey>?
    @ObservationIgnored private var queuedKeySetCache: Set<OfflineSongKey>?

    private func invalidateStatusCaches() {
        downloadedStemsCache.removeAll()
        desiredKeysCache.removeAll()
        downloadKeysCache = nil
        queuedKeySetCache = nil
    }

    private var downloadKeys: Set<OfflineSongKey> {
        if let downloadKeysCache { return downloadKeysCache }
        let keys = Set(localState.downloads.map(\.key))
        downloadKeysCache = keys
        return keys
    }

    private var queuedKeySet: Set<OfflineSongKey> {
        if let queuedKeySetCache { return queuedKeySetCache }
        let keys = Set(queuedKeys)
        queuedKeySetCache = keys
        return keys
    }

    private func downloadedStems(for connectionId: UUID) -> Set<String> {
        if let cached = downloadedStemsCache[connectionId] { return cached }
        let directoryURL = OfflineDownloadPaths.directory(for: connectionId)
        let names = (try? FileManager.default.contentsOfDirectory(
            at: directoryURL,
            includingPropertiesForKeys: nil,
            options: [.skipsHiddenFiles]
        ))?.map(\.lastPathComponent) ?? []
        let stems = Set(names.map { String($0.prefix(while: { $0 != "." })) })
        downloadedStemsCache[connectionId] = stems
        return stems
    }

    private func hasDownloadedFile(for key: OfflineSongKey) -> Bool {
        guard !key.songPath.isEmpty else { return false }
        return downloadedStems(for: key.connectionId).contains(OfflineDownloadPaths.fileStem(for: key.songPath))
    }

    init() {
        loadManifest()
        repairDownloadedRecords()
        seedSelectionsFromDownloadsIfNeeded()
    }

    var currentArtistSelections: [OfflineArtistSelection] {
        guard let connectionId = currentConnection?.id else { return [] }
        return localState.artists
            .filter { $0.connectionId == connectionId }
            .map {
                OfflineArtistSelection(
                    artist: $0.artist,
                    resolvedSongCount: librarySongs(forArtist: $0.artist).count
                )
            }
            .sorted { $0.artist < $1.artist }
    }

    var currentAlbumSelections: [OfflineAlbumSelection] {
        guard let connectionId = currentConnection?.id else { return [] }
        return localState.albums
            .filter { $0.connectionId == connectionId }
            .map {
                OfflineAlbumSelection(
                    artist: $0.artist,
                    album: $0.album,
                    resolvedSongCount: librarySongs(forArtist: $0.artist, album: $0.album).count
                )
            }
            .sorted {
                if $0.artist == $1.artist { return $0.album < $1.album }
                return $0.artist < $1.artist
            }
    }

    var currentTrackSelections: [OfflineTrackSelection] {
        guard let connectionId = currentConnection?.id else { return [] }
        return localState.tracks
            .filter { $0.connectionId == connectionId && !$0.songPath.isEmpty }
            .map { selection in
                let song = librarySong(forPath: selection.songPath)
                let key = OfflineSongKey(connectionId: connectionId, songPath: selection.songPath)
                let record = localState.downloads.first { $0.key == key }
                let title = song?.displayTitle
                    ?? record?.title
                    ?? URL(fileURLWithPath: selection.songPath).deletingPathExtension().lastPathComponent
                let subtitle = [
                    song?.displayArtist ?? record?.artist,
                    song?.displayAlbum ?? record?.album
                ]
                .compactMap { value in
                    guard let value, !value.isEmpty else { return nil }
                    return value
                }
                .joined(separator: " • ")

                return OfflineTrackSelection(
                    songPath: selection.songPath,
                    title: title,
                    subtitle: subtitle.isEmpty ? "Saved track" : subtitle
                )
            }
            .sorted { $0.title < $1.title }
    }

    private func librarySongs(forArtist artist: String) -> [Song] {
        librarySongs.filter { stringsEqual($0.displayArtist, artist) }
    }

    private func librarySongs(forArtist artist: String, album: String) -> [Song] {
        librarySongs.filter { stringsEqual($0.displayArtist, artist) && stringsEqual($0.displayAlbum, album) }
    }

    private func librarySong(forPath path: String) -> Song? {
        librarySongs.first { songsEqual($0.path ?? "", path) }
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

        for key in desired {
            guard let song = librarySong(forPath: key.songPath) else { continue }
            items[key] = OfflineManagedSong(
                key: key,
                title: song.displayTitle,
                artist: song.displayArtist,
                album: song.displayAlbum,
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

    func updateConnection(_ connection: ServerConnection?) {
        guard currentConnection?.id != connection?.id else {
            if connection != nil, librarySongs.isEmpty {
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
        librarySongs = []
        lastError = nil
        invalidateStatusCaches()

        guard connection != nil else { return }
        Task { await refreshLibrary(force: true) }
    }

    func refreshLibrary(force: Bool = true) async {
        guard let connection = currentConnection else { return }
        if !force && !librarySongs.isEmpty {
            syncDesiredState()
            return
        }

        isLoadingLibrary = true
        defer { isLoadingLibrary = false }

        let api = APIClient(baseURL: connection.baseURL)

        do {
            let songs = try await api.getSongs()
            librarySongs = sortedSongs(songs)
            invalidateStatusCaches()
            lastError = nil
            migrateLegacyState(for: connection.id)
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
        // The desired-song set is resolved against librarySongs now, so any
        // cached keys are stale the moment the library changes.
        invalidateStatusCaches()
        migrateLegacyState(for: connection.id)
        syncDesiredState()
    }

    func offlineLibrarySongs(connection: ServerConnection?) -> [Song] {
        guard let connection else { return [] }

        let existingDownloads = localState.downloads.filter {
            $0.key.connectionId == connection.id &&
                !$0.key.songPath.isEmpty &&
                hasDownloadedFile(for: $0.key)
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

    func toggleArtist(_ artist: String, songs: [Song], connection: ServerConnection?) {
        guard let connection, connection.id == currentConnection?.id else { return }
        if areSongsKeptOffline(songs, connection: connection) {
            removeArtistCoverage(artist: artist, songs: songs, connectionId: connection.id)
        } else {
            addArtistSelection(artist, connectionId: connection.id)
        }
        selectionsChanged()
    }

    func toggleAlbum(artist: String, album: String, songs: [Song], connection: ServerConnection?) {
        guard let connection, connection.id == currentConnection?.id else { return }
        if areSongsKeptOffline(songs, connection: connection) {
            removeAlbumCoverage(artist: artist, album: album, songs: songs, connectionId: connection.id)
        } else {
            addAlbumSelection(artist: artist, album: album, connectionId: connection.id)
        }
        selectionsChanged()
    }

    func toggleTrack(_ song: Song, connection: ServerConnection?) {
        guard let connection, connection.id == currentConnection?.id,
              let songPath = song.path, !songPath.isEmpty else { return }
        if areSongsKeptOffline([song], connection: connection) {
            removeTrackCoverage(song: song, connectionId: connection.id)
        } else {
            addTrackSelection(songPath, connectionId: connection.id)
        }
        selectionsChanged()
    }

    /// True when every song is already kept offline, regardless of whether the
    /// coverage comes from an artist, album, or individual track selection.
    func areSongsKeptOffline(_ songs: [Song], connection: ServerConnection?) -> Bool {
        guard let connection, connection.id == currentConnection?.id else { return false }
        let keys = songs.compactMap { $0.offlineSongKey(connectionId: connection.id) }
        guard !keys.isEmpty else { return false }
        let desired = desiredSongKeys(for: connection.id)
        return keys.allSatisfy { desired.contains($0) }
    }

    /// Marks each song to be kept offline; returns how many were newly added.
    func keepTracksOffline(_ songs: [Song], connection: ServerConnection?) -> Int {
        guard let connection, connection.id == currentConnection?.id else { return 0 }
        let desired = desiredSongKeys(for: connection.id)
        var added = 0
        for song in songs {
            guard let key = song.offlineSongKey(connectionId: connection.id) else { continue }
            guard !desired.contains(key) else { continue }
            addTrackSelection(key.songPath, connectionId: connection.id)
            added += 1
        }
        if added > 0 { selectionsChanged() }
        return added
    }

    func removeArtistSelection(_ selection: OfflineArtistSelection) {
        guard let connectionId = currentConnection?.id else { return }
        localState.artists.removeAll {
            $0.connectionId == connectionId && stringsEqual($0.artist, selection.artist)
        }
        selectionsChanged()
    }

    func removeAlbumSelection(_ selection: OfflineAlbumSelection) {
        guard let connectionId = currentConnection?.id else { return }
        localState.albums.removeAll {
            $0.connectionId == connectionId
                && stringsEqual($0.artist, selection.artist)
                && stringsEqual($0.album, selection.album)
        }
        selectionsChanged()
    }

    func removeTrackSelection(_ selection: OfflineTrackSelection) {
        guard let connectionId = currentConnection?.id else { return }
        localState.tracks.removeAll {
            $0.connectionId == connectionId && songsEqual($0.songPath, selection.songPath)
        }
        selectionsChanged()
    }

    private func selectionsChanged() {
        // Invalidate before syncing: the desired-keys cache still reflects the
        // selections as they were before this mutation.
        invalidateStatusCaches()
        syncDesiredState()
    }

    private func addArtistSelection(_ artist: String, connectionId: UUID) {
        guard !localState.artists.contains(where: {
            $0.connectionId == connectionId && stringsEqual($0.artist, artist)
        }) else { return }
        localState.artists.append(StoredArtistSelection(connectionId: connectionId, artist: artist))
    }

    private func addAlbumSelection(artist: String, album: String, connectionId: UUID) {
        guard !localState.albums.contains(where: {
            $0.connectionId == connectionId && stringsEqual($0.artist, artist) && stringsEqual($0.album, album)
        }) else { return }
        localState.albums.append(StoredAlbumSelection(connectionId: connectionId, artist: artist, album: album))
    }

    private func addTrackSelection(_ songPath: String, connectionId: UUID) {
        guard !localState.tracks.contains(where: {
            $0.connectionId == connectionId && songsEqual($0.songPath, songPath)
        }) else { return }
        localState.tracks.append(StoredTrackSelection(connectionId: connectionId, songPath: songPath))
    }

    /// Removes every selection that keeps this artist's songs offline.
    private func removeArtistCoverage(artist: String, songs: [Song], connectionId: UUID) {
        localState.artists.removeAll { $0.connectionId == connectionId && stringsEqual($0.artist, artist) }
        localState.albums.removeAll { $0.connectionId == connectionId && stringsEqual($0.artist, artist) }
        removeTrackSelections(coveringAnyOf: songs, connectionId: connectionId)
    }

    /// Removes every selection that keeps this album's songs offline. A
    /// whole-artist selection covering the album is split: it is replaced by
    /// album selections for the artist's other albums so they stay offline.
    private func removeAlbumCoverage(artist: String, album: String, songs: [Song], connectionId: UUID) {
        localState.albums.removeAll {
            $0.connectionId == connectionId && stringsEqual($0.artist, artist) && stringsEqual($0.album, album)
        }
        removeTrackSelections(coveringAnyOf: songs, connectionId: connectionId)

        guard localState.artists.contains(where: {
            $0.connectionId == connectionId && stringsEqual($0.artist, artist)
        }) else { return }

        localState.artists.removeAll { $0.connectionId == connectionId && stringsEqual($0.artist, artist) }
        let otherAlbums = Set(librarySongs(forArtist: artist).map(\.displayAlbum))
            .filter { !stringsEqual($0, album) }
        for otherAlbum in otherAlbums.sorted() {
            addAlbumSelection(artist: artist, album: otherAlbum, connectionId: connectionId)
        }
    }

    /// Removes every selection that keeps this song offline. Covering album or
    /// artist selections are split so their other songs stay offline.
    private func removeTrackCoverage(song: Song, connectionId: UUID) {
        guard let path = song.path else { return }
        localState.tracks.removeAll { $0.connectionId == connectionId && songsEqual($0.songPath, path) }

        let artist = song.displayArtist
        let album = song.displayAlbum
        var mustSplitAlbum = false

        if localState.albums.contains(where: {
            $0.connectionId == connectionId && stringsEqual($0.artist, artist) && stringsEqual($0.album, album)
        }) {
            localState.albums.removeAll {
                $0.connectionId == connectionId && stringsEqual($0.artist, artist) && stringsEqual($0.album, album)
            }
            mustSplitAlbum = true
        }

        if localState.artists.contains(where: {
            $0.connectionId == connectionId && stringsEqual($0.artist, artist)
        }) {
            localState.artists.removeAll { $0.connectionId == connectionId && stringsEqual($0.artist, artist) }
            let otherAlbums = Set(librarySongs(forArtist: artist).map(\.displayAlbum))
                .filter { !stringsEqual($0, album) }
            for otherAlbum in otherAlbums.sorted() {
                addAlbumSelection(artist: artist, album: otherAlbum, connectionId: connectionId)
            }
            mustSplitAlbum = true
        }

        if mustSplitAlbum {
            for other in librarySongs(forArtist: artist, album: album) {
                guard let otherPath = other.path, !songsEqual(otherPath, path) else { continue }
                addTrackSelection(otherPath, connectionId: connectionId)
            }
        }
    }

    private func removeTrackSelections(coveringAnyOf songs: [Song], connectionId: UUID) {
        let paths = songs.compactMap(\.path)
        localState.tracks.removeAll { selection in
            selection.connectionId == connectionId && paths.contains { songsEqual($0, selection.songPath) }
        }
    }

    func retryFailedDownloads() {
        failedMessages.removeAll()
        syncDesiredState()
    }

    func retry(_ song: OfflineManagedSong) {
        failedMessages.removeValue(forKey: song.key)
        if !queuedKeys.contains(song.key) {
            queuedKeys.append(song.key)
            queuedKeySetCache = nil
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
        if queuedKeySet.contains(key) { return .queued }
        if downloadKeys.contains(key) && hasDownloadedFile(for: key) {
            return .downloaded
        }
        if failedMessages[key] != nil { return .failed }
        if desiredSongKeys(for: key.connectionId).contains(key) { return .pending }
        return .notTracked
    }

    private func desiredSongKeys(for connectionId: UUID) -> Set<OfflineSongKey> {
        guard currentConnection?.id == connectionId else { return [] }
        if let cached = desiredKeysCache[connectionId] { return cached }

        var keys = Set<OfflineSongKey>()
        let artists = localState.artists.filter { $0.connectionId == connectionId }
        let albums = localState.albums.filter { $0.connectionId == connectionId }

        if !artists.isEmpty || !albums.isEmpty {
            for song in librarySongs {
                guard let path = song.path, !path.isEmpty else { continue }
                let covered = artists.contains { stringsEqual($0.artist, song.displayArtist) }
                    || albums.contains {
                        stringsEqual($0.artist, song.displayArtist) && stringsEqual($0.album, song.displayAlbum)
                    }
                if covered {
                    keys.insert(OfflineSongKey(connectionId: connectionId, songPath: path))
                }
            }
        }

        for track in localState.tracks where track.connectionId == connectionId && !track.songPath.isEmpty {
            keys.insert(OfflineSongKey(connectionId: connectionId, songPath: track.songPath))
        }

        desiredKeysCache[connectionId] = keys
        return keys
    }

    private func syncDesiredState() {
        repairDownloadedRecords()
        guard let connection = currentConnection else {
            persistManifest()
            return
        }
        // Never prune files before the library has loaded — an empty library
        // would make every download look undesired and delete them all.
        guard !librarySongs.isEmpty else {
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

        // Sets computed once up front - the naive per-key linear scans made
        // this loop quadratic in the number of selected songs.
        let existingDownloadKeys = Set(localState.downloads.map(\.key))
        var queued = Set(queuedKeys)
        let libraryPaths = Set(librarySongs.compactMap { $0.path?.lowercased() })
        for key in desired {
            guard existingDownloadKeys.contains(key) == false else { continue }
            guard activeDownloadKey != key else { continue }
            guard queued.contains(key) == false else { continue }
            guard libraryPaths.contains(key.songPath.lowercased()) else { continue }
            queuedKeys.append(key)
            queued.insert(key)
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
        queuedKeySetCache = nil
        activeDownloadKey = first
        return first
    }

    private func downloadSong(for key: OfflineSongKey) async throws {
        guard let connection = currentConnection, connection.id == key.connectionId else {
            throw OfflineDownloadError.noConnection
        }
        guard let song = librarySong(forPath: key.songPath) else {
            throw OfflineDownloadError.songUnavailable
        }

        let api = APIClient(baseURL: connection.baseURL)
        let downloadURL = api.offlineSongMediaURL(songPath: key.songPath)
        let (tempURL, response) = try await URLSession.shared.download(from: downloadURL)

        guard let http = response as? HTTPURLResponse else {
            throw OfflineDownloadError.invalidResponse
        }
        guard (200..<300).contains(http.statusCode) else {
            throw APIError.serverError(http.statusCode, "Download failed")
        }

        let targetURL = OfflineDownloadPaths.plannedFileURL(for: song, connectionId: connection.id)
        if FileManager.default.fileExists(atPath: targetURL.path) {
            try? FileManager.default.removeItem(at: targetURL)
        }
        try FileManager.default.moveItem(at: tempURL, to: targetURL)

        let record = OfflineDownloadRecord(
            key: key,
            title: song.displayTitle,
            artist: song.displayArtist,
            album: song.displayAlbum,
            addedAt: Date()
        )

        localState.downloads.removeAll { $0.key == key }
        localState.downloads.append(record)
    }

    private func repairDownloadedRecords() {
        let originalCount = localState.downloads.count
        localState.downloads.removeAll {
            !$0.key.songPath.isEmpty && !hasDownloadedFile(for: $0.key)
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

    /// Upgrade path from the versions that kept selections on the server:
    /// every song already downloaded to this device becomes a local track
    /// selection, so nothing on the phone is deleted by the switch to
    /// device-local selections.
    private func seedSelectionsFromDownloadsIfNeeded() {
        guard localState.selectionsSeeded != true else { return }

        for record in localState.downloads where !record.key.songPath.isEmpty {
            let alreadySelected = localState.tracks.contains {
                $0.connectionId == record.key.connectionId && songsEqual($0.songPath, record.key.songPath)
            }
            if !alreadySelected {
                localState.tracks.append(
                    StoredTrackSelection(connectionId: record.key.connectionId, songPath: record.key.songPath)
                )
            }
        }

        localState.selectionsSeeded = true
        persistManifest()
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
        var hash: UInt64 = Self.fnv1aOffsetBasis
        for byte in value.utf8 {
            hash ^= UInt64(byte)
            hash &*= Self.fnv1aPrime
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

    private func loadManifest() {
        guard let data = try? Data(contentsOf: manifestURL),
              let decoded = try? JSONDecoder().decode(OfflineLocalState.self, from: data) else {
            localState = OfflineLocalState()
            return
        }

        localState = decoded
    }

    private func persistManifest() {
        // persistManifest follows every mutation of downloads/queue/manifest
        // state, so it doubles as the cache-invalidation chokepoint.
        invalidateStatusCaches()
        let encoder = JSONEncoder()
        encoder.outputFormatting = [.prettyPrinted, .sortedKeys]
        guard let data = try? encoder.encode(localState) else { return }
        try? data.write(to: manifestURL, options: .atomic)
    }
}
