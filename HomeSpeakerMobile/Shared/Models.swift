import Foundation

// MARK: - Server Connection

struct ServerConnection: Codable, Identifiable, Equatable, Hashable {
    var id: UUID
    var name: String
    var host: String
    var port: Int

    init(id: UUID = UUID(), name: String, host: String, port: Int = 5000) {
        self.id = id
        self.name = name
        self.host = host
        self.port = port
    }

    var baseURL: URL {
        let cleanHost = host.trimmingCharacters(in: .whitespaces)
        if cleanHost.hasPrefix("http://") || cleanHost.hasPrefix("https://") {
            return URL(string: cleanHost)!
        }
        return URL(string: "http://\(cleanHost):\(port)")!
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

    var isPlaying: Bool { stillPlaying }

    var elapsedFormatted: String {
        elapsed.flatMap { formatDuration($0) } ?? "0:00"
    }

    var remainingFormatted: String {
        remaining.flatMap { formatDuration($0) } ?? "0:00"
    }

    private func formatDuration(_ value: String) -> String? {
        let parts = value.split(separator: ":").map { Int($0) ?? 0 }
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

struct TemperatureStatus: Codable {
    let outsideTemperature: Double?
    let youngerGirlsRoomTemperature: Double?
    let olderGirlsRoomTemperature: Double?
    let boysRoomTemperature: Double?
    let momAndDadsRoomTemperature: Double?
    let greenhouseTemperature: Double?
    let shouldWindowsBeClosed: Bool?
    let temperatureDifference: Double?
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
