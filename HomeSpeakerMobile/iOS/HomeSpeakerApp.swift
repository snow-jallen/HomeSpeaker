import AppIntents
import SwiftUI

@main
struct HomeSpeakerApp: App {
    @UIApplicationDelegateAdaptor(PushNotificationAppDelegate.self) private var pushDelegate
    @State private var store = ConnectionStore()
    @State private var localPlayer = LocalPlayer()
    private let offlineDownloads = OfflineDownloadsStore.shared
    @State private var pushNotifications = PushNotificationStore()
    @State private var speakerStatus = SpeakerStatusStore()
    @Environment(\.scenePhase) private var scenePhase

    var body: some Scene {
        WindowGroup {
            ContentView()
                .environment(store)
                .environment(localPlayer)
                .environment(offlineDownloads)
                .environment(pushNotifications)
                .environment(speakerStatus)
                .onAppear {
                    SpeakerActivityController.shared.reattach()
                    speakerStatus.follow(store.selectedConnection)
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
                speakerStatus.follow(store.selectedConnection)
                SpeakerActivityController.shared.reattach()
                offlineDownloads.updateConnection(store.selectedConnection)
                Task {
                    await pushNotifications.handleSceneBecameActive(connection: store.selectedConnection)
                }
            } else if scenePhase == .background {
                speakerStatus.pause()
            }
        }
        .onChange(of: store.connections) {
            HomeSpeakerShortcuts.updateAppShortcutParameters()
            SpeakerSurfaces.reloadWidgets()
        }
        .onChange(of: store.selectedConnection) {
            speakerStatus.follow(store.selectedConnection)
            // Widgets set to follow the app's speaker should switch too.
            SpeakerSurfaces.reloadWidgets()
            offlineDownloads.updateConnection(store.selectedConnection)
            pushNotifications.updateConnection(store.selectedConnection)
        }
    }
}
