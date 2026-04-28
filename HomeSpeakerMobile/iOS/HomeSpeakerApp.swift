import SwiftUI

@main
struct HomeSpeakerApp: App {
    @State private var store = ConnectionStore()

    var body: some Scene {
        WindowGroup {
            ContentView()
                .environment(store)
        }
    }
}
