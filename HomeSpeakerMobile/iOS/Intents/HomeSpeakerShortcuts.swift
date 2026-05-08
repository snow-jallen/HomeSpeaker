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
                "Play \(\.$artist) on \(.applicationName)",
                "Play \(\.$artist) \(.applicationName)",
                "Shuffle \(\.$artist) on \(.applicationName)",
                "Shuffle \(\.$artist) \(.applicationName)",
            ],
            shortTitle: "Play Artist",
            systemImageName: "music.mic"
        )
        AppShortcut(
            intent: PlayAlbumOnHomeSpeakerIntent(),
            phrases: [
                "Play album \(\.$album) on \(.applicationName)",
                "Play album \(\.$album) \(.applicationName)",
            ],
            shortTitle: "Play Album",
            systemImageName: "opticaldisc"
        )
        AppShortcut(
            intent: PlayPlaylistOnHomeSpeakerIntent(),
            phrases: [
                "Play playlist \(\.$playlist) on \(.applicationName)",
                "Play playlist \(\.$playlist) \(.applicationName)",
            ],
            shortTitle: "Play Playlist",
            systemImageName: "music.note.list"
        )
        AppShortcut(
            intent: PlayAiPlaylistOnHomeSpeakerIntent(),
            phrases: [
                "Play AI playlist \(\.$genre) on \(.applicationName)",
                "Play AI playlist \(\.$genre) \(.applicationName)",
                "Play \(\.$genre) AI playlist on \(.applicationName)",
                "Play \(\.$genre) AI playlist \(.applicationName)",
            ],
            shortTitle: "Play AI Playlist",
            systemImageName: "sparkles"
        )
        AppShortcut(
            intent: PlayStreamOnHomeSpeakerIntent(),
            phrases: [
                "Stream \(\.$streamName) on \(.applicationName)",
                "Stream \(\.$streamName) \(.applicationName)",
                "Play stream \(\.$streamName) on \(.applicationName)",
                "Play stream \(\.$streamName) \(.applicationName)",
            ],
            shortTitle: "Play Stream",
            systemImageName: "antenna.radiowaves.left.and.right"
        )
    }
}
