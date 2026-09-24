import Foundation
import Observation
import UIKit
import UserNotifications

enum PushAuthorizationState: String {
    case notDetermined
    case denied
    case authorized
    case provisional
    case ephemeral

    var isAuthorized: Bool {
        switch self {
        case .authorized, .provisional, .ephemeral:
            return true
        case .notDetermined, .denied:
            return false
        }
    }

    var displayText: String {
        switch self {
        case .notDetermined:
            return "Not enabled"
        case .denied:
            return "Denied"
        case .authorized:
            return "Enabled"
        case .provisional:
            return "Deliver quietly"
        case .ephemeral:
            return "Temporary"
        }
    }
}

@MainActor
@Observable
final class PushNotificationStore {
    private let installationIdKey = "hs_push_installation_id"
    private let deviceTokenKey = "hs_push_device_token"
    private let defaults = UserDefaults.standard

    private(set) var installationId: String
    private(set) var authorizationState: PushAuthorizationState = .notDetermined
    private(set) var registrationStatus: PushRegistrationStatusDto?
    private(set) var currentConnection: ServerConnection?
    private(set) var serverSupportsPush = true
    private(set) var isRefreshing = false
    private(set) var lastError: String?
    private(set) var lastRegisteredToken: String?

    init() {
        if let savedInstallationId = defaults.string(forKey: installationIdKey), !savedInstallationId.isEmpty {
            installationId = savedInstallationId
        } else {
            let newInstallationId = UUID().uuidString
            defaults.set(newInstallationId, forKey: installationIdKey)
            installationId = newInstallationId
        }

        lastRegisteredToken = defaults.string(forKey: deviceTokenKey)
    }

    var summaryLine: String {
        guard currentConnection != nil else { return "No server" }
        if !serverSupportsPush { return "Unavailable" }
        if authorizationState == .denied { return "Denied" }
        if registrationStatus?.isRegistered == true {
            return "Ready"
        }
        if authorizationState.isAuthorized, lastRegisteredToken != nil {
            return "Registering…"
        }
        if authorizationState.isAuthorized {
            return "Waiting for token"
        }
        return "Not enabled"
    }

    var registrationDetail: String {
        if !serverSupportsPush {
            return "This server does not expose push registration yet."
        }

        guard currentConnection != nil else {
            return "Choose a HomeSpeaker server first."
        }

        if let status = registrationStatus, status.isRegistered {
            if let updated = status.deviceTokenUpdatedUtc, !updated.isEmpty {
                return "Device token registered."
            }
            return "This iPhone is registered with the server."
        }

        if authorizationState.isAuthorized, lastRegisteredToken == nil {
            return "Waiting for Apple to return a device token."
        }

        if authorizationState.isAuthorized {
            return "This iPhone is waiting to finish registration with the server."
        }

        return "This iPhone is not registered with the server yet."
    }

    func bindDelegate(_ delegate: PushNotificationAppDelegate) {
        delegate.store = self
    }

    func updateConnection(_ connection: ServerConnection?) {
        guard currentConnection?.id != connection?.id else { return }
        currentConnection = connection
        registrationStatus = nil
        serverSupportsPush = true
        lastError = nil

        Task {
            await refreshStatus()
        }
    }

    func handleSceneBecameActive(connection: ServerConnection?) async {
        currentConnection = connection
        await refreshAuthorizationState()
        await refreshStatus()
    }

    func refreshStatus() async {
        await refreshAuthorizationState()

        guard let connection = currentConnection else {
            registrationStatus = nil
            lastError = nil
            return
        }

        isRefreshing = true
        defer { isRefreshing = false }

        let api = APIClient(baseURL: connection.baseURL)
        do {
            if authorizationState.isAuthorized, let token = lastRegisteredToken {
                _ = try await api.upsertPushInstallation(
                    installationId: installationId,
                    request: UpsertPushInstallationRequest(
                        installationId: installationId,
                        platform: "ios",
                        deviceToken: token,
                        deviceName: UIDevice.current.name,
                        bundleId: Bundle.main.bundleIdentifier
                    )
                )
            }

            let status = try await api.getPushRegistrationStatus(installationId: installationId)
            serverSupportsPush = true
            registrationStatus = status
            lastError = nil
        } catch let error as APIError {
            handleApiError(error)
        } catch {
            lastError = error.localizedDescription
        }
    }

    func requestPushAuthorization() async {
        do {
            let granted = try await UNUserNotificationCenter.current().requestAuthorization(options: [.alert, .sound, .badge])
            await refreshAuthorizationState()
            if granted {
                UIApplication.shared.registerForRemoteNotifications()
            }
        } catch {
            lastError = error.localizedDescription
        }
    }

    func didRegisterForRemoteNotifications(deviceToken: Data) async {
        let token = deviceToken.map { String(format: "%02x", $0) }.joined()
        defaults.set(token, forKey: deviceTokenKey)
        lastRegisteredToken = token
        lastError = nil
        await refreshStatus()
    }

    func didFailToRegisterForRemoteNotifications(error: Error) {
        lastError = error.localizedDescription
    }

    private func refreshAuthorizationState() async {
        let settings = await UNUserNotificationCenter.current().notificationSettings()
        switch settings.authorizationStatus {
        case .authorized:
            authorizationState = .authorized
        case .denied:
            authorizationState = .denied
        case .provisional:
            authorizationState = .provisional
        case .ephemeral:
            authorizationState = .ephemeral
        case .notDetermined:
            authorizationState = .notDetermined
        @unknown default:
            authorizationState = .notDetermined
        }
    }

    private func handleApiError(_ error: APIError) {
        switch error {
        case .serverError(let code, _) where code == 404:
            serverSupportsPush = false
            registrationStatus = nil
            lastError = nil
        default:
            lastError = error.localizedDescription
        }
    }
}

final class PushNotificationAppDelegate: NSObject, UIApplicationDelegate {
    weak var store: PushNotificationStore?

    func application(_ application: UIApplication, didRegisterForRemoteNotificationsWithDeviceToken deviceToken: Data) {
        Task { @MainActor in
            await store?.didRegisterForRemoteNotifications(deviceToken: deviceToken)
        }
    }

    func application(_ application: UIApplication, didFailToRegisterForRemoteNotificationsWithError error: Error) {
        Task { @MainActor in
            store?.didFailToRegisterForRemoteNotifications(error: error)
        }
    }
}
