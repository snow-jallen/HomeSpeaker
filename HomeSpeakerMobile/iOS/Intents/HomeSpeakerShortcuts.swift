import AppIntents

// Phrases use a single MediaQueryEntity parameter per phrase (AppIntents requirement).
// The entity captures "content" or "content on server" (e.g., "Kings Singers on kitchen")
// and perform() parses it to extract the server hint automatically.

struct HomeSpeakerShortcuts: AppShortcutsProvider {
    static var shortcutTileColor: ShortcutTileColor = .blue

    static var appShortcuts: [AppShortcut] {
        AppShortcut(
            intent: PlayArtistOnHomeSpeakerIntent(),
            phrases: [
                "Play \(\.$artist) in \(.applicationName)",
                "Shuffle \(\.$artist) in \(.applicationName)",
            ],
            shortTitle: "Play Artist",
            systemImageName: "music.mic"
        )
        AppShortcut(
            intent: PlayAlbumOnHomeSpeakerIntent(),
            phrases: [
                "Play album \(\.$album) in \(.applicationName)",
            ],
            shortTitle: "Play Album",
            systemImageName: "opticaldisc"
        )
        AppShortcut(
            intent: PlayPlaylistOnHomeSpeakerIntent(),
            phrases: [
                "Play playlist \(\.$playlist) in \(.applicationName)",
            ],
            shortTitle: "Play Playlist",
            systemImageName: "music.note.list"
        )
        AppShortcut(
            intent: PlayAiPlaylistOnHomeSpeakerIntent(),
            phrases: [
                "Play AI playlist \(\.$genre) in \(.applicationName)",
                "Play \(\.$genre) AI playlist in \(.applicationName)",
            ],
            shortTitle: "Play AI Playlist",
            systemImageName: "sparkles"
        )
        AppShortcut(
            intent: PlayStreamOnHomeSpeakerIntent(),
            phrases: [
                "Stream \(\.$streamName) in \(.applicationName)",
                "Play stream \(\.$streamName) in \(.applicationName)",
            ],
            shortTitle: "Play Stream",
            systemImageName: "antenna.radiowaves.left.and.right"
        )
    }
}
