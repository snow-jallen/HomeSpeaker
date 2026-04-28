import SwiftUI

@main
struct HomeSpeakerWatchApp: App {
    @State private var store = ConnectionStore()

    var body: some Scene {
        WindowGroup {
            WatchContentView()
                .environment(store)
        }
    }
}
