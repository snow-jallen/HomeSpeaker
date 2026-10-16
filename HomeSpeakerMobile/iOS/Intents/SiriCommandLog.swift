import Foundation

/// Every Siri command the app handled: what Siri captured, what the app made of it, and what
/// the server did. Shown on the Siri Command History page so a command that misbehaves can be
/// diagnosed from the phone.
enum SiriCommandLog {
    struct Entry: Codable, Identifiable {
        var id = UUID()
        var date: Date
        /// Which command ran, e.g. "Play" or "Quiet Down".
        var command: String
        /// What Siri handed the app: the spoken name, genres, server and other parameters.
        var captured: String
        /// What the app made of it: the server and the request it sent.
        var translated: String
        /// What the server did, or why it didn't.
        var result: String
    }

    private static let key = "hs_siriCommandLog"
    private static let limit = 100
    private static let lock = NSLock()

    // The App Group, so intents run outside the app process can record too.
    private static var defaults: UserDefaults {
        UserDefaults(suiteName: SharedConnections.appGroup) ?? .standard
    }

    static func entries() -> [Entry] {
        lock.withLock { load() }
    }

    /// Records a command and returns its id, for updating once the server answers.
    @discardableResult
    static func add(command: String, captured: String, translated: String, result: String) -> UUID {
        let entry = Entry(date: .now, command: command, captured: captured, translated: translated, result: result)
        lock.withLock { save([entry] + load().prefix(limit - 1)) }
        return entry.id
    }

    /// Replaces a recorded command's result, e.g. when Siri's play step follows its search step.
    static func update(_ id: UUID, result: String) {
        lock.withLock {
            var entries = load()
            guard let index = entries.firstIndex(where: { $0.id == id }) else { return }
            entries[index].result = result
            save(entries)
        }
    }

    static func clear() {
        lock.withLock { defaults.removeObject(forKey: key) }
    }

    private static func load() -> [Entry] {
        guard let data = defaults.data(forKey: key) else { return [] }
        return (try? JSONDecoder().decode([Entry].self, from: data)) ?? []
    }

    private static func save(_ entries: [Entry]) {
        if let data = try? JSONEncoder().encode(entries) {
            defaults.set(data, forKey: key)
        }
    }
}
