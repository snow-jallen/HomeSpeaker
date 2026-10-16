import Intents

/// Teaches Siri the playlist, stream and AI mix names on the selected server, so "play <name>"
/// is heard as written ("Classical Kids", "KBAQ") rather than as similar-sounding words.
enum SiriVocabulary {
    static func update(for connection: ServerConnection?) async {
        guard let connection else { return }
        let api = APIClient.client(for: connection)
        async let playlists = try? api.getPlaylists()
        async let streams = try? api.getRadioStreams()
        async let mixes = try? api.getAiPlaylists()
        let (playlistList, streamList, mixList) = await (playlists, streams, mixes)
        // Keep the old names when the server can't be reached rather than forgetting them.
        guard playlistList != nil || streamList != nil || mixList != nil else { return }

        let names = (playlistList ?? []).map(\.name)
            + (streamList ?? []).map(\.name)
            + (mixList ?? []).filter { $0.trackCount > 0 }.map(\.displayName)
        INVocabulary.shared().setVocabularyStrings(NSOrderedSet(array: names), of: .mediaPlaylistTitle)
    }
}
