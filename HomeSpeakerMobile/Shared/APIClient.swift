import Foundation

enum APIError: Error, LocalizedError {
    case invalidURL
    case networkError(Error)
    case serverError(Int, String)
    case decodingError(Error)

    var errorDescription: String? {
        switch self {
        case .invalidURL: return "Invalid URL"
        case .networkError(let e): return "Network error: \(e.localizedDescription)"
        case .serverError(let code, let msg): return "Server error \(code): \(msg)"
        case .decodingError(let e): return "Decode error: \(e.localizedDescription)"
        }
    }
}

final class APIClient: @unchecked Sendable {
    /// Addresses for this server, home address first. Requests go to the one
    /// that last worked and fall back to the others when it can't be reached.
    let candidateBaseURLs: [URL]
    private let preferenceKey: String?
    private let session: URLSession
    private let streamingSession: URLSession
    private let decoder = JSONDecoder()
    private let encoder = JSONEncoder()
    private let lock = NSLock()
    private var preferredIndex = 0
    private var lastSuccessAt: Date?

    /// The address requests currently go to.
    var baseURL: URL {
        lock.withLock { candidateBaseURLs[preferredIndex] }
    }

    convenience init(baseURL: URL) {
        self.init(candidates: [baseURL], preferenceKey: nil)
    }

    convenience init(connection: ServerConnection) {
        self.init(candidates: connection.candidateBaseURLs,
                  preferenceKey: "hs_preferredBase_\(connection.id.uuidString)")
    }

    private init(candidates: [URL], preferenceKey: String?) {
        self.candidateBaseURLs = candidates
        self.preferenceKey = preferenceKey
        let config = URLSessionConfiguration.default
        config.timeoutIntervalForRequest = 10
        self.session = URLSession(configuration: config)
        // The status stream sends a heartbeat every 10 s, so allow a longer
        // idle gap before treating it as dropped.
        let streamingConfig = URLSessionConfiguration.default
        streamingConfig.timeoutIntervalForRequest = 30
        streamingConfig.timeoutIntervalForResource = 60 * 60 * 24
        self.streamingSession = URLSession(configuration: streamingConfig)
        if let preferenceKey {
            let saved = SharedConnections.defaults.integer(forKey: preferenceKey)
            preferredIndex = candidates.indices.contains(saved) ? saved : 0
        }
    }

    // MARK: - Shared clients

    private static let cacheLock = NSLock()
    nonisolated(unsafe) private static var cache: [UUID: (connection: ServerConnection, client: APIClient)] = [:]

    /// One client per server, rebuilt only when the server's addresses change.
    static func client(for connection: ServerConnection) -> APIClient {
        cacheLock.withLock {
            if let cached = cache[connection.id], cached.connection.candidateBaseURLs == connection.candidateBaseURLs {
                return cached.client
            }
            let client = APIClient(connection: connection)
            cache[connection.id] = (connection, client)
            return client
        }
    }

    // MARK: - URLs

    private func url(_ path: String, base: URL) -> URL {
        let baseString = base.absoluteString.hasSuffix("/")
            ? String(base.absoluteString.dropLast())
            : base.absoluteString
        return URL(string: "\(baseString)/\(path)")!
    }

    private func url(_ path: String) -> URL {
        url(path, base: baseURL)
    }

    func resolveURL(_ pathOrURL: String) -> URL {
        if let absoluteURL = URL(string: pathOrURL), absoluteURL.scheme != nil {
            return absoluteURL
        }

        let normalizedPath = pathOrURL.hasPrefix("/")
            ? String(pathOrURL.dropFirst())
            : pathOrURL
        return url(normalizedPath)
    }

    // MARK: - Transport

    private func attemptOrder() -> [Int] {
        let preferred = lock.withLock { preferredIndex }
        return [preferred] + candidateBaseURLs.indices.filter { $0 != preferred }
    }

    private func markWorking(_ index: Int) {
        let changed = lock.withLock { () -> Bool in
            lastSuccessAt = .now
            guard preferredIndex != index else { return false }
            preferredIndex = index
            return true
        }
        if changed, let preferenceKey {
            SharedConnections.defaults.set(index, forKey: preferenceKey)
        }
    }

    /// Errors meaning this address can't be reached, so another might work.
    /// A timeout or dropped connection only counts for GETs: a command that
    /// failed that way may still have landed (skipping twice would be wrong).
    private static func isUnreachable(_ error: URLError, method: String) -> Bool {
        switch error.code {
        case .cannotFindHost, .cannotConnectToHost, .dnsLookupFailed,
             .secureConnectionFailed,
             .serverCertificateUntrusted, .serverCertificateHasBadDate,
             .serverCertificateNotYetValid, .serverCertificateHasUnknownRoot,
             .appTransportSecurityRequiresSecureConnection:
            return true
        case .timedOut, .networkConnectionLost:
            return method == "GET"
        default:
            return false
        }
    }

    /// Commands can't safely be retried on a timeout, so before sending one
    /// with an unconfirmed address (e.g. a Lock Screen tap after leaving home
    /// Wi-Fi), find a reachable address with a quick read first. Reads also
    /// re-check now and then whether the home address works again.
    private func shouldProbe(method: String) -> Bool {
        guard candidateBaseURLs.count > 1 else { return false }
        return lock.withLock {
            let age = lastSuccessAt.map { Date.now.timeIntervalSince($0) } ?? .infinity
            if method != "GET" { return age > 60 }
            return preferredIndex != 0 && age > 5 * 60
        }
    }

    private func probe() async {
        // Home first, so the app goes back to it once it's reachable again.
        for index in candidateBaseURLs.indices {
            var req = URLRequest(url: url("api/features", base: candidateBaseURLs[index]))
            req.timeoutInterval = 3
            if let result = try? await session.data(for: req), result.1 is HTTPURLResponse {
                markWorking(index)
                return
            }
        }
    }

    private func send(
        _ path: String,
        method: String,
        body: Data?,
        contentType: String?
    ) async throws -> (Data, HTTPURLResponse) {
        if shouldProbe(method: method) {
            await probe()
        }
        let order = attemptOrder()
        for (attempt, index) in order.enumerated() {
            var req = URLRequest(url: url(path, base: candidateBaseURLs[index]))
            req.httpMethod = method
            if let body {
                req.httpBody = body
                if let contentType {
                    req.setValue(contentType, forHTTPHeaderField: "Content-Type")
                }
            }
            do {
                let (data, response) = try await session.data(for: req)
                guard let http = response as? HTTPURLResponse else {
                    throw APIError.networkError(URLError(.badServerResponse))
                }
                markWorking(index)
                return (data, http)
            } catch let error as URLError
                where attempt < order.count - 1 && Self.isUnreachable(error, method: method) {
                continue
            }
        }
        // The loop always returns or throws on its last attempt.
        throw APIError.networkError(URLError(.cannotConnectToHost))
    }

    private func request<T: Decodable>(_ path: String, method: String = "GET", body: (any Encodable)? = nil) async throws -> T {
        let (data, http) = try await send(path, method: method,
                                          body: try body.map { try encoder.encode($0) },
                                          contentType: "application/json")
        guard (200..<300).contains(http.statusCode) else {
            let msg = String(data: data, encoding: .utf8) ?? "Unknown error"
            throw APIError.serverError(http.statusCode, msg)
        }
        do {
            return try decoder.decode(T.self, from: data)
        } catch {
            throw APIError.decodingError(error)
        }
    }

    private func requestVoidRaw(_ path: String, method: String, body: Data, contentType: String) async throws {
        let (_, http) = try await send(path, method: method, body: body, contentType: contentType)
        guard (200..<300).contains(http.statusCode) else {
            throw APIError.serverError(http.statusCode, "Request failed")
        }
    }

    private func requestVoid(_ path: String, method: String = "GET", body: (any Encodable)? = nil) async throws {
        let (_, http) = try await send(path, method: method,
                                       body: try body.map { try encoder.encode($0) },
                                       contentType: "application/json")
        guard (200..<300).contains(http.statusCode) else {
            throw APIError.serverError(http.statusCode, "Request failed")
        }
    }

    /// Fetches raw bytes (e.g. album art), or nil when there are none.
    func data(at url: URL) async -> Data? {
        guard let result = try? await session.data(from: url),
              let http = result.1 as? HTTPURLResponse,
              (200..<300).contains(http.statusCode),
              !result.0.isEmpty else { return nil }
        return result.0
    }

    // MARK: - Live status

    /// Player status pushed by the server as it changes (server-sent events),
    /// plus a heartbeat every 10 s. Finishes or throws when the connection
    /// drops; servers without the endpoint throw straight away, so callers
    /// should fall back to polling `getPlayerStatus()`.
    func statusUpdates() -> AsyncThrowingStream<PlayerStatus, Error> {
        let request = URLRequest(url: url("api/homespeaker/player/events"))
        let session = streamingSession
        let decoder = decoder
        return AsyncThrowingStream { continuation in
            let task = Task {
                do {
                    let (bytes, response) = try await session.bytes(for: request)
                    guard let http = response as? HTTPURLResponse, http.statusCode == 200 else {
                        throw APIError.serverError((response as? HTTPURLResponse)?.statusCode ?? 0, "No status stream")
                    }
                    // Each event is "event: status" then one "data: {json}" line.
                    for try await line in bytes.lines where line.hasPrefix("data:") {
                        let payload = line.dropFirst(5).trimmingCharacters(in: .whitespaces)
                        if let status = try? decoder.decode(PlayerStatus.self, from: Data(payload.utf8)) {
                            continuation.yield(status)
                        }
                    }
                    continuation.finish()
                } catch {
                    continuation.finish(throwing: error)
                }
            }
            continuation.onTermination = { _ in task.cancel() }
        }
    }

    // MARK: - Player

    func getPlayerStatus() async throws -> PlayerStatus {
        try await request("api/homespeaker/player/status")
    }

    func stop() async throws {
        try await requestVoid("api/homespeaker/player/control", method: "POST",
            body: PlayerControlRequest(stop: true, play: false, clearQueue: false, skipToNext: false, setVolume: false, volumeLevel: 0))
    }

    func resume() async throws {
        try await requestVoid("api/homespeaker/player/control", method: "POST",
            body: PlayerControlRequest(stop: false, play: true, clearQueue: false, skipToNext: false, setVolume: false, volumeLevel: 0))
    }

    func skipToNext() async throws {
        try await requestVoid("api/homespeaker/player/control", method: "POST",
            body: PlayerControlRequest(stop: false, play: false, clearQueue: false, skipToNext: true, setVolume: false, volumeLevel: 0))
    }

    func setVolume(_ level: Int) async throws {
        try await requestVoid("api/homespeaker/player/control", method: "POST",
            body: PlayerControlRequest(stop: false, play: false, clearQueue: false, skipToNext: false, setVolume: true, volumeLevel: level))
    }

    func setSleepTimer(minutes: Int) async throws {
        try await requestVoid("api/homespeaker/player/sleep", method: "POST",
            body: SetSleepTimerRequest(minutes: minutes))
    }

    func cancelSleepTimer() async throws {
        try await requestVoid("api/homespeaker/player/sleep", method: "DELETE")
    }

    func setRepeatMode(enabled: Bool) async throws {
        try await requestVoid("api/homespeaker/player/repeat", method: "PUT",
            body: SetRepeatModeRequest(enabled: enabled))
    }

    // MARK: - Songs

    func getSongs(folder: String? = nil) async throws -> [Song] {
        var path = "api/homespeaker/songs"
        if let folder, let encoded = folder.addingPercentEncoding(withAllowedCharacters: .urlQueryAllowed) {
            path += "?folder=\(encoded)"
        }
        return try await request(path)
    }

    func playSong(_ songId: Int) async throws {
        try await requestVoid("api/homespeaker/songs/\(songId)/play", method: "POST")
    }

    func musicFileURL(songId: Int) -> URL {
        url("api/music/\(songId)")
    }

    func offlineSongMediaURL(songPath: String) -> URL {
        let encodedSongPath = songPath.addingPercentEncoding(withAllowedCharacters: .urlQueryAllowed) ?? songPath
        return url("api/homespeaker/offline/media?songPath=\(encodedSongPath)")
    }

    func enqueueSong(_ songId: Int) async throws {
        try await requestVoid("api/homespeaker/songs/\(songId)/enqueue", method: "POST")
    }

    func enqueueArtist(_ artist: String) async throws {
        let enc = artist.addingPercentEncoding(withAllowedCharacters: .urlQueryAllowed) ?? artist
        try await requestVoid("api/homespeaker/songs/enqueue-by-artist?artist=\(enc)", method: "POST")
    }

    func playArtist(_ artist: String) async throws {
        let enc = artist.addingPercentEncoding(withAllowedCharacters: .urlQueryAllowed) ?? artist
        try await requestVoid("api/homespeaker/songs/play-by-artist?artist=\(enc)", method: "POST")
    }

    func enqueueAlbum(_ album: String) async throws {
        let enc = album.addingPercentEncoding(withAllowedCharacters: .urlQueryAllowed) ?? album
        try await requestVoid("api/homespeaker/songs/enqueue-by-album?album=\(enc)", method: "POST")
    }

    func playAlbum(_ album: String) async throws {
        let enc = album.addingPercentEncoding(withAllowedCharacters: .urlQueryAllowed) ?? album
        try await requestVoid("api/homespeaker/songs/play-by-album?album=\(enc)", method: "POST")
    }

    func albumArtURL(songId: Int) -> URL {
        url("api/homespeaker/songs/\(songId)/art")
    }

    func updateSong(id: Int, name: String, artist: String, album: String) async throws {
        try await requestVoid("api/homespeaker/songs/\(id)", method: "PUT",
            body: UpdateSongRequest(name: name, artist: artist, album: album))
    }

    func updateAlbumArt(album: String, imageData: Data) async throws {
        let enc = album.addingPercentEncoding(withAllowedCharacters: .urlQueryAllowed) ?? album
        try await requestVoidRaw("api/homespeaker/albums/art?album=\(enc)", method: "PUT",
            body: imageData, contentType: "image/jpeg")
    }

    // MARK: - Queue

    func getQueue() async throws -> [Song] {
        try await request("api/homespeaker/queue")
    }

    func shuffleQueue() async throws {
        try await requestVoid("api/homespeaker/queue/shuffle", method: "POST")
    }

    func clearQueue() async throws {
        try await requestVoid("api/homespeaker/queue", method: "DELETE")
    }

    func updateQueue(songPaths: [String]) async throws {
        try await requestVoid("api/homespeaker/queue", method: "PUT",
            body: UpdateQueueRequest(songs: songPaths))
    }

    // MARK: - Playlists

    func getPlaylists() async throws -> [Playlist] {
        try await request("api/homespeaker/playlists")
    }

    func playPlaylist(name: String) async throws {
        let enc = name.addingPercentEncoding(withAllowedCharacters: .urlPathAllowed) ?? name
        try await requestVoid("api/homespeaker/playlists/\(enc)/play", method: "POST")
    }

    func deletePlaylist(name: String) async throws {
        let enc = name.addingPercentEncoding(withAllowedCharacters: .urlPathAllowed) ?? name
        try await requestVoid("api/homespeaker/playlists/\(enc)", method: "DELETE")
    }

    func renamePlaylist(from oldName: String, to newName: String) async throws {
        let enc = oldName.addingPercentEncoding(withAllowedCharacters: .urlPathAllowed) ?? oldName
        try await requestVoid("api/homespeaker/playlists/\(enc)/rename", method: "PUT",
            body: RenamePlaylistRequest(newName: newName))
    }

    func addSongToPlaylist(playlistName: String, songPath: String) async throws {
        let enc = playlistName.addingPercentEncoding(withAllowedCharacters: .urlPathAllowed) ?? playlistName
        try await requestVoid("api/homespeaker/playlists/\(enc)/songs", method: "POST",
            body: AddSongRequest(songPath: songPath))
    }

    func removeSongFromPlaylist(playlistName: String, songPath: String) async throws {
        let enc = playlistName.addingPercentEncoding(withAllowedCharacters: .urlPathAllowed) ?? playlistName
        try await requestVoid("api/homespeaker/playlists/\(enc)/songs", method: "DELETE",
            body: RemoveSongRequest(songPath: songPath))
    }

    func reorderPlaylist(name: String, songPaths: [String]) async throws {
        let enc = name.addingPercentEncoding(withAllowedCharacters: .urlPathAllowed) ?? name
        try await requestVoid("api/homespeaker/playlists/\(enc)/reorder", method: "PUT",
            body: ReorderRequest(songPaths: songPaths))
    }

    // MARK: - Radio Streams

    func getRadioStreams() async throws -> [RadioStream] {
        try await request("api/homespeaker/radio")
    }

    func playRadioStream(id: Int) async throws {
        try await requestVoid("api/homespeaker/radio/\(id)/play", method: "POST")
    }

    func createRadioStream(name: String, url: String) async throws -> RadioStream {
        try await request("api/homespeaker/radio", method: "POST",
            body: CreateStreamRequest(name: name, url: url))
    }

    func updateRadioStream(id: Int, name: String, url: String) async throws -> RadioStream {
        try await request("api/homespeaker/radio/\(id)", method: "PUT",
            body: CreateStreamRequest(name: name, url: url))
    }

    func deleteRadioStream(id: Int) async throws {
        try await requestVoid("api/homespeaker/radio/\(id)", method: "DELETE")
    }

    // MARK: - YouTube

    func searchYouTube(_ query: String) async throws -> [VideoDto] {
        let enc = query.addingPercentEncoding(withAllowedCharacters: .urlQueryAllowed) ?? query
        return try await request("api/homespeaker/youtube/search?q=\(enc)")
    }

    func playYouTubeVideo(id: String, title: String) async throws {
        let enc = title.addingPercentEncoding(withAllowedCharacters: .urlQueryAllowed) ?? title
        try await requestVoid("api/homespeaker/youtube/\(id)/play?title=\(enc)", method: "POST")
    }

    func cacheYouTubeVideo(_ video: VideoDto) async throws {
        try await requestVoid("api/homespeaker/youtube/cache", method: "POST",
            body: CacheVideoRequest(video: video))
    }

    // MARK: - Misc

    func getRecentlyPlayed(limit: Int = 20) async throws -> [Song] {
        try await request("api/music/recently-played?limit=\(limit)")
    }

    func getFeatures() async throws -> Features {
        try await request("api/features")
    }

    func getTemperature() async throws -> TemperatureStatus {
        try await request("api/temperature")
    }

    // MARK: - Push Notifications

    func getPushRegistrationStatus(installationId: String) async throws -> PushRegistrationStatusDto {
        let encodedInstallationId = installationId.addingPercentEncoding(withAllowedCharacters: .urlPathAllowed) ?? installationId
        return try await request("api/homespeaker/push/installations/\(encodedInstallationId)")
    }

    func upsertPushInstallation(
        installationId: String,
        request: UpsertPushInstallationRequest
    ) async throws -> PushRegistrationStatusDto {
        let encodedInstallationId = installationId.addingPercentEncoding(withAllowedCharacters: .urlPathAllowed) ?? installationId
        return try await self.request("api/homespeaker/push/installations/\(encodedInstallationId)", method: "PUT", body: request)
    }

    func getForecast() async throws -> ForecastStatus {
        try await request("api/forecast")
    }

    func getBloodSugar() async throws -> BloodSugarStatus {
        try await request("api/bloodsugar")
    }

    func playStream(url: String) async throws {
        try await requestVoid("api/homespeaker/stream/play", method: "POST",
            body: PlayStreamRequest(streamUrl: url))
    }

    // MARK: - Live Activities

    /// Lets the server push playback changes to a Lock Screen Live Activity.
    func registerLiveActivity(id: String, pushToken: String) async throws {
        let enc = id.addingPercentEncoding(withAllowedCharacters: .urlPathAllowed) ?? id
        try await requestVoid("api/homespeaker/live-activities/\(enc)", method: "PUT",
            body: LiveActivityRegistrationRequest(pushToken: pushToken))
    }

    func unregisterLiveActivity(id: String) async throws {
        let enc = id.addingPercentEncoding(withAllowedCharacters: .urlPathAllowed) ?? id
        try await requestVoid("api/homespeaker/live-activities/\(enc)", method: "DELETE")
    }

    func faviconURL(for fileName: String) -> URL {
        url("favicons/\(fileName)")
    }
    
    // MARK: - Play by Name

    /// The playlist, stream, AI mix, artist, album or song a spoken name means, or nil when nothing matches.
    func findMedia(query: String, kind: String? = nil, artist: String? = nil) async throws -> MediaMatch? {
        try await mediaRequest("api/homespeaker/media/match", method: "GET", query: query, kind: kind, artist: artist)
    }

    /// Plays the best match for a spoken name in place of what's playing, or returns nil when nothing matches.
    func playMedia(query: String, kind: String? = nil, artist: String? = nil) async throws -> MediaMatch? {
        try await mediaRequest("api/homespeaker/media/play", method: "POST", query: query, kind: kind, artist: artist)
    }

    private func mediaRequest(_ path: String, method: String, query: String, kind: String?, artist: String?) async throws -> MediaMatch? {
        var components = URLComponents()
        components.queryItems = [
            URLQueryItem(name: "query", value: query),
            kind.map { URLQueryItem(name: "kind", value: $0) },
            artist.map { URLQueryItem(name: "artist", value: $0) },
        ].compactMap { $0 }
        // URLComponents leaves "+" alone, which servers read as a space.
        let encoded = (components.percentEncodedQuery ?? "").replacingOccurrences(of: "+", with: "%2B")
        do {
            let match: MediaMatch = try await request("\(path)?\(encoded)", method: method)
            return match
        } catch APIError.serverError(404, _) {
            return nil
        }
    }

    // MARK: - AI Playlists

    func getAiPlaylists() async throws -> [AiPlaylistSummaryDto] {
        try await request("api/ai/playlists")
    }
    
    func getAiPlaylist(genreKey: String) async throws -> AiPlaylistDto {
        let enc = genreKey.addingPercentEncoding(withAllowedCharacters: .urlPathAllowed) ?? genreKey
        return try await request("api/ai/playlists/\(enc)")
    }
    
    func playAiPlaylist(genreKey: String) async throws {
        let enc = genreKey.addingPercentEncoding(withAllowedCharacters: .urlPathAllowed) ?? genreKey
        try await requestVoid("api/ai/playlists/\(enc)/play", method: "POST")
    }
    
    func getAiStatus() async throws -> AiLibraryStatusDto {
        try await request("api/ai/status")
    }
    
    func resumeAiProcessing() async throws {
        try await requestVoid("api/ai/process/resume", method: "POST")
    }
    
    func sendAiFeedback(songId: Int, feedback: String, sessionId: String?) async throws {
        try await requestVoid("api/ai/feedback", method: "POST",
            body: AiFeedbackRequest(songId: songId, feedback: feedback, sessionId: sessionId))
    }
    
    func startAiAutoplayFromCurrent() async throws {
        try await requestVoid("api/ai/autoplay/from-current", method: "POST")
    }
}
