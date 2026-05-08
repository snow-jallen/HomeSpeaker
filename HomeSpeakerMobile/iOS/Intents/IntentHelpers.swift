import AppIntents
import Foundation

// MARK: - Passthrough entity for free-form spoken text

struct MediaQueryEntity: AppEntity {
    static var typeDisplayRepresentation = TypeDisplayRepresentation(name: "Media")
    static var defaultQuery = MediaQueryEntityQuery()

    var id: String

    var displayRepresentation: DisplayRepresentation {
        DisplayRepresentation(title: "\(id)")
    }

    init(id: String) {
        self.id = id
    }
}

struct MediaQueryEntityQuery: EntityQuery, EntityStringQuery {
    func entities(for identifiers: [String]) async throws -> [MediaQueryEntity] {
        identifiers.map { MediaQueryEntity(id: $0) }
    }

    func entities(matching string: String) async throws -> [MediaQueryEntity] {
        [MediaQueryEntity(id: string)]
    }

    func suggestedEntities() async throws -> [MediaQueryEntity] { [] }
}

// MARK: - Helpers

func storedConnections() -> [ServerConnection] {
    guard let data = UserDefaults.standard.data(forKey: "hs_connections"),
          let connections = try? JSONDecoder().decode([ServerConnection].self, from: data) else {
        return []
    }
    return connections
}

/// Parses "Kings Singers on kitchen" → ("Kings Singers", Kitchen connection).
/// Uses the last " on " occurrence so artist names like "Kings of Leon" work correctly.
func parseContentAndServer(_ input: String) -> (content: String, server: ServerConnection?) {
    let connections = storedConnections()
    guard !connections.isEmpty else { return (input, nil) }
    let lower = input.lowercased()
    if let onRange = lower.range(of: " on ", options: .backwards) {
        let serverHint = String(lower[onRange.upperBound...]).trimmingCharacters(in: .whitespaces)
        if let found = connections.first(where: {
            let name = $0.name.lowercased()
            return name.contains(serverHint) || serverHint.contains(name)
        }) {
            let content = String(input[input.startIndex..<onRange.lowerBound])
            return (content, found)
        }
    }
    return (input, nil)
}

func resolveServer(_ entity: ServerEntity?) throws -> ServerConnection {
    let connections = storedConnections()
    guard !connections.isEmpty else { throw HomeSpeakerIntentError.noServers }
    if let entity = entity {
        let lower = entity.id.lowercased()
        if let found = connections.first(where: { $0.name.lowercased().contains(lower) }) {
            return found
        }
        throw HomeSpeakerIntentError.serverNotFound(entity.id)
    }
    if let idStr = UserDefaults.standard.string(forKey: "hs_selectedId"),
       let id = UUID(uuidString: idStr),
       let conn = connections.first(where: { $0.id == id }) {
        return conn
    }
    return connections.first!
}

func bestMatch<T>(from items: [T], query: String, key: (T) -> String) -> T? {
    let lower = query.lowercased()
    if let m = items.first(where: { key($0).lowercased() == lower }) { return m }
    if let m = items.first(where: { key($0).lowercased().hasPrefix(lower) }) { return m }
    return items.first(where: { key($0).lowercased().contains(lower) })
}

// MARK: - Errors

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
