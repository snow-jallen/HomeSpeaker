import AppIntents

// Keep Siri phrases explicit and app-scoped so playback commands don't collide with
// more generic system music requests. Each command also has a phrase naming a
// server ("... on Kitchen"); without one it acts on the server selected in the app.
// iOS allows at most 10 App Shortcuts.

struct HomeSpeakerShortcuts: AppShortcutsProvider {
    static var shortcutTileColor: ShortcutTileColor = .blue

    static var appShortcuts: [AppShortcut] {
        AppShortcut(
            intent: NextSongOnHomeSpeakerIntent(),
            phrases: [
                "Next song in \(.applicationName)",
                "In \(.applicationName), next song",
                "Next song in \(.applicationName) on \(\.$server)",
            ],
            shortTitle: "Next Song",
            systemImageName: "forward.fill"
        )
        AppShortcut(
            intent: PlayFunMusicOnHomeSpeakerIntent(),
            phrases: [
                "Play fun music in \(.applicationName)",
                "In \(.applicationName), play fun music",
                "Play fun music in \(.applicationName) on \(\.$server)",
            ],
            shortTitle: "Fun Music",
            systemImageName: "sparkles"
        )
        AppShortcut(
            intent: PlayHymnsOnHomeSpeakerIntent(),
            phrases: [
                "Play hymns in \(.applicationName)",
                "In \(.applicationName), play hymns",
                "Play hymns in \(.applicationName) on \(\.$server)",
            ],
            shortTitle: "Hymns",
            systemImageName: "music.note"
        )
        AppShortcut(
            intent: QuietDownOnHomeSpeakerIntent(),
            phrases: [
                "Quiet down in \(.applicationName)",
                "In \(.applicationName), quiet down",
                "Quiet down \(\.$server) in \(.applicationName)",
            ],
            shortTitle: "Quiet Down",
            systemImageName: "speaker.wave.1.fill"
        )
        AppShortcut(
            intent: StopHomeSpeakerIntent(),
            phrases: [
                "Stop HomeSpeaker in \(.applicationName)",
                "In \(.applicationName), stop HomeSpeaker",
                "Stop \(\.$server) in \(.applicationName)",
            ],
            shortTitle: "Stop",
            systemImageName: "stop.fill"
        )
        AppShortcut(
            intent: ResumeHomeSpeakerIntent(),
            phrases: [
                "Resume \(.applicationName)",
                "In \(.applicationName), resume music",
                "Resume \(\.$server) in \(.applicationName)",
            ],
            shortTitle: "Resume",
            systemImageName: "play.fill"
        )
        AppShortcut(
            intent: SetVolumeOnHomeSpeakerIntent(),
            phrases: [
                "Set volume in \(.applicationName)",
                "In \(.applicationName), set the volume",
                "Set \(\.$server) volume in \(.applicationName)",
            ],
            shortTitle: "Set Volume",
            systemImageName: "speaker.wave.2.fill"
        )
        AppShortcut(
            intent: SetSleepTimerOnHomeSpeakerIntent(),
            phrases: [
                "Set a sleep timer in \(.applicationName)",
                "In \(.applicationName), set a sleep timer",
                "Set a sleep timer on \(\.$server) in \(.applicationName)",
            ],
            shortTitle: "Sleep Timer",
            systemImageName: "moon.fill"
        )
        AppShortcut(
            intent: CancelSleepTimerOnHomeSpeakerIntent(),
            phrases: [
                "Cancel the sleep timer in \(.applicationName)",
                "In \(.applicationName), cancel the sleep timer",
            ],
            shortTitle: "Cancel Sleep Timer",
            systemImageName: "moon.zzz"
        )
        AppShortcut(
            intent: WhatsPlayingOnHomeSpeakerIntent(),
            phrases: [
                "What's playing in \(.applicationName)",
                "In \(.applicationName), what's playing",
                "What's playing on \(\.$server) in \(.applicationName)",
            ],
            shortTitle: "What's Playing",
            systemImageName: "music.note.list"
        )
    }
}
