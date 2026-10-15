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
            "Next song in {app}",
            "In {app}, next song",
            "Next song in {app} on {server}",
            "Next song on {server} {app}",
            "Skip {server} {app}",
        ]),
        Command(title: "Fun Music", systemImage: "sparkles", phrases: [
            "Play fun music in {app}",
            "In {app}, play fun music",
            "Play fun music in {app} on {server}",
            "Play fun music on {server} {app}",
        ]),
        Command(title: "Hymns", systemImage: "music.note", phrases: [
            "Play hymns in {app}",
            "In {app}, play hymns",
            "Play hymns in {app} on {server}",
            "Play hymns on {server} {app}",
        ]),
        Command(title: "Quiet Down", systemImage: "speaker.wave.1.fill", phrases: [
            "Quiet down in {app}",
            "In {app}, quiet down",
            "Quiet down {server} in {app}",
            "Quiet down {server} {app}",
            "Turn down {server} {app}",
        ]),
        Command(title: "Stop", systemImage: "stop.fill", phrases: [
            "Stop HomeSpeaker in {app}",
            "In {app}, stop HomeSpeaker",
            "Stop {server} in {app}",
            "Stop {server} {app}",
            "Stop the {app}",
        ]),
        Command(title: "Resume", systemImage: "play.fill", phrases: [
            "Resume {app}",
            "In {app}, resume music",
            "Resume {server} in {app}",
            "Resume {server} {app}",
            "Start {server} {app}",
        ]),
        Command(title: "Set Volume", systemImage: "speaker.wave.2.fill", phrases: [
            "Set volume in {app}",
            "In {app}, set the volume",
            "Set {server} volume in {app}",
            "Set {server} {app} volume",
        ]),
        Command(title: "Sleep Timer", systemImage: "moon.fill", phrases: [
            "Set a sleep timer in {app}",
            "In {app}, set a sleep timer",
            "Set a sleep timer on {server} in {app}",
            "Set a sleep timer on {server} {app}",
        ]),
        Command(title: "Cancel Sleep Timer", systemImage: "moon.zzz", phrases: [
            "Cancel the sleep timer in {app}",
            "In {app}, cancel the sleep timer",
        ]),
        Command(title: "What's Playing", systemImage: "music.note.list", phrases: [
            "What's playing in {app}",
            "In {app}, what's playing",
            "What's playing on {server} in {app}",
            "What's playing on {server} {app}",
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
                Text("Say \u{201C}Hey Siri\u{201D} followed by any phrase below. Siri also answers to \u{201C}Home Speaker\u{201D} and \u{201C}Speaker\u{201D}, so \u{201C}Stop \(exampleServer) Speaker\u{201D} works too.")
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
            .replacingOccurrences(of: "{app}", with: "HomeSpeaker")
            .replacingOccurrences(of: "{server}", with: exampleServer)
    }
}
