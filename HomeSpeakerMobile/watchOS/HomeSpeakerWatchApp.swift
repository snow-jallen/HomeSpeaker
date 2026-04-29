import SwiftUI

@main
struct HomeSpeakerWatchApp: App {
    @State private var store = ConnectionStore()

    var body: some Scene {
        WindowGroup {
            WatchContentView()
                .environment(store)
                .onAppear {
                    WatchSync.shared.onConnectionsReceived = { connections, selectedId in
                        store.receiveFromPhone(connections, selectedId: selectedId)
                    }
                    WatchSync.shared.activate()
                }
        }
    }
}
