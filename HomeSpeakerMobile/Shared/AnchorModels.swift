import Foundation

// MARK: - Anchors (mirrors HomeSpeaker.Shared/Anchors; see docs/superpowers/specs/2026-11-04-anchors-design.md)
//
// Dates arrive as "yyyy-MM-dd" strings and are kept as strings: the server owns
// "today" (its board says which day it is), so the app never computes it.

struct AnchorBoard: Codable {
    let today: String
    let from: String
    let to: String
    let people: [AnchorPerson]
    let feed: AnchorFeedStatus?
    let stale: Bool?
}

struct AnchorPerson: Codable, Identifiable, Hashable {
    let id: String
    let name: String
    let color: String
    let emoji: String?
    /// "local", or the name of the app the person comes from.
    let source: String
    let streak: Int
    let todayDone: Int
    let todayTotal: Int
    let anchors: [Anchor]
    let feedPersonId: String?

    var isLocal: Bool { source == "local" }
}

struct Anchor: Codable, Identifiable, Hashable {
    let id: String
    let title: String
    /// "morning", "day" or "evening".
    let slot: String
    let order: Int
    /// "HH:mm"
    let targetTime: String?
    /// Weekday mask: Sunday = 1, Monday = 2 ... Saturday = 64; 127 = every day.
    let days: Int
    let retired: Bool
    let periods: [AnchorPeriod]?
    let source: String
    let editable: Bool
    let writable: Bool
    let streak: Int
    let cells: [AnchorCell]

    func cell(on date: String) -> AnchorCell? {
        cells.first { $0.date == date }
    }
}

struct AnchorPeriod: Codable, Hashable {
    let from: String
    let to: String?
    let days: Int
}

struct AnchorCell: Codable, Hashable {
    let date: String
    /// kept | missed | open | off | none | unknown
    let state: String
    let pending: Bool?
    let doneAt: String?
    /// "HH:mm", e.g. when someone actually went to bed.
    let actualTime: String?

    var isKept: Bool { state == "kept" }
}

struct AnchorFeedStatus: Codable, Hashable {
    let name: String
    /// ok | stale | unreachable
    let status: String
    let asOf: String?
    let error: String?
    let people: [AnchorFeedPerson]?
}

struct AnchorFeedPerson: Codable, Hashable, Identifiable {
    let id: String
    let name: String
}

struct AnchorPersonRequest: Codable {
    let name: String
    var color: String? = nil
    var emoji: String? = nil
    var feedPersonId: String? = nil
}

struct AnchorRequest: Codable {
    let title: String
    var slot: String? = nil
    /// "HH:mm"; empty clears it; nil keeps it.
    var targetTime: String? = nil
    var days: Int? = nil
}

struct AnchorOrderRequest: Codable {
    let ids: [String]
}

struct AnchorLogRequest: Codable {
    let done: Bool
    /// ISO-8601: when the person tapped. The newest tap wins on the server.
    let clientUpdatedAt: String
}

enum AnchorSlots {
    static let all = ["morning", "day", "evening"]

    static func label(_ slot: String) -> String {
        switch slot {
        case "morning": return "Morning"
        case "evening": return "Evening"
        default: return "During the day"
        }
    }

    static func rank(_ slot: String) -> Int {
        all.firstIndex(of: slot) ?? all.count
    }

    /// Anchors grouped Morning / Day / Evening, each in its saved order; empty slots left out.
    static func grouped(_ anchors: [Anchor]) -> [(slot: String, anchors: [Anchor])] {
        all.compactMap { slot in
            let inSlot = anchors.filter { $0.slot == slot }.sorted { $0.order < $1.order }
            return inSlot.isEmpty ? nil : (slot, inSlot)
        }
    }
}

enum AnchorDays {
    static let every = 127

    /// Monday first, the way a family week reads. Bits: Sunday = 1 ... Saturday = 64.
    static let week: [(bit: Int, short: String, name: String)] = [
        (2, "M", "Monday"), (4, "T", "Tuesday"), (8, "W", "Wednesday"), (16, "T", "Thursday"),
        (32, "F", "Friday"), (64, "S", "Saturday"), (1, "S", "Sunday"),
    ]

    static func label(_ days: Int) -> String {
        switch days & every {
        case every, 0: return "Every day"
        case 62: return "Weekdays"
        case 65: return "Weekends"
        default: return week.filter { days & $0.bit != 0 }.map { String($0.name.prefix(3)) }.joined(separator: ", ")
        }
    }
}

enum AnchorTime {
    /// "06:30" -> "6:30 AM" in the device's locale.
    static func display(_ hhmm: String) -> String {
        let parts = hhmm.split(separator: ":").compactMap { Int($0) }
        guard parts.count == 2,
              let date = Calendar.current.date(bySettingHour: parts[0], minute: parts[1], second: 0, of: .now) else {
            return hhmm
        }
        return date.formatted(date: .omitted, time: .shortened)
    }

    static func wire(_ date: Date) -> String {
        let c = Calendar.current.dateComponents([.hour, .minute], from: date)
        return String(format: "%02d:%02d", c.hour ?? 0, c.minute ?? 0)
    }

    static func date(_ hhmm: String?) -> Date? {
        guard let hhmm else { return nil }
        let parts = hhmm.split(separator: ":").compactMap { Int($0) }
        guard parts.count == 2 else { return nil }
        return Calendar.current.date(bySettingHour: parts[0], minute: parts[1], second: 0, of: .now)
    }

    /// The small line under an anchor: its times, why it looks the way it does, its streak.
    static func meta(for anchor: Anchor, cell: AnchorCell?) -> String {
        if cell?.pending == true { return "Waiting to sync" }
        if cell?.state == "off" { return "Not scheduled today" }
        if cell?.state == "unknown" { return "No data" }
        var parts: [String] = []
        if let actual = cell?.actualTime {
            parts.append(display(actual))
            if let target = anchor.targetTime { parts.append("target " + display(target)) }
        } else if let target = anchor.targetTime {
            parts.append("by " + display(target))
        }
        if anchor.streak >= 2 { parts.append("\(anchor.streak)-day streak") }
        return parts.joined(separator: " · ")
    }
}

/// Which anchor person this device belongs to, remembered per server.
enum AnchorMe {
    private static func key(_ serverId: UUID) -> String { "hs_anchor_me_\(serverId.uuidString)" }

    static func get(serverId: UUID) -> String? {
        SharedConnections.defaults.string(forKey: key(serverId))
    }

    static func set(_ personId: String?, serverId: UUID) {
        SharedConnections.defaults.set(personId, forKey: key(serverId))
    }
}
