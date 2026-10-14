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

func bestMatch<T>(from items: [T], query: String, key: (T) -> String) -> T? {
    let lower = query.lowercased()
    if let m = items.first(where: { key($0).lowercased() == lower }) { return m }
    if let m = items.first(where: { key($0).lowercased().hasPrefix(lower) }) { return m }
    return items.first(where: { key($0).lowercased().contains(lower) })
}
