import AppIntents

struct ServerEntity: AppEntity {
    static var typeDisplayRepresentation = TypeDisplayRepresentation(name: "HomeSpeaker Server")
    static var defaultQuery = ServerEntityQuery()

    var id: String

    var displayRepresentation: DisplayRepresentation {
        DisplayRepresentation(title: "\(id)")
    }

    init(id: String) {
        self.id = id
    }
}

struct ServerEntityQuery: EntityQuery, EntityStringQuery {
    func entities(for identifiers: [String]) async throws -> [ServerEntity] {
        storedConnections()
            .filter { identifiers.contains($0.name) }
            .map { ServerEntity(id: $0.name) }
    }

    func entities(matching string: String) async throws -> [ServerEntity] {
        let lower = string.lowercased()
        return storedConnections()
            .filter { $0.name.lowercased().contains(lower) }
            .map { ServerEntity(id: $0.name) }
    }

    func suggestedEntities() async throws -> [ServerEntity] {
        storedConnections().map { ServerEntity(id: $0.name) }
    }
}
