import Foundation

// MARK: - Server Connection

struct ServerConnection: Codable, Identifiable, Equatable, Hashable {
    var id: UUID
    var name: String
    var host: String
    var port: Int
    /// Optional second address used when the home address can't be reached,
    /// e.g. "kitchen.boston-cloud.ts.net" over Tailscale. Defaults to https.
    var remoteURL: String?

    init(id: UUID = UUID(), name: String, host: String, port: Int = 80, remoteURL: String? = nil) {
        self.id = id
        self.name = name
        self.host = host
        self.port = port
        self.remoteURL = remoteURL
    }

    /// The home address. A host that can't form a URL (a typo, or a bad
    /// discovery record) falls back to `http://localhost` rather than crashing;
    /// requests to it simply fail.
    var baseURL: URL {
        let cleanHost = host.trimmingCharacters(in: .whitespaces)
        if cleanHost.hasPrefix("http://") || cleanHost.hasPrefix("https://") {
            return URL(string: cleanHost) ?? Self.fallbackURL
        }
        return URL(string: "http://\(cleanHost):\(port)") ?? Self.fallbackURL
    }

    private static let fallbackURL = URL(string: "http://localhost")!

    var remoteBaseURL: URL? {
        guard let raw = remoteURL?.trimmingCharacters(in: .whitespacesAndNewlines), !raw.isEmpty else {
            return nil
        }
        return URL(string: raw.contains("://") ? raw : "https://\(raw)")
    }

    /// Addresses to try, home address first.
    var candidateBaseURLs: [URL] {
        guard let remote = remoteBaseURL, remote != baseURL else { return [baseURL] }
        return [baseURL, remote]
    }
}

// MARK: - Music Models

struct Song: Codable, Identifiable, Hashable {
    let songId: Int
    let name: String
    let path: String?
    let album: String?
    let artist: String?

    var id: Int { songId }

    var displayTitle: String { name }
    var displayArtist: String { artist ?? "Unknown Artist" }
    var displayAlbum: String { album ?? "Unknown Album" }
}

struct Playlist: Codable, Identifiable, Hashable {
    let name: String
    var alwaysShuffle: Bool
    var songs: [Song]

    var id: String { name }

    enum CodingKeys: String, CodingKey {
        case name, alwaysShuffle, songs
    }
}

struct RadioStream: Codable, Identifiable, Hashable {
    let id: Int
    let name: String
    let url: String
    let faviconFileName: String?
    let playCount: Int
    let displayOrder: Int
}

/// What the server's "play ..." search picked. Kind is Playlist, Stream, Artist, Album or Song.
struct MediaMatch: Codable, Hashable {
    let kind: String
    let name: String
    let artist: String?
}

struct VideoDto: Codable, Identifiable, Hashable {
    let title: String
    let id: String
    let url: String
    let thumbnail: String?
    let author: String?
    let duration: String?
}

// MARK: - Player Models

struct PlayerStatus: Codable {
    let elapsed: String?
    let remaining: String?
    let stillPlaying: Bool
    let percentComplete: Double
    let currentSong: Song?
    let volume: Int
    let sleepTimerActive: Bool?
    let sleepTimerRemainingMinutes: Double?
    let repeatMode: Bool?
    let isStream: Bool?
    let streamName: String?

    var isPlaying: Bool { stillPlaying }
    var quietDownVolume: Int { volume > 0 ? max(1, volume / 2) : 0 }

    /// The playing song from the library. For radio the server reports a
    /// stand-in "song" (the station's now-playing title, with the stream URL
    /// as its path), which this leaves out.
    var librarySong: Song? {
        isStream == true ? nil : currentSong
    }

    /// What to show as the headline: the song (or radio now-playing title),
    /// else the station. Matches the Live Activity title the server pushes.
    var displayTitle: String {
        if let name = currentSong?.name, !name.isEmpty { return name }
        if let streamName, !streamName.isEmpty { return streamName }
        return stillPlaying ? "Streaming" : "Not Playing"
    }

    /// The artist, or for radio the station under its now-playing title.
    var displaySubtitle: String {
        if isStream == true {
            guard let streamName, !streamName.isEmpty, streamName != displayTitle else { return "" }
            return streamName
        }
        return currentSong?.artist ?? ""
    }

    var elapsedSeconds: Double { elapsed.flatMap(Self.seconds) ?? 0 }
    var remainingSeconds: Double { remaining.flatMap(Self.seconds) ?? 0 }

    /// Parses a .NET TimeSpan ("hh:mm:ss.fffffff", or "d.hh:mm:ss") into seconds.
    static func seconds(_ value: String) -> Double? {
        var parts = value.split(separator: ":").map(String.init)
        guard parts.count == 3 else { return nil }
        var days = 0.0
        let hourPart = parts[0].split(separator: ".")
        if hourPart.count == 2 {
            days = Double(hourPart[0]) ?? 0
            parts[0] = String(hourPart[1])
        }
        guard let h = Double(parts[0]), let m = Double(parts[1]), let s = Double(parts[2]) else { return nil }
        return days * 86_400 + h * 3_600 + m * 60 + s
    }

    var elapsedFormatted: String {
        elapsed.flatMap { formatDuration($0) } ?? "0:00"
    }

    var remainingFormatted: String {
        remaining.flatMap { formatDuration($0) } ?? "0:00"
    }

    private func formatDuration(_ value: String) -> String? {
        // TimeSpans arrive as "hh:mm:ss.fffffff" - the seconds component has a
        // fractional part, so parse as Double (Int("31.75") is nil, which froze
        // the displayed time at whole minutes).
        let parts = value.split(separator: ":").map { Int(Double($0) ?? 0) }
        if parts.count == 3 {
            let h = parts[0], m = parts[1], s = parts[2]
            if h > 0 {
                return String(format: "%d:%02d:%02d", h, m, s)
            }
            return String(format: "%d:%02d", m, s)
        }
        return value
    }
}

// MARK: - Sensor Models

struct Features: Codable {
    let temperatureEnabled: Bool
    let bloodSugarEnabled: Bool
    let forecastEnabled: Bool?
    let anchorsEnabled: Bool?
    /// False when the server has no APNs configured (its push and Live
    /// Activity endpoints then 404). Nil from servers that predate the flag.
    let pushEnabled: Bool?
    /// "F" or "C". Temperatures always arrive in °F; this is how to show them.
    let temperatureUnit: String?

    /// Converts a °F reading from the server into the server's display unit.
    func displayTemperature(_ fahrenheit: Double) -> Double {
        temperatureUnit?.uppercased() == "C" ? (fahrenheit - 32) * 5 / 9 : fahrenheit
    }

    var temperatureUnitSymbol: String {
        temperatureUnit?.uppercased() == "C" ? "°C" : "°F"
    }
}

struct ForecastData: Codable {
    let dateTime: String?
    let temperature: Double?
    let conditions: String?
    let iconUrl: String?
    let precipitationChance: Double?
}

struct ForecastStatus: Codable {
    let tonightLow: ForecastData?
    let todayHigh: ForecastData?
    let tomorrowHigh: ForecastData?
    let lastUpdated: String?
    let lastCachedAt: String?
}

struct BloodSugarReading: Codable {
    let sgv: Double?
    let date: String?
    let direction: String?
    let type: String?
}

struct BloodSugarStatus: Codable {
    let currentReading: BloodSugarReading?
    let lastUpdated: String?
    let isStale: Bool?
    let timeSinceLastReading: String?

    var directionArrow: String {
        switch currentReading?.direction {
        case "DoubleUp": return "↑↑"
        case "SingleUp": return "↑"
        case "FortyFiveUp": return "↗"
        case "Flat": return "→"
        case "FortyFiveDown": return "↘"
        case "SingleDown": return "↓"
        case "DoubleDown": return "↓↓"
        default: return "?"
        }
    }
}

/// One room's sensor reading (°F), as the server's `RoomReading`.
struct RoomReading: Codable, Hashable {
    let name: String
    let temperature: Double?
    let batteryPercent: Double?
}

struct TemperatureStatus: Codable {
    let outsideTemperature: Double?
    let outsideRoomName: String?
    let indoorRoomName: String?
    let indoorTemperature: Double?
    /// Every configured room (the outside one included), in the server's order.
    let rooms: [RoomReading]?
    let shouldWindowsBeClosed: Bool?
    let temperatureDifference: Double?
}

// MARK: - Push Notifications

struct PushRegistrationStatusDto: Codable {
    let installationId: String
    let platform: String?
    let isRegistered: Bool
    let bundleId: String?
    let deviceTokenUpdatedUtc: String?

    enum CodingKeys: String, CodingKey {
        case installationId
        case platform
        case isRegistered
        case bundleId
        case deviceTokenUpdatedUtc
    }

    init(from decoder: Decoder) throws {
        let container = try decoder.container(keyedBy: CodingKeys.self)
        installationId = (try? container.decode(String.self, forKey: .installationId)) ?? ""
        platform = try? container.decode(String.self, forKey: .platform)
        isRegistered = (try? container.decode(Bool.self, forKey: .isRegistered)) ?? false
        bundleId = try? container.decode(String.self, forKey: .bundleId)
        deviceTokenUpdatedUtc = try? container.decode(String.self, forKey: .deviceTokenUpdatedUtc)
    }
}

// MARK: - Request Bodies (used by APIClient)

struct PlayerControlRequest: Codable {
    let stop: Bool
    let play: Bool
    let clearQueue: Bool
    let skipToNext: Bool
    let setVolume: Bool
    let volumeLevel: Int
}

struct SetSleepTimerRequest: Codable {
    let minutes: Int
}

struct SetRepeatModeRequest: Codable {
    let enabled: Bool
}

struct LiveActivityRegistrationRequest: Codable {
    let pushToken: String
}

struct RenamePlaylistRequest: Codable {
    let newName: String
}

struct AddSongRequest: Codable {
    let songPath: String
}

struct RemoveSongRequest: Codable {
    let songPath: String
}

struct ReorderRequest: Codable {
    let songPaths: [String]
}

struct UpdateQueueRequest: Codable {
    let songs: [String]
}

struct CreateStreamRequest: Codable {
    let name: String
    let url: String
}

struct PlayStreamRequest: Codable {
    let streamUrl: String
}

struct CacheVideoRequest: Codable {
    let video: VideoDto
}

struct UpdateSongRequest: Codable {
    let name: String
    let artist: String
    let album: String
}

struct UpsertPushInstallationRequest: Codable {
    let installationId: String
    let platform: String
    let deviceToken: String
    let deviceName: String?
    let bundleId: String?
}
