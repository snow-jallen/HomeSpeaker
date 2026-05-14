import AppIntents

private func resolvedConnectionAndAPI() throws -> (ServerConnection, APIClient) {
    let connection = try resolveServer(nil)
    UserDefaults.standard.set(connection.id.uuidString, forKey: "hs_selectedId")
    return (connection, APIClient(baseURL: connection.baseURL))
}

private func normalizedShortcutPhrase(_ value: String) -> String {
    value
        .lowercased()
        .replacingOccurrences(of: "[^a-z0-9]+", with: " ", options: .regularExpression)
        .trimmingCharacters(in: .whitespacesAndNewlines)
}

private func matchesShortcutAlias(_ value: String, aliases: [String]) -> Bool {
    let normalizedValue = normalizedShortcutPhrase(value)
    return aliases.map(normalizedShortcutPhrase).contains(normalizedValue)
}

private func matchedAiPlaylist(
    from playlists: [AiPlaylistSummaryDto],
    aliases: [String]
) -> AiPlaylistSummaryDto? {
    playlists.first { playlist in
        aliases.contains { alias in
            matchesShortcutAlias(playlist.displayName, aliases: [alias]) ||
            matchesShortcutAlias(playlist.genreKey, aliases: [alias])
        }
    }
}

private func matchedPlaylist(from playlists: [Playlist], aliases: [String]) -> Playlist? {
    playlists.first { playlist in
        aliases.contains { alias in matchesShortcutAlias(playlist.name, aliases: [alias]) }
    }
}

private func playNamedMix(
    userFacingName: String,
    aliases: [String],
    api: APIClient
) async throws -> String {
    if let match = matchedAiPlaylist(from: (try? await api.getAiPlaylists()) ?? [], aliases: aliases) {
        try await api.playAiPlaylist(genreKey: match.genreKey)
        return match.displayName
    }

    if let match = matchedPlaylist(from: (try? await api.getPlaylists()) ?? [], aliases: aliases) {
        try await api.playPlaylist(name: match.name)
        return match.name
    }

    throw HomeSpeakerIntentError.contentNotFound(userFacingName)
}

// MARK: - Focused Siri Controls

struct NextSongOnHomeSpeakerIntent: AppIntent {
    static var title: LocalizedStringResource = "HomeSpeaker Next Song"
    static var description = IntentDescription("Skip to the next song on the selected HomeSpeaker server.")
    static var openAppWhenRun: Bool = false

    func perform() async throws -> some IntentResult & ProvidesDialog {
        let (connection, api) = try resolvedConnectionAndAPI()
        try await api.skipToNext()
        return .result(dialog: "Skipping to the next song on \(connection.name).")
    }
}

struct PlayFunMusicOnHomeSpeakerIntent: AppIntent {
    static var title: LocalizedStringResource = "Play Fun Music on HomeSpeaker"
    static var description = IntentDescription("Start the Fun Music mix on the selected HomeSpeaker server.")
    static var openAppWhenRun: Bool = false

    func perform() async throws -> some IntentResult & ProvidesDialog {
        let (connection, api) = try resolvedConnectionAndAPI()
        try await api.playAiPlaylist(genreKey: "family-singalong")
        return .result(dialog: "Playing Fun Music on \(connection.name).")
    }
}

struct PlayHymnsOnHomeSpeakerIntent: AppIntent {
    static var title: LocalizedStringResource = "Play Hymns on HomeSpeaker"
    static var description = IntentDescription("Start the Hymns mix on the selected HomeSpeaker server.")
    static var openAppWhenRun: Bool = false

    func perform() async throws -> some IntentResult & ProvidesDialog {
        let (connection, api) = try resolvedConnectionAndAPI()
        let playlistName = try await playNamedMix(
            userFacingName: "hymns",
            aliases: ["hymns", "hymn"],
            api: api
        )
        return .result(dialog: "Playing \(playlistName) on \(connection.name).")
    }
}

struct QuietDownOnHomeSpeakerIntent: AppIntent {
    static var title: LocalizedStringResource = "Quiet Down HomeSpeaker"
    static var description = IntentDescription("Cut the current HomeSpeaker volume in half on the selected server.")
    static var openAppWhenRun: Bool = false

    func perform() async throws -> some IntentResult & ProvidesDialog {
        let (connection, api) = try resolvedConnectionAndAPI()
        let status = try await api.getPlayerStatus()
        let newVolume = status.quietDownVolume
        try await api.setVolume(newVolume)
        return .result(dialog: "Volume reduced to \(newVolume) on \(connection.name).")
    }
}

struct StopHomeSpeakerIntent: AppIntent {
    static var title: LocalizedStringResource = "Stop HomeSpeaker"
    static var description = IntentDescription("Stop playback on the selected HomeSpeaker server.")
    static var openAppWhenRun: Bool = false

    func perform() async throws -> some IntentResult & ProvidesDialog {
        let (connection, api) = try resolvedConnectionAndAPI()
        try await api.stop()
        return .result(dialog: "Stopped \(connection.name).")
    }
}

// MARK: - Legacy Generic Intents

struct PlayArtistOnHomeSpeakerIntent: AppIntent {
    static var title: LocalizedStringResource = "Play Artist on HomeSpeaker"
    static var description = IntentDescription("Shuffle all songs by an artist on a HomeSpeaker server.")
    static var openAppWhenRun: Bool = true
    static var isDiscoverable: Bool = false

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
    static var isDiscoverable: Bool = false

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
    static var isDiscoverable: Bool = false

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
    static var isDiscoverable: Bool = false

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
    static var isDiscoverable: Bool = false

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
