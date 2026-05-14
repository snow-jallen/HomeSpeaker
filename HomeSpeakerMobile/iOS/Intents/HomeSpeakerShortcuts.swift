import AppIntents

// Keep Siri phrases explicit and app-scoped so playback commands don't collide with
// more generic system music requests.

struct HomeSpeakerShortcuts: AppShortcutsProvider {
    static var shortcutTileColor: ShortcutTileColor = .blue

    static var appShortcuts: [AppShortcut] {
        AppShortcut(
            intent: NextSongOnHomeSpeakerIntent(),
            phrases: [
                "Next song in \(.applicationName)",
                "In \(.applicationName), next song",
            ],
            shortTitle: "Next Song",
            systemImageName: "forward.fill"
        )
        AppShortcut(
            intent: PlayFunMusicOnHomeSpeakerIntent(),
            phrases: [
                "Play fun music in \(.applicationName)",
                "In \(.applicationName), play fun music",
            ],
            shortTitle: "Fun Music",
            systemImageName: "sparkles"
        )
        AppShortcut(
            intent: PlayHymnsOnHomeSpeakerIntent(),
            phrases: [
                "Play hymns in \(.applicationName)",
                "In \(.applicationName), play hymns",
            ],
            shortTitle: "Hymns",
            systemImageName: "music.note"
        )
        AppShortcut(
            intent: QuietDownOnHomeSpeakerIntent(),
            phrases: [
                "Quiet down in \(.applicationName)",
                "In \(.applicationName), quiet down",
            ],
            shortTitle: "Quiet Down",
            systemImageName: "speaker.wave.1.fill"
        )
        AppShortcut(
            intent: StopHomeSpeakerIntent(),
            phrases: [
                "Stop HomeSpeaker in \(.applicationName)",
                "In \(.applicationName), stop HomeSpeaker",
            ],
            shortTitle: "Stop",
            systemImageName: "stop.fill"
        )
    }
}
