import AppIntents

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

private func matchedPlaylist(from playlists: [Playlist], aliases: [String]) -> Playlist? {
    playlists.first { playlist in
        aliases.contains { alias in matchesShortcutAlias(playlist.name, aliases: [alias]) }
    }
}

/// Plays the playlist matching `aliases`.
private func playNamedPlaylist(
    userFacingName: String,
    aliases: [String],
    connection: ServerConnection,
    api: APIClient
) async throws -> String {
    if let match = matchedPlaylist(from: (try? await api.getPlaylists()) ?? [], aliases: aliases) {
        try await api.playPlaylist(name: match.name)
        return match.name
    }

    throw HomeSpeakerIntentError.playlistUnavailable(userFacingName, server: connection.name)
}

// MARK: - Siri Command History

/// What Siri passed for the server parameter, for the history.
private func serverCaptured(_ server: ServerEntity?) -> String {
    server.map { "server: \($0.name)" } ?? "no server named"
}

/// Runs a Siri command against the server it names, else the selected one, and records it
/// in the Siri Command History. `work` returns what Siri should say, which is also the result.
private func runCommand(
    _ command: String,
    server: ServerEntity?,
    captured extra: [String] = [],
    request: String,
    _ work: (ServerConnection, APIClient) async throws -> String
) async throws -> String {
    try await runCommand(command,
                         captured: (extra + [serverCaptured(server)]).joined(separator: "; "),
                         request: request,
                         connection: { try resolveServer(server) },
                         work)
}

private func runCommand(
    _ command: String,
    captured: String,
    request: String,
    connection resolve: () throws -> ServerConnection,
    _ work: (ServerConnection, APIClient) async throws -> String
) async throws -> String {
    let connection: ServerConnection
    do {
        connection = try resolve()
    } catch {
        SiriCommandLog.add(command: command, captured: captured, translated: "No server to ask",
                           result: error.localizedDescription)
        throw error
    }

    let translated = "Ask \(connection.name) to \(request)"
    do {
        let result = try await work(connection, APIClient.client(for: connection))
        SiriCommandLog.add(command: command, captured: captured, translated: translated, result: result)
        return result
    } catch {
        SiriCommandLog.add(command: command, captured: captured, translated: translated,
                           result: "Failed: \(error.localizedDescription)")
        throw error
    }
}

/// Legacy intents take a server from their parameter, else from "... on Kitchen" in the name.
private func legacyConnection(_ server: ServerEntity?, extracted: ServerConnection?) throws -> ServerConnection {
    if let server { return try resolveServer(server) }
    return try extracted ?? resolveServer(nil)
}

// MARK: - Focused Siri Controls

struct NextSongOnHomeSpeakerIntent: AppIntent {
    static var title: LocalizedStringResource = "HomeSpeaker Next Song"
    static var description = IntentDescription("Skip to the next song on a HomeSpeaker server.")
    static var openAppWhenRun: Bool = false

    @Parameter(title: "Server")
    var server: ServerEntity?

    func perform() async throws -> some IntentResult & ProvidesDialog {
        let said = try await runCommand("Next Song", server: server, request: "skip to the next song") { connection, api in
            try await api.skipToNext()
            await SpeakerSurfaces.refresh(after: connection)
            return "Skipping to the next song on \(connection.name)."
        }
        return .result(dialog: "\(said)")
    }
}

struct PlayFunMusicOnHomeSpeakerIntent: AppIntent {
    static var title: LocalizedStringResource = "Play Fun Music on HomeSpeaker"
    static var description = IntentDescription("Start the Fun Music playlist on a HomeSpeaker server.")
    static var openAppWhenRun: Bool = false

    @Parameter(title: "Server")
    var server: ServerEntity?

    func perform() async throws -> some IntentResult & ProvidesDialog {
        let said = try await runCommand("Fun Music", server: server,
                                        request: "play a fun playlist") { connection, api in
            let playlistName = try await playNamedPlaylist(
                userFacingName: "Fun Music",
                aliases: ["family-singalong", "fun music", "fun"],
                connection: connection,
                api: api
            )
            await SpeakerSurfaces.refresh(after: connection)
            return "Playing \(playlistName) on \(connection.name)."
        }
        return .result(dialog: "\(said)")
    }
}

struct PlayHymnsOnHomeSpeakerIntent: AppIntent {
    static var title: LocalizedStringResource = "Play Hymns on HomeSpeaker"
    static var description = IntentDescription("Start the Hymns playlist on a HomeSpeaker server.")
    static var openAppWhenRun: Bool = false

    @Parameter(title: "Server")
    var server: ServerEntity?

    func perform() async throws -> some IntentResult & ProvidesDialog {
        let said = try await runCommand("Hymns", server: server,
                                        request: "play a hymns playlist") { connection, api in
            let playlistName = try await playNamedPlaylist(
                userFacingName: "Hymns",
                aliases: ["hymns", "hymn", "Hymns for Home and Church"],
                connection: connection,
                api: api
            )
            await SpeakerSurfaces.refresh(after: connection)
            return "Playing \(playlistName) on \(connection.name)."
        }
        return .result(dialog: "\(said)")
    }
}

struct QuietDownOnHomeSpeakerIntent: AppIntent {
    static var title: LocalizedStringResource = "Quiet Down HomeSpeaker"
    static var description = IntentDescription("Cut a HomeSpeaker server's volume in half.")
    static var openAppWhenRun: Bool = false

    @Parameter(title: "Server")
    var server: ServerEntity?

    func perform() async throws -> some IntentResult & ProvidesDialog {
        let said = try await runCommand("Quiet Down", server: server, request: "lower the volume") { connection, api in
            let status = try await api.getPlayerStatus()
            let newVolume = status.quietDownVolume
            try await api.setVolume(newVolume)
            await SpeakerSurfaces.refresh(after: connection)
            return "Volume reduced to \(newVolume) on \(connection.name)."
        }
        return .result(dialog: "\(said)")
    }
}

struct StopHomeSpeakerIntent: AppIntent {
    static var title: LocalizedStringResource = "Stop HomeSpeaker"
    static var description = IntentDescription("Stop playback on a HomeSpeaker server.")
    static var openAppWhenRun: Bool = false

    @Parameter(title: "Server")
    var server: ServerEntity?

    func perform() async throws -> some IntentResult & ProvidesDialog {
        let said = try await runCommand("Stop", server: server, request: "stop playing") { connection, api in
            try await api.stop()
            await SpeakerSurfaces.refresh(after: connection)
            return "Stopped \(connection.name)."
        }
        return .result(dialog: "\(said)")
    }
}

struct ResumeHomeSpeakerIntent: AppIntent {
    static var title: LocalizedStringResource = "Resume HomeSpeaker"
    static var description = IntentDescription("Resume playback on a HomeSpeaker server.")
    static var openAppWhenRun: Bool = false

    @Parameter(title: "Server")
    var server: ServerEntity?

    func perform() async throws -> some IntentResult & ProvidesDialog {
        let said = try await runCommand("Resume", server: server, request: "resume playing") { connection, api in
            try await api.resume()
            await SpeakerSurfaces.refresh(after: connection)
            return "Resuming \(connection.name)."
        }
        return .result(dialog: "\(said)")
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
        let clamped = min(100, max(0, level))
        let said = try await runCommand("Set Volume", server: server, captured: ["volume: \(level)"],
                                        request: "set the volume to \(clamped)") { connection, api in
            try await api.setVolume(clamped)
            await SpeakerSurfaces.refresh(after: connection)
            return "Volume set to \(clamped) on \(connection.name)."
        }
        return .result(dialog: "\(said)")
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
        let clamped = min(240, max(1, minutes))
        let said = try await runCommand("Sleep Timer", server: server, captured: ["minutes: \(minutes)"],
                                        request: "stop in \(clamped) minutes") { connection, api in
            try await api.setSleepTimer(minutes: clamped)
            await SpeakerSurfaces.refresh(after: connection)
            return "\(connection.name) will stop in \(clamped) minutes."
        }
        return .result(dialog: "\(said)")
    }
}

struct CancelSleepTimerOnHomeSpeakerIntent: AppIntent {
    static var title: LocalizedStringResource = "Cancel HomeSpeaker Sleep Timer"
    static var description = IntentDescription("Cancel a HomeSpeaker server's sleep timer.")
    static var openAppWhenRun: Bool = false

    @Parameter(title: "Server")
    var server: ServerEntity?

    func perform() async throws -> some IntentResult & ProvidesDialog {
        let said = try await runCommand("Cancel Sleep Timer", server: server, request: "cancel its sleep timer") { connection, api in
            try await api.cancelSleepTimer()
            await SpeakerSurfaces.refresh(after: connection)
            return "Sleep timer cancelled on \(connection.name)."
        }
        return .result(dialog: "\(said)")
    }
}

struct WhatsPlayingOnHomeSpeakerIntent: AppIntent {
    static var title: LocalizedStringResource = "What's Playing on HomeSpeaker"
    static var description = IntentDescription("Say what a HomeSpeaker server is playing.")
    static var openAppWhenRun: Bool = false

    @Parameter(title: "Server")
    var server: ServerEntity?

    func perform() async throws -> some IntentResult & ProvidesDialog & ReturnsValue<String> {
        var playing = ""
        let said = try await runCommand("What's Playing", server: server, request: "say what's playing") { connection, api in
            let status = try await api.getPlayerStatus()
            guard status.isPlaying else {
                return "Nothing is playing on \(connection.name)."
            }
            if let song = status.currentSong {
                playing = song.artist.map { "\(song.displayTitle) by \($0)" } ?? song.displayTitle
            } else {
                playing = status.displayTitle
            }
            return "\(connection.name) is playing \(playing)."
        }
        return .result(value: playing, dialog: "\(said)")
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
        let said = try await runCommand("Play Artist",
                                        captured: "artist: \(artist.id); \(serverCaptured(server))",
                                        request: "shuffle songs by \(contentName)",
                                        connection: { try legacyConnection(server, extracted: extractedServer) }) { conn, _ in
            let api = selectAndConnect(conn)
            try await api.playArtist(contentName)
            try await api.shuffleQueue()
            return "Shuffling \(contentName) on \(conn.name)."
        }
        return .result(dialog: "\(said)")
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
        let said = try await runCommand("Play Album",
                                        captured: "album: \(album.id); \(serverCaptured(server))",
                                        request: "play the album \(contentName)",
                                        connection: { try legacyConnection(server, extracted: extractedServer) }) { conn, _ in
            let api = selectAndConnect(conn)
            try await api.playAlbum(contentName)
            return "Playing \(contentName) on \(conn.name)."
        }
        return .result(dialog: "\(said)")
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
        let said = try await runCommand("Play Playlist",
                                        captured: "playlist: \(playlist.id); \(serverCaptured(server))",
                                        request: "play the playlist closest to \"\(contentName)\"",
                                        connection: { try legacyConnection(server, extracted: extractedServer) }) { conn, _ in
            let api = selectAndConnect(conn)
            let playlists = try await api.getPlaylists()
            guard let match = bestMatch(from: playlists, query: contentName, key: { $0.name }) else {
                throw HomeSpeakerIntentError.contentNotFound(contentName)
            }
            try await api.playPlaylist(name: match.name)
            return "Playing \(match.name) on \(conn.name)."
        }
        return .result(dialog: "\(said)")
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
        let said = try await runCommand("Stream Radio",
                                        captured: "stream: \(streamName.id); \(serverCaptured(server))",
                                        request: "play the stream closest to \"\(contentName)\"",
                                        connection: { try legacyConnection(server, extracted: extractedServer) }) { conn, _ in
            let api = selectAndConnect(conn)
            let streams = try await api.getRadioStreams()
            guard let match = bestMatch(from: streams, query: contentName, key: { $0.name }) else {
                throw HomeSpeakerIntentError.contentNotFound(contentName)
            }
            try await api.playRadioStream(id: match.id)
            return "Streaming \(match.name) on \(conn.name)."
        }
        return .result(dialog: "\(said)")
    }
}
