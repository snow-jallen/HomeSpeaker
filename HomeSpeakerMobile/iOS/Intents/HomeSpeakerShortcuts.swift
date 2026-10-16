import AppIntents

// Siri keeps common phrases for its own features ("next song", "resume", "stop",
// "set the volume", "set a timer", "what's playing", "play X in <app>") and won't
// hand them to an app shortcut, even one naming the app. So every phrase leads
// with the app name ("Home Speaker, ..." or "Tell/Ask Home Speaker to ...") and
// avoids those bare system commands. Each command also has phrases naming a
// server ("... on Kitchen"); without one it acts on the server selected in the app.
// iOS allows at most 10 App Shortcuts. SiriCommandsView lists these phrases in the
// app; update it when they change.

struct HomeSpeakerShortcuts: AppShortcutsProvider {
    static var shortcutTileColor: ShortcutTileColor = .blue

    static var appShortcuts: [AppShortcut] {
        AppShortcut(
            intent: NextSongOnHomeSpeakerIntent(),
            phrases: [
                "\(.applicationName), skip this song",
                "\(.applicationName), skip this song on \(\.$server)",
                "Tell \(.applicationName) to skip this song",
                "Tell \(.applicationName) to skip the song on \(\.$server)",
            ],
            shortTitle: "Next Song",
            systemImageName: "forward.fill"
        )
        AppShortcut(
            intent: PlayFunMusicOnHomeSpeakerIntent(),
            phrases: [
                "\(.applicationName), fun music",
                "\(.applicationName), fun music on \(\.$server)",
                "Tell \(.applicationName) to put on fun music",
                "Tell \(.applicationName) to put on fun music on \(\.$server)",
            ],
            shortTitle: "Fun Music",
            systemImageName: "sparkles"
        )
        AppShortcut(
            intent: PlayHymnsOnHomeSpeakerIntent(),
            phrases: [
                "\(.applicationName), hymns",
                "\(.applicationName), hymns on \(\.$server)",
                "Tell \(.applicationName) to put on hymns",
                "Tell \(.applicationName) to put on hymns on \(\.$server)",
            ],
            shortTitle: "Hymns",
            systemImageName: "music.note"
        )
        AppShortcut(
            intent: QuietDownOnHomeSpeakerIntent(),
            phrases: [
                "\(.applicationName), quiet down",
                "\(.applicationName), quiet down \(\.$server)",
                "Tell \(.applicationName) to quiet down",
                "Tell \(.applicationName) to quiet down \(\.$server)",
            ],
            shortTitle: "Quiet Down",
            systemImageName: "speaker.wave.1.fill"
        )
        AppShortcut(
            intent: StopHomeSpeakerIntent(),
            phrases: [
                "\(.applicationName), stop the music",
                "\(.applicationName), stop the music on \(\.$server)",
                "Tell \(.applicationName) to stop the music",
                "Tell \(.applicationName) to stop \(\.$server)",
            ],
            shortTitle: "Stop",
            systemImageName: "stop.fill"
        )
        AppShortcut(
            intent: ResumeHomeSpeakerIntent(),
            phrases: [
                "\(.applicationName), start the music",
                "\(.applicationName), start the music on \(\.$server)",
                "Tell \(.applicationName) to start the music",
                "Tell \(.applicationName) to start \(\.$server)",
            ],
            shortTitle: "Resume",
            systemImageName: "play.fill"
        )
        AppShortcut(
            intent: SetVolumeOnHomeSpeakerIntent(),
            phrases: [
                "\(.applicationName), change the volume",
                "\(.applicationName), change the volume on \(\.$server)",
                "Tell \(.applicationName) to change the volume",
                "Tell \(.applicationName) to change the volume on \(\.$server)",
            ],
            shortTitle: "Set Volume",
            systemImageName: "speaker.wave.2.fill"
        )
        AppShortcut(
            intent: SetSleepTimerOnHomeSpeakerIntent(),
            phrases: [
                "\(.applicationName), start a sleep timer",
                "\(.applicationName), start a sleep timer on \(\.$server)",
                "Tell \(.applicationName) to start a sleep timer",
                "Tell \(.applicationName) to start a sleep timer on \(\.$server)",
            ],
            shortTitle: "Sleep Timer",
            systemImageName: "moon.fill"
        )
        AppShortcut(
            intent: CancelSleepTimerOnHomeSpeakerIntent(),
            phrases: [
                "\(.applicationName), cancel the sleep timer",
                "\(.applicationName), cancel the sleep timer on \(\.$server)",
                "Tell \(.applicationName) to cancel the sleep timer",
                "Tell \(.applicationName) to cancel the sleep timer on \(\.$server)",
            ],
            shortTitle: "Cancel Sleep Timer",
            systemImageName: "moon.zzz"
        )
        AppShortcut(
            intent: WhatsPlayingOnHomeSpeakerIntent(),
            phrases: [
                "\(.applicationName), what song is this",
                "\(.applicationName), what song is on \(\.$server)",
                "Ask \(.applicationName) what's playing",
                "Ask \(.applicationName) what's playing on \(\.$server)",
            ],
            shortTitle: "What's Playing",
            systemImageName: "music.note.list"
        )
    }
}
