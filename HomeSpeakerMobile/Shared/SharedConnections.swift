import Foundation

/// The saved server list lives in the `group.com.homespeaker` App Group so the
/// app, its Siri intents, and the widget extensions all read the same servers.
enum SharedConnections {
    static let appGroup = "group.com.homespeaker"
    static let connectionsKey = "hs_connections"
    static let selectedIdKey = "hs_selectedId"
    private static let migratedKey = "hs_migratedToAppGroup"

    static var defaults: UserDefaults {
        UserDefaults(suiteName: appGroup) ?? .standard
    }

    static func load() -> [ServerConnection] {
        guard let data = defaults.data(forKey: connectionsKey),
              let connections = try? JSONDecoder().decode([ServerConnection].self, from: data) else {
            return []
        }
        return connections
    }

    static func selectedId() -> UUID? {
        defaults.string(forKey: selectedIdKey).flatMap(UUID.init)
    }

    static func setSelectedId(_ id: UUID?) {
        defaults.set(id?.uuidString, forKey: selectedIdKey)
    }

    /// The selected server, falling back to the first saved one.
    static func selected() -> ServerConnection? {
        let connections = load()
        if let id = selectedId(), let match = connections.first(where: { $0.id == id }) {
            return match
        }
        return connections.first
    }

    static func connection(id: UUID) -> ServerConnection? {
        load().first { $0.id == id }
    }

    /// Older builds saved servers in the app's own defaults, which extensions
    /// can't read. Copy them into the App Group once.
    static func migrateFromStandardDefaultsIfNeeded() {
        let group = defaults
        guard group !== UserDefaults.standard, !group.bool(forKey: migratedKey) else { return }
        defer { group.set(true, forKey: migratedKey) }

        let standard = UserDefaults.standard
        guard group.data(forKey: connectionsKey) == nil,
              let data = standard.data(forKey: connectionsKey) else { return }
        group.set(data, forKey: connectionsKey)
        if let selected = standard.string(forKey: selectedIdKey) {
            group.set(selected, forKey: selectedIdKey)
        }
    }
}
