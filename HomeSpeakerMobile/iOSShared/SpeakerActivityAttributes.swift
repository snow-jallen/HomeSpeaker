import ActivityKit
import Foundation

/// The Lock Screen / Dynamic Island Live Activity for one speaker.
///
/// The server sends ContentState in APNs pushes, so its property names are a
/// contract with the server (see the live-activity push code there). Dates go
/// over the wire as Unix seconds because ActivityKit's own date decoding isn't
/// what the server would produce.
struct SpeakerActivityAttributes: ActivityAttributes {
    struct ContentState: Codable, Hashable {
        var title: String
        var artist: String
        var isPlaying: Bool
        var isStream: Bool
        var volume: Int
        var repeatMode: Bool
        var sleepTimerEndsAtUnix: Double?
        var updatedAtUnix: Double

        var sleepTimerEndsAt: Date? {
            sleepTimerEndsAtUnix.map(Date.init(timeIntervalSince1970:))
        }

        var updatedAt: Date { Date(timeIntervalSince1970: updatedAtUnix) }

        init(status: PlayerStatus, now: Date = .now) {
            title = status.displayTitle
            artist = status.displaySubtitle
            isPlaying = status.isPlaying
            isStream = status.isStream ?? false
            volume = status.volume
            repeatMode = status.repeatMode ?? false
            if status.sleepTimerActive == true, let minutes = status.sleepTimerRemainingMinutes {
                sleepTimerEndsAtUnix = now.addingTimeInterval(minutes * 60).timeIntervalSince1970
            } else {
                sleepTimerEndsAtUnix = nil
            }
            updatedAtUnix = now.timeIntervalSince1970
        }
    }

    var serverId: String
    var serverName: String

    var server: ServerEntity { ServerEntity(id: serverId, name: serverName) }
}
