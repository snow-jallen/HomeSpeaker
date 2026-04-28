import SwiftUI

struct ContentView: View {
    @Environment(ConnectionStore.self) private var store

    var body: some View {
        if store.connections.isEmpty {
            NavigationStack {
                ServerListView()
            }
        } else {
            MainTabView()
        }
    }
}
