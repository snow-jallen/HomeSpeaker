import AppIntents

// MARK: - Play Artist

struct PlayArtistOnHomeSpeakerIntent: AppIntent {
    static var title: LocalizedStringResource = "Play Artist on HomeSpeaker"
    static var description = IntentDescription("Shuffle all songs by an artist on a HomeSpeaker server.")
    static var openAppWhenRun: Bool = true

    @Parameter(title: "Artist")
    var artist: MediaQueryEntity

    @Parameter(title: "Server")
    var server: ServerEntity?

    func perform() async throws -> some IntentResult & ProvidesDialog {
        let (contentName, extractedServer) = parseContentAndServer(artist.id)
        let conn: ServerConnection
        if let explicit = server { conn = try resolveServer(explicit) }
        else if let extracted = extractedServer { conn = extracted }
        else { conn = try resolveServer(nil) }
        UserDefaults.standard.set(conn.id.uuidString, forKey: "hs_selectedId")
        let api = APIClient(baseURL: conn.baseURL)
        try await api.playArtist(contentName)
        try await api.shuffleQueue()
        return .result(dialog: "Shuffling \(contentName) on \(conn.name).")
    }
}

// MARK: - Play Album

struct PlayAlbumOnHomeSpeakerIntent: AppIntent {
    static var title: LocalizedStringResource = "Play Album on HomeSpeaker"
    static var description = IntentDescription("Play an album on a HomeSpeaker server.")
    static var openAppWhenRun: Bool = true

    @Parameter(title: "Album")
    var album: MediaQueryEntity

    @Parameter(title: "Server")
    var server: ServerEntity?

    func perform() async throws -> some IntentResult & ProvidesDialog {
        let (contentName, extractedServer) = parseContentAndServer(album.id)
        let conn: ServerConnection
        if let explicit = server { conn = try resolveServer(explicit) }
        else if let extracted = extractedServer { conn = extracted }
        else { conn = try resolveServer(nil) }
        UserDefaults.standard.set(conn.id.uuidString, forKey: "hs_selectedId")
        let api = APIClient(baseURL: conn.baseURL)
        try await api.playAlbum(contentName)
        return .result(dialog: "Playing \(contentName) on \(conn.name).")
    }
}

// MARK: - Play Playlist

struct PlayPlaylistOnHomeSpeakerIntent: AppIntent {
    static var title: LocalizedStringResource = "Play Playlist on HomeSpeaker"
    static var description = IntentDescription("Play a playlist on a HomeSpeaker server.")
    static var openAppWhenRun: Bool = true

    @Parameter(title: "Playlist")
    var playlist: MediaQueryEntity

    @Parameter(title: "Server")
    var server: ServerEntity?

    func perform() async throws -> some IntentResult & ProvidesDialog {
        let (contentName, extractedServer) = parseContentAndServer(playlist.id)
        let conn: ServerConnection
        if let explicit = server { conn = try resolveServer(explicit) }
        else if let extracted = extractedServer { conn = extracted }
        else { conn = try resolveServer(nil) }
        UserDefaults.standard.set(conn.id.uuidString, forKey: "hs_selectedId")
        let api = APIClient(baseURL: conn.baseURL)
        let playlists = try await api.getPlaylists()
        guard let match = bestMatch(from: playlists, query: contentName, key: { $0.name }) else {
            throw HomeSpeakerIntentError.contentNotFound(contentName)
        }
        try await api.playPlaylist(name: match.name)
        return .result(dialog: "Playing \(match.name) on \(conn.name).")
    }
}

// MARK: - Play AI Playlist

struct PlayAiPlaylistOnHomeSpeakerIntent: AppIntent {
    static var title: LocalizedStringResource = "Play AI Playlist on HomeSpeaker"
    static var description = IntentDescription("Play an AI-generated playlist on a HomeSpeaker server.")
    static var openAppWhenRun: Bool = true

    @Parameter(title: "Genre or Style")
    var genre: MediaQueryEntity

    @Parameter(title: "Server")
    var server: ServerEntity?

    func perform() async throws -> some IntentResult & ProvidesDialog {
        let (contentName, extractedServer) = parseContentAndServer(genre.id)
        let conn: ServerConnection
        if let explicit = server { conn = try resolveServer(explicit) }
        else if let extracted = extractedServer { conn = extracted }
        else { conn = try resolveServer(nil) }
        UserDefaults.standard.set(conn.id.uuidString, forKey: "hs_selectedId")
        let api = APIClient(baseURL: conn.baseURL)
        let playlists = try await api.getAiPlaylists()
        let match = bestMatch(from: playlists, query: contentName, key: { $0.displayName })
            ?? bestMatch(from: playlists, query: contentName, key: { $0.genreKey })
        guard let match else {
            throw HomeSpeakerIntentError.contentNotFound(contentName)
        }
        try await api.playAiPlaylist(genreKey: match.genreKey)
        return .result(dialog: "Playing \(match.displayName) on \(conn.name).")
    }
}

// MARK: - Play Stream

struct PlayStreamOnHomeSpeakerIntent: AppIntent {
    static var title: LocalizedStringResource = "Stream Radio on HomeSpeaker"
    static var description = IntentDescription("Play a radio stream on a HomeSpeaker server.")
    static var openAppWhenRun: Bool = true

    @Parameter(title: "Stream Name")
    var streamName: MediaQueryEntity

    @Parameter(title: "Server")
    var server: ServerEntity?

    func perform() async throws -> some IntentResult & ProvidesDialog {
        let (contentName, extractedServer) = parseContentAndServer(streamName.id)
        let conn: ServerConnection
        if let explicit = server { conn = try resolveServer(explicit) }
        else if let extracted = extractedServer { conn = extracted }
        else { conn = try resolveServer(nil) }
        UserDefaults.standard.set(conn.id.uuidString, forKey: "hs_selectedId")
        let api = APIClient(baseURL: conn.baseURL)
        let streams = try await api.getRadioStreams()
        guard let match = bestMatch(from: streams, query: contentName, key: { $0.name }) else {
            throw HomeSpeakerIntentError.contentNotFound(contentName)
        }
        try await api.playRadioStream(id: match.id)
        return .result(dialog: "Streaming \(match.name) on \(conn.name).")
    }
}
