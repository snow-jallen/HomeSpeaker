import SwiftUI
import UIKit

struct PushNotificationsView: View {
    @Environment(ConnectionStore.self) private var store
    @Environment(PushNotificationStore.self) private var pushNotifications
    @Environment(\.openURL) private var openURL

    var body: some View {
        List {
            statusSection
            helpSection
        }
        .navigationTitle("Notifications")
        .refreshable {
            await pushNotifications.refreshStatus()
        }
        .task {
            pushNotifications.updateConnection(store.selectedConnection)
            await pushNotifications.refreshStatus()
        }
    }

    private var statusSection: some View {
        Section("Status") {
            LabeledContent("Server", value: store.selectedConnection?.name ?? "No server")
            LabeledContent("Permission", value: pushNotifications.authorizationState.displayText)
            LabeledContent("Registration", value: pushNotifications.summaryLine)

            if pushNotifications.isRefreshing {
                HStack(spacing: 8) {
                    ProgressView()
                    Text("Updating registration…")
                        .foregroundStyle(.secondary)
                }
            } else {
                Text(pushNotifications.registrationDetail)
                    .font(.caption)
                    .foregroundStyle(.secondary)
            }

            if let error = pushNotifications.lastError {
                Text(error)
                    .font(.caption)
                    .foregroundStyle(.red)
            }

            if pushNotifications.authorizationState == .notDetermined {
                Button("Enable Notifications") {
                    Task {
                        await pushNotifications.requestPushAuthorization()
                    }
                }
            } else if pushNotifications.authorizationState == .denied {
                Button("Open Settings") {
                    if let settingsURL = URL(string: UIApplication.openSettingsURLString) {
                        openURL(settingsURL)
                    }
                }
            }
        }
    }

    private var helpSection: some View {
        Section("How it works") {
            Text("Enable notifications here to let the selected HomeSpeaker server send push alerts to this iPhone.")
                .font(.caption)
                .foregroundStyle(.secondary)
        }
    }
}
