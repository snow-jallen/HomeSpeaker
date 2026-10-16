import AppIntents
import SwiftUI

// Lists what Siri understands. AppShortcut doesn't expose its phrases, so this
// mirrors HomeSpeakerShortcuts.swift by hand: keep the two in sync.
struct SiriCommandsView: View {
    @Environment(ConnectionStore.self) private var store

    private struct Command: Identifiable {
        let title: String
        let systemImage: String
        let phrases: [String]
        var id: String { title }
    }

    // "{app}" stands for the app name, "{server}" for a saved server's name.
    private static let commands: [Command] = [
        Command(title: "Next Song", systemImage: "forward.fill", phrases: [
            "{app}, skip this song",
            "{app}, skip this song on {server}",
            "Tell {app} to skip this song",
            "Tell {app} to skip the song on {server}",
        ]),
        Command(title: "Fun Music", systemImage: "sparkles", phrases: [
            "{app}, fun music",
            "{app}, fun music on {server}",
            "Tell {app} to put on fun music",
            "Tell {app} to put on fun music on {server}",
        ]),
        Command(title: "Hymns", systemImage: "music.note", phrases: [
            "{app}, hymns",
            "{app}, hymns on {server}",
            "Tell {app} to put on hymns",
            "Tell {app} to put on hymns on {server}",
        ]),
        Command(title: "Quiet Down", systemImage: "speaker.wave.1.fill", phrases: [
            "{app}, quiet down",
            "{app}, quiet down {server}",
            "Tell {app} to quiet down",
            "Tell {app} to quiet down {server}",
        ]),
        Command(title: "Stop", systemImage: "stop.fill", phrases: [
            "{app}, stop the music",
            "{app}, stop the music on {server}",
            "Tell {app} to stop the music",
            "Tell {app} to stop {server}",
        ]),
        Command(title: "Resume", systemImage: "play.fill", phrases: [
            "{app}, start the music",
            "{app}, start the music on {server}",
            "Tell {app} to start the music",
            "Tell {app} to start {server}",
        ]),
        Command(title: "Set Volume", systemImage: "speaker.wave.2.fill", phrases: [
            "{app}, change the volume",
            "{app}, change the volume on {server}",
            "Tell {app} to change the volume",
            "Tell {app} to change the volume on {server}",
        ]),
        Command(title: "Sleep Timer", systemImage: "moon.fill", phrases: [
            "{app}, start a sleep timer",
            "{app}, start a sleep timer on {server}",
            "Tell {app} to start a sleep timer",
            "Tell {app} to start a sleep timer on {server}",
        ]),
        Command(title: "Cancel Sleep Timer", systemImage: "moon.zzz", phrases: [
            "{app}, cancel the sleep timer",
            "{app}, cancel the sleep timer on {server}",
            "Tell {app} to cancel the sleep timer",
            "Tell {app} to cancel the sleep timer on {server}",
        ]),
        Command(title: "What's Playing", systemImage: "music.note.list", phrases: [
            "{app}, what song is this",
            "{app}, what song is on {server}",
            "Ask {app} what's playing",
            "Ask {app} what's playing on {server}",
        ]),
    ]

    // Intents without Siri phrases (iOS caps an app at 10 App Shortcuts).
    private static let shortcutsOnly: [(title: String, systemImage: String)] = [
        ("Play Artist", "music.mic"),
        ("Play Album", "square.stack"),
        ("Play Playlist", "music.note.list"),
        ("Play AI Playlist", "sparkles"),
        ("Stream Radio", "radio"),
    ]

    private var exampleServer: String {
        store.selectedConnection?.name ?? store.connections.first?.name ?? "Kitchen"
    }

    var body: some View {
        List {
            Section {
                Text("Say \u{201C}Hey Siri\u{201D}, then any phrase below. Start with \u{201C}Home Speaker\u{201D}: Siri keeps plain commands like \u{201C}next song\u{201D} or \u{201C}stop\u{201D} for itself.")
                Text("Phrases without a server act on the server selected in the app.")
                    .foregroundStyle(.secondary)
            }
            .font(.subheadline)

            ForEach(Self.commands) { command in
                Section {
                    ForEach(command.phrases, id: \.self) { phrase in
                        Text("\u{201C}\(spoken(phrase))\u{201D}")
                    }
                } header: {
                    Label(command.title, systemImage: command.systemImage)
                }
            }

            Section {
                ForEach(Self.shortcutsOnly, id: \.title) { item in
                    Label(item.title, systemImage: item.systemImage)
                }
                ShortcutsLink()
                    .frame(maxWidth: .infinity)
                    .listRowBackground(Color.clear)
            } header: {
                Text("In the Shortcuts App")
            } footer: {
                Text("These actions don't have Siri phrases, but you can add them to a shortcut and give it your own name.")
            }
        }
        .navigationTitle("Siri Commands")
        .navigationBarTitleDisplayMode(.inline)
    }

    private func spoken(_ phrase: String) -> String {
        phrase
            .replacingOccurrences(of: "{app}", with: "Home Speaker")
            .replacingOccurrences(of: "{server}", with: exampleServer)
    }
}
