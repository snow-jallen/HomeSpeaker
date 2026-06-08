import AppIntents
import SwiftUI

@main
struct HomeSpeakerApp: App {
    @UIApplicationDelegateAdaptor(PushNotificationAppDelegate.self) private var pushDelegate
    @State private var store = ConnectionStore()
    @State private var localPlayer = LocalPlayer()
    private let offlineDownloads = OfflineDownloadsStore.shared
    @State private var pushNotifications = PushNotificationStore()
    @Environment(\.scenePhase) private var scenePhase

    var body: some Scene {
        WindowGroup {
            ContentView()
                .environment(store)
                .environment(localPlayer)
                .environment(offlineDownloads)
                .environment(pushNotifications)
                .onAppear {
                    pushNotifications.bindDelegate(pushDelegate)
                    WatchSync.shared.activate()
                    WatchSync.shared.send(connections: store.connections, selectedId: store.selectedConnection?.id)
                    HomeSpeakerShortcuts.updateAppShortcutParameters()
                    offlineDownloads.updateConnection(store.selectedConnection)
                    pushNotifications.updateConnection(store.selectedConnection)
                    Task {
                        await pushNotifications.handleSceneBecameActive(connection: store.selectedConnection)
                    }
                }
        }
        .onChange(of: scenePhase) {
            if scenePhase == .active {
                store.reload()
                offlineDownloads.updateConnection(store.selectedConnection)
                Task {
                    await pushNotifications.handleSceneBecameActive(connection: store.selectedConnection)
                }
            }
        }
        .onChange(of: store.connections) {
            HomeSpeakerShortcuts.updateAppShortcutParameters()
        }
        .onChange(of: store.selectedConnection) {
            offlineDownloads.updateConnection(store.selectedConnection)
            pushNotifications.updateConnection(store.selectedConnection)
        }
    }
}
