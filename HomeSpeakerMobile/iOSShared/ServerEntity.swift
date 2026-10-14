import AppIntents

/// A saved HomeSpeaker server, as Siri, Shortcuts and widgets see it.
struct ServerEntity: AppEntity {
    static var typeDisplayRepresentation = TypeDisplayRepresentation(name: "HomeSpeaker Server")
    static var defaultQuery = ServerEntityQuery()

    /// The server's UUID. Shortcuts saved by older builds used the server's
    /// name instead, so lookups accept either.
    var id: String
    var name: String

    var displayRepresentation: DisplayRepresentation {
        DisplayRepresentation(title: "\(name)")
    }

    init(id: String, name: String) {
        self.id = id
        self.name = name
    }

    init(connection: ServerConnection) {
        self.init(id: connection.id.uuidString, name: connection.name)
    }
}

struct ServerEntityQuery: EntityQuery, EntityStringQuery {
    func entities(for identifiers: [String]) async throws -> [ServerEntity] {
        let connections = SharedConnections.load()
        return identifiers.compactMap { identifier in
            connections.first { $0.id.uuidString == identifier || $0.name == identifier }
                .map(ServerEntity.init(connection:))
        }
    }

    func entities(matching string: String) async throws -> [ServerEntity] {
        let lower = string.lowercased()
        return SharedConnections.load()
            .filter { $0.name.lowercased().contains(lower) }
            .map(ServerEntity.init(connection:))
    }

    func suggestedEntities() async throws -> [ServerEntity] {
        SharedConnections.load().map(ServerEntity.init(connection:))
    }
}

// MARK: - Resolving a server

func storedConnections() -> [ServerConnection] {
    SharedConnections.load()
}

/// The server an intent should act on: the one asked for, else the server
/// selected in the app.
func resolveServer(_ entity: ServerEntity?) throws -> ServerConnection {
    let connections = storedConnections()
    guard !connections.isEmpty else { throw HomeSpeakerIntentError.noServers }
    if let entity {
        if let byId = connections.first(where: { $0.id.uuidString == entity.id }) {
            return byId
        }
        let lower = entity.id.lowercased()
        if let byName = connections.first(where: { $0.name.lowercased().contains(lower) }) {
            return byName
        }
        throw HomeSpeakerIntentError.serverNotFound(entity.name)
    }
    return SharedConnections.selected() ?? connections[0]
}

enum HomeSpeakerIntentError: LocalizedError {
    case noServers
    case serverNotFound(String)
    case contentNotFound(String)

    var errorDescription: String? {
        switch self {
        case .noServers: return "No HomeSpeaker servers are configured."
        case .serverNotFound(let name): return "No HomeSpeaker server named \"\(name)\" was found."
        case .contentNotFound(let name): return "Could not find \"\(name)\"."
        }
    }
}
