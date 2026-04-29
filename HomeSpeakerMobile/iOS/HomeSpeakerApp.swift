import SwiftUI

@main
struct HomeSpeakerApp: App {
    @State private var store = ConnectionStore()

    var body: some Scene {
        WindowGroup {
            ContentView()
                .environment(store)
                .onAppear {
                    WatchSync.shared.activate()
                    WatchSync.shared.send(connections: store.connections, selectedId: store.selectedConnection?.id)
                }
        }
    }
}
