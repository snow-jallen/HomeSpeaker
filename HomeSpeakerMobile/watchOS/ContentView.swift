import SwiftUI

struct WatchContentView: View {
    @Environment(ConnectionStore.self) private var store

    var body: some View {
        if store.connections.isEmpty {
            WatchServerPickerView()
        } else {
            TabView {
                WatchNowPlayingView()
                WatchQueueView()
                WatchServerPickerView()
            }
            .tabViewStyle(.page)
        }
    }
}
