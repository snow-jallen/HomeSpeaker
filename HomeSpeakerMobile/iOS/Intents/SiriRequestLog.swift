import Foundation

/// The last few "play ..." requests Siri handed the app and what came of them, shown on the
/// Siri Commands page so a request that does nothing can be diagnosed from the phone.
enum SiriRequestLog {
    struct Entry: Codable, Identifiable {
        var id = UUID()
        var date: Date
        var heard: String
        var outcome: String
    }

    private static let key = "hs_siriRequestLog"
    private static let limit = 10

    static func entries() -> [Entry] {
        guard let data = UserDefaults.standard.data(forKey: key) else { return [] }
        return (try? JSONDecoder().decode([Entry].self, from: data)) ?? []
    }

    static func add(heard: String, outcome: String) {
        let entries = [Entry(date: .now, heard: heard, outcome: outcome)] + entries().prefix(limit - 1)
        if let data = try? JSONEncoder().encode(entries) {
            UserDefaults.standard.set(data, forKey: key)
        }
    }
}
