import Foundation
import WatchConnectivity

class WatchSync: NSObject, WCSessionDelegate {
    static let shared = WatchSync()
    private override init() { super.init() }

    private let connectionsKey = "hs_connections"
    private let selectedIdKey = "hs_selectedId"

    var onConnectionsReceived: (([ServerConnection], UUID?) -> Void)?

    func activate() {
        guard WCSession.isSupported() else { return }
        WCSession.default.delegate = self
        WCSession.default.activate()
    }

    func send(connections: [ServerConnection], selectedId: UUID?) {
        guard WCSession.isSupported(),
              WCSession.default.activationState == .activated,
              let data = try? JSONEncoder().encode(connections) else { return }
        #if os(iOS)
        guard WCSession.default.isPaired,
              WCSession.default.isWatchAppInstalled else { return }
        #endif
        var context: [String: Any] = [connectionsKey: data]
        if let id = selectedId {
            context[selectedIdKey] = id.uuidString
        }
        try? WCSession.default.updateApplicationContext(context)
    }

    // MARK: - WCSessionDelegate

    func session(_ session: WCSession, activationDidCompleteWith activationState: WCSessionActivationState, error: Error?) {}

    func session(_ session: WCSession, didReceiveApplicationContext applicationContext: [String: Any]) {
        guard let data = applicationContext[connectionsKey] as? Data,
              let connections = try? JSONDecoder().decode([ServerConnection].self, from: data) else { return }
        let selectedId = (applicationContext[selectedIdKey] as? String).flatMap(UUID.init)
        DispatchQueue.main.async {
            self.onConnectionsReceived?(connections, selectedId)
        }
    }

    #if os(iOS)
    func sessionDidBecomeInactive(_ session: WCSession) {}
    func sessionDidDeactivate(_ session: WCSession) {
        WCSession.default.activate()
    }
    #endif
}
