import AppIntents

/// The server an intent targets: the one named, else the app's selected server.
private func connectionAndAPI(_ server: ServerEntity?) throws -> (ServerConnection, APIClient) {
    let connection = try resolveServer(server)
    return (connection, APIClient.client(for: connection))
}

/// Legacy intents also make the server they acted on the app's selection.
private func selectAndConnect(_ connection: ServerConnection) -> APIClient {
    SharedConnections.setSelectedId(connection.id)
    return APIClient.client(for: connection)
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

/// Plays the AI mix matching `aliases`, else a regular playlist that does.
/// Servers without AI configured list every AI mix with no tracks, and playing
/// an empty mix is a 404, so empty mixes are skipped.
private func playNamedMix(
    userFacingName: String,
    aliases: [String],
    connection: ServerConnection,
    api: APIClient
) async throws -> String {
    let aiPlaylists = ((try? await api.getAiPlaylists()) ?? []).filter { $0.trackCount > 0 }
    if let match = matchedAiPlaylist(from: aiPlaylists, aliases: aliases) {
        do {
            try await api.playAiPlaylist(genreKey: match.genreKey)
            return match.displayName
        } catch APIError.serverError(404, _) {
            // The mix emptied since it was listed; try a regular playlist.
        }
    }

    if let match = matchedPlaylist(from: (try? await api.getPlaylists()) ?? [], aliases: aliases) {
        try await api.playPlaylist(name: match.name)
        return match.name
    }

    throw HomeSpeakerIntentError.mixUnavailable(userFacingName, server: connection.name)
}

// MARK: - Focused Siri Controls

struct NextSongOnHomeSpeakerIntent: AppIntent {
    static var title: LocalizedStringResource = "HomeSpeaker Next Song"
    static var description = IntentDescription("Skip to the next song on a HomeSpeaker server.")
    static var openAppWhenRun: Bool = false

    @Parameter(title: "Server")
    var server: ServerEntity?

    func perform() async throws -> some IntentResult & ProvidesDialog {
        let (connection, api) = try connectionAndAPI(server)
        try await api.skipToNext()
        await SpeakerSurfaces.refresh(after: connection)
        return .result(dialog: "Skipping to the next song on \(connection.name).")
    }
}

struct PlayFunMusicOnHomeSpeakerIntent: AppIntent {
    static var title: LocalizedStringResource = "Play Fun Music on HomeSpeaker"
    static var description = IntentDescription("Start the Fun Music mix on a HomeSpeaker server.")
    static var openAppWhenRun: Bool = false

    @Parameter(title: "Server")
    var server: ServerEntity?

    func perform() async throws -> some IntentResult & ProvidesDialog {
        let (connection, api) = try connectionAndAPI(server)
        let playlistName = try await playNamedMix(
            userFacingName: "Fun Music",
            aliases: ["family-singalong", "fun music", "fun"],
            connection: connection,
            api: api
        )
        await SpeakerSurfaces.refresh(after: connection)
        return .result(dialog: "Playing \(playlistName) on \(connection.name).")
    }
}

struct PlayHymnsOnHomeSpeakerIntent: AppIntent {
    static var title: LocalizedStringResource = "Play Hymns on HomeSpeaker"
    static var description = IntentDescription("Start the Hymns mix on a HomeSpeaker server.")
    static var openAppWhenRun: Bool = false

    @Parameter(title: "Server")
    var server: ServerEntity?

    func perform() async throws -> some IntentResult & ProvidesDialog {
        let (connection, api) = try connectionAndAPI(server)
        let playlistName = try await playNamedMix(
            userFacingName: "Hymns",
            aliases: ["hymns", "hymn", "Hymns for Home and Church"],
            connection: connection,
            api: api
        )
        await SpeakerSurfaces.refresh(after: connection)
        return .result(dialog: "Playing \(playlistName) on \(connection.name).")
    }
}

struct QuietDownOnHomeSpeakerIntent: AppIntent {
    static var title: LocalizedStringResource = "Quiet Down HomeSpeaker"
    static var description = IntentDescription("Cut a HomeSpeaker server's volume in half.")
    static var openAppWhenRun: Bool = false

    @Parameter(title: "Server")
    var server: ServerEntity?

    func perform() async throws -> some IntentResult & ProvidesDialog {
        let (connection, api) = try connectionAndAPI(server)
        let status = try await api.getPlayerStatus()
        let newVolume = status.quietDownVolume
        try await api.setVolume(newVolume)
        await SpeakerSurfaces.refresh(after: connection)
        return .result(dialog: "Volume reduced to \(newVolume) on \(connection.name).")
    }
}

struct StopHomeSpeakerIntent: AppIntent {
    static var title: LocalizedStringResource = "Stop HomeSpeaker"
    static var description = IntentDescription("Stop playback on a HomeSpeaker server.")
    static var openAppWhenRun: Bool = false

    @Parameter(title: "Server")
    var server: ServerEntity?

    func perform() async throws -> some IntentResult & ProvidesDialog {
        let (connection, api) = try connectionAndAPI(server)
        try await api.stop()
        await SpeakerSurfaces.refresh(after: connection)
        return .result(dialog: "Stopped \(connection.name).")
    }
}

struct ResumeHomeSpeakerIntent: AppIntent {
    static var title: LocalizedStringResource = "Resume HomeSpeaker"
    static var description = IntentDescription("Resume playback on a HomeSpeaker server.")
    static var openAppWhenRun: Bool = false

    @Parameter(title: "Server")
    var server: ServerEntity?

    func perform() async throws -> some IntentResult & ProvidesDialog {
        let (connection, api) = try connectionAndAPI(server)
        try await api.resume()
        await SpeakerSurfaces.refresh(after: connection)
        return .result(dialog: "Resuming \(connection.name).")
    }
}

struct SetVolumeOnHomeSpeakerIntent: AppIntent {
    static var title: LocalizedStringResource = "Set HomeSpeaker Volume"
    static var description = IntentDescription("Set the volume of a HomeSpeaker server from 0 to 100.")
    static var openAppWhenRun: Bool = false

    @Parameter(title: "Volume", inclusiveRange: (0, 100),
               requestValueDialog: IntentDialog("What volume, from 0 to 100?"))
    var level: Int

    @Parameter(title: "Server")
    var server: ServerEntity?

    func perform() async throws -> some IntentResult & ProvidesDialog {
        let (connection, api) = try connectionAndAPI(server)
        let clamped = min(100, max(0, level))
        try await api.setVolume(clamped)
        await SpeakerSurfaces.refresh(after: connection)
        return .result(dialog: "Volume set to \(clamped) on \(connection.name).")
    }
}

struct SetSleepTimerOnHomeSpeakerIntent: AppIntent {
    static var title: LocalizedStringResource = "Set HomeSpeaker Sleep Timer"
    static var description = IntentDescription("Stop a HomeSpeaker server after some minutes.")
    static var openAppWhenRun: Bool = false

    @Parameter(title: "Minutes", inclusiveRange: (1, 240),
               requestValueDialog: IntentDialog("How many minutes?"))
    var minutes: Int

    @Parameter(title: "Server")
    var server: ServerEntity?

    func perform() async throws -> some IntentResult & ProvidesDialog {
        let (connection, api) = try connectionAndAPI(server)
        let clamped = min(240, max(1, minutes))
        try await api.setSleepTimer(minutes: clamped)
        await SpeakerSurfaces.refresh(after: connection)
        return .result(dialog: "\(connection.name) will stop in \(clamped) minutes.")
    }
}

struct CancelSleepTimerOnHomeSpeakerIntent: AppIntent {
    static var title: LocalizedStringResource = "Cancel HomeSpeaker Sleep Timer"
    static var description = IntentDescription("Cancel a HomeSpeaker server's sleep timer.")
    static var openAppWhenRun: Bool = false

    @Parameter(title: "Server")
    var server: ServerEntity?

    func perform() async throws -> some IntentResult & ProvidesDialog {
        let (connection, api) = try connectionAndAPI(server)
        try await api.cancelSleepTimer()
        await SpeakerSurfaces.refresh(after: connection)
        return .result(dialog: "Sleep timer cancelled on \(connection.name).")
    }
}

struct WhatsPlayingOnHomeSpeakerIntent: AppIntent {
    static var title: LocalizedStringResource = "What's Playing on HomeSpeaker"
    static var description = IntentDescription("Say what a HomeSpeaker server is playing.")
    static var openAppWhenRun: Bool = false

    @Parameter(title: "Server")
    var server: ServerEntity?

    func perform() async throws -> some IntentResult & ProvidesDialog & ReturnsValue<String> {
        let (connection, api) = try connectionAndAPI(server)
        let status = try await api.getPlayerStatus()
        guard status.isPlaying else {
            return .result(value: "", dialog: "Nothing is playing on \(connection.name).")
        }
        let description: String
        if let song = status.currentSong {
            description = song.artist.map { "\(song.displayTitle) by \($0)" } ?? song.displayTitle
        } else {
            description = status.displayTitle
        }
        return .result(value: description, dialog: "\(connection.name) is playing \(description).")
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
        let api = selectAndConnect(conn)
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
        let api = selectAndConnect(conn)
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
        let api = selectAndConnect(conn)
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
        let api = selectAndConnect(conn)
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
        let api = selectAndConnect(conn)
        let streams = try await api.getRadioStreams()
        guard let match = bestMatch(from: streams, query: contentName, key: { $0.name }) else {
            throw HomeSpeakerIntentError.contentNotFound(contentName)
        }
        try await api.playRadioStream(id: match.id)
        return .result(dialog: "Streaming \(match.name) on \(conn.name).")
    }
}
