import ActivityKit
import Foundation
import Observation

/// Starts, updates and ends the speaker Live Activity. Each activity's push
/// token goes to its speaker, which then pushes playback changes to the Lock
/// Screen while the app is suspended.
@MainActor
@Observable
final class SpeakerActivityController {
    static let shared = SpeakerActivityController()

    /// Server ids (UUID strings) that have a Live Activity running.
    private(set) var activeServerIds: Set<String> = []
    private(set) var lastError: String?

    @ObservationIgnored private var observedActivityIds: Set<String> = []

    private init() {}

    var areActivitiesEnabled: Bool {
        ActivityAuthorizationInfo().areActivitiesEnabled
    }

    func isActive(for connection: ServerConnection) -> Bool {
        activeServerIds.contains(connection.id.uuidString)
    }

    /// Picks up activities that outlived the previous app launch.
    func reattach() {
        for activity in Activity<SpeakerActivityAttributes>.activities {
            observe(activity)
        }
        refreshActiveIds()
    }

    func start(for connection: ServerConnection, status: PlayerStatus) async {
        lastError = nil
        if isActive(for: connection) {
            await update(for: connection, status: status)
            return
        }
        guard areActivitiesEnabled else {
            lastError = "Live Activities are turned off for HomeSpeaker in Settings."
            return
        }
        let attributes = SpeakerActivityAttributes(serverId: connection.id.uuidString,
                                                   serverName: connection.name)
        let content = ActivityContent(state: SpeakerActivityAttributes.ContentState(status: status),
                                      staleDate: .now.addingTimeInterval(30 * 60))
        do {
            let activity = try Activity.request(attributes: attributes, content: content, pushType: .token)
            observe(activity)
            refreshActiveIds()
        } catch {
            lastError = error.localizedDescription
        }
    }

    func end(for connection: ServerConnection) async {
        for activity in activities(for: connection) {
            await activity.end(nil, dismissalPolicy: .immediate)
        }
        refreshActiveIds()
    }

    func update(for connection: ServerConnection, status: PlayerStatus) async {
        guard isActive(for: connection) else { return }
        await SpeakerSurfaces.updateLiveActivities(for: connection, status: status)
    }

    // MARK: - Private

    private func activities(for connection: ServerConnection) -> [Activity<SpeakerActivityAttributes>] {
        Activity<SpeakerActivityAttributes>.activities
            .filter { $0.attributes.serverId == connection.id.uuidString }
    }

    private func refreshActiveIds() {
        activeServerIds = Set(Activity<SpeakerActivityAttributes>.activities
            .filter { $0.activityState == .active || $0.activityState == .stale }
            .map(\.attributes.serverId))
    }

    private func observe(_ activity: Activity<SpeakerActivityAttributes>) {
        guard observedActivityIds.insert(activity.id).inserted else { return }
        let activityId = activity.id
        let serverId = activity.attributes.serverId

        let tokenTask = Task {
            for await tokenData in activity.pushTokenUpdates {
                let token = tokenData.map { String(format: "%02x", $0) }.joined()
                guard let connection = UUID(uuidString: serverId).flatMap(SharedConnections.connection(id:)) else { continue }
                try? await APIClient.client(for: connection).registerLiveActivity(id: activityId, pushToken: token)
            }
        }

        Task { [weak self] in
            for await state in activity.activityStateUpdates {
                if state == .ended || state == .dismissed {
                    break
                }
            }
            tokenTask.cancel()
            if let connection = UUID(uuidString: serverId).flatMap(SharedConnections.connection(id:)) {
                try? await APIClient.client(for: connection).unregisterLiveActivity(id: activityId)
            }
            self?.observedActivityIds.remove(activityId)
            self?.refreshActiveIds()
        }
    }
}
