import Intents

/// Teaches Siri the playlist and stream names on the selected server, so "play <name>"
/// is heard as written ("Classical Kids", "KBAQ") rather than as similar-sounding words.
enum SiriVocabulary {
    static func update(for connection: ServerConnection?) async {
        guard let connection else { return }
        let api = APIClient.client(for: connection)
        async let playlists = try? api.getPlaylists()
        async let streams = try? api.getRadioStreams()
        let (playlistList, streamList) = await (playlists, streams)
        // Keep the old names when the server can't be reached rather than forgetting them.
        guard playlistList != nil || streamList != nil else { return }

        let names = (playlistList ?? []).map(\.name)
            + (streamList ?? []).map(\.name)
        INVocabulary.shared().setVocabularyStrings(NSOrderedSet(array: names), of: .mediaPlaylistTitle)
    }
}
