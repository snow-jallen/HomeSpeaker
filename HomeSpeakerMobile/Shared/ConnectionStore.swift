import Foundation
import Observation

@Observable
class ConnectionStore {
    private let connectionsKey = "hs_connections"
    private let selectedIdKey = "hs_selectedId"
    #if os(watchOS)
    private let defaults = UserDefaults(suiteName: "group.com.homespeaker") ?? .standard
    #else
    private let defaults: UserDefaults = .standard
    #endif

    var connections: [ServerConnection] = []
    var selectedConnection: ServerConnection?

    var api: APIClient? {
        guard let conn = selectedConnection else { return nil }
        return APIClient(baseURL: conn.baseURL)
    }

    init() {
        load()
    }

    func add(_ connection: ServerConnection) {
        connections.append(connection)
        if connections.count == 1 {
            selectedConnection = connection
        }
        save()
    }

    func update(_ connection: ServerConnection) {
        if let i = connections.firstIndex(where: { $0.id == connection.id }) {
            connections[i] = connection
            if selectedConnection?.id == connection.id {
                selectedConnection = connection
            }
        }
        save()
    }

    func remove(_ connection: ServerConnection) {
        connections.removeAll { $0.id == connection.id }
        if selectedConnection?.id == connection.id {
            selectedConnection = connections.first
        }
        save()
    }

    func select(_ connection: ServerConnection) {
        selectedConnection = connection
        save()
    }

    func receiveFromPhone(_ connections: [ServerConnection], selectedId: UUID?) {
        self.connections = connections
        if let id = selectedId, let match = connections.first(where: { $0.id == id }) {
            selectedConnection = match
        } else if selectedConnection == nil || !connections.contains(where: { $0.id == selectedConnection?.id }) {
            selectedConnection = connections.first
        }
        save()
    }

    private func save() {
        if let data = try? JSONEncoder().encode(connections) {
            defaults.set(data, forKey: connectionsKey)
        }
        defaults.set(selectedConnection?.id.uuidString, forKey: selectedIdKey)
        #if os(iOS)
        WatchSync.shared.send(connections: connections, selectedId: selectedConnection?.id)
        #endif
    }

    private func load() {
        if let data = defaults.data(forKey: connectionsKey),
           let saved = try? JSONDecoder().decode([ServerConnection].self, from: data) {
            connections = saved
        }
        if let idStr = defaults.string(forKey: selectedIdKey),
           let id = UUID(uuidString: idStr) {
            selectedConnection = connections.first { $0.id == id }
        } else {
            selectedConnection = connections.first
        }
    }
}
