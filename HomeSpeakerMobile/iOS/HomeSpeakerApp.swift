import AppIntents
import SwiftUI

@main
struct HomeSpeakerApp: App {
    @State private var store = ConnectionStore()
    @State private var localPlayer = LocalPlayer()
    @Environment(\.scenePhase) private var scenePhase

    var body: some Scene {
        WindowGroup {
            ContentView()
                .environment(store)
                .environment(localPlayer)
                .onAppear {
                    WatchSync.shared.activate()
                    WatchSync.shared.send(connections: store.connections, selectedId: store.selectedConnection?.id)
                    HomeSpeakerShortcuts.updateAppShortcutParameters()
                }
        }
        .onChange(of: scenePhase) {
            if scenePhase == .active {
                store.reload()
            }
        }
        .onChange(of: store.connections) {
            HomeSpeakerShortcuts.updateAppShortcutParameters()
        }
    }
}
