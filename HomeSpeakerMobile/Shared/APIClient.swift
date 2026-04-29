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

class APIClient {
    let baseURL: URL
    private let session: URLSession
    private let decoder = JSONDecoder()
    private let encoder = JSONEncoder()

    init(baseURL: URL) {
        self.baseURL = baseURL
        let config = URLSessionConfiguration.default
        config.timeoutIntervalForRequest = 10
        self.session = URLSession(configuration: config)
    }

    private func url(_ path: String) -> URL {
        let base = baseURL.absoluteString.hasSuffix("/")
            ? String(baseURL.absoluteString.dropLast())
            : baseURL.absoluteString
        return URL(string: "\(base)/\(path)")!
    }

    private func request<T: Decodable>(_ path: String, method: String = "GET", body: (any Encodable)? = nil) async throws -> T {
        var req = URLRequest(url: url(path))
        req.httpMethod = method
        if let body {
            req.httpBody = try encoder.encode(body)
            req.setValue("application/json", forHTTPHeaderField: "Content-Type")
        }
        let (data, response) = try await session.data(for: req)
        guard let http = response as? HTTPURLResponse else {
            throw APIError.networkError(URLError(.badServerResponse))
        }
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

    private func requestVoid(_ path: String, method: String = "GET", body: (any Encodable)? = nil) async throws {
        var req = URLRequest(url: url(path))
        req.httpMethod = method
        if let body {
            req.httpBody = try encoder.encode(body)
            req.setValue("application/json", forHTTPHeaderField: "Content-Type")
        }
        let (_, response) = try await session.data(for: req)
        guard let http = response as? HTTPURLResponse else {
            throw APIError.networkError(URLError(.badServerResponse))
        }
        guard (200..<300).contains(http.statusCode) else {
            throw APIError.serverError(http.statusCode, "Request failed")
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

    func enqueueSong(_ songId: Int) async throws {
        try await requestVoid("api/homespeaker/songs/\(songId)/enqueue", method: "POST")
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

    func faviconURL(for fileName: String) -> URL {
        url("favicons/\(fileName)")
    }
}
