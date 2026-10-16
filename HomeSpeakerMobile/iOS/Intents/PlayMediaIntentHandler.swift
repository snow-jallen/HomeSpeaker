import Foundation
import Intents

/// Handles Siri's own "play" command: "Play King's Singers on Home Speaker". Siri hears any
/// name, so the server's media search decides whether it's a playlist, stream, AI mix,
/// artist, album or song. The app handles it in-process (see the app delegate), with no
/// Intents extension.
final class PlayMediaIntentHandler: NSObject, INPlayMediaIntentHandling {
    /// What to play, carried from resolve to handle in the media item's identifier.
    private struct Request: Codable {
        var serverId: UUID
        /// Nil means carry on with what was playing ("Play Home Speaker").
        var query: String?
        var kind: String?
        var artist: String?
    }

    func resolveMediaItems(for intent: INPlayMediaIntent) async -> [INPlayMediaMediaItemResolutionResult] {
        let heard = describe(intent)
        let connections = storedConnections()
        guard var connection = SharedConnections.selected() ?? connections.first else {
            SiriRequestLog.add(heard: heard, outcome: "No servers saved")
            return [.unsupported(forReason: .serviceUnavailable)]
        }

        let search = intent.mediaSearch
        // Siri usually puts the name in mediaName, but albums, genres, moods and names it
        // already resolved land elsewhere.
        var text = [
            search?.mediaName, search?.albumName, search?.genreNames?.first, search?.moodNames?.first,
            intent.mediaContainer?.title, intent.mediaItems?.first?.title,
        ].compactMap { $0 }.first { !$0.trimmingCharacters(in: .whitespaces).isEmpty }
        if let spoken = text, let split = splitServer(spoken, from: connections) {
            text = split.rest
            connection = split.connection
        }
        let artist = search?.artistName

        if (text ?? "").isEmpty && (artist ?? "").isEmpty {
            SiriRequestLog.add(heard: heard, outcome: "No name heard, so resuming \(connection.name)")
            let resume = Request(serverId: connection.id)
            return [.success(with: mediaItem(resume, title: connection.name, type: .music))]
        }

        do {
            let api = APIClient.client(for: connection)
            let kind = kindName(search?.mediaType ?? .unknown)
            guard let match = try await api.findMedia(query: text ?? "", kind: kind, artist: artist) else {
                SiriRequestLog.add(heard: heard, outcome: "\(connection.name) has nothing matching \"\(text ?? artist ?? "")\"")
                return [.unsupported()]
            }
            SiriRequestLog.add(heard: heard, outcome: "Found \(match.kind) \"\(match.name)\" on \(connection.name)")
            // Replaying the match's own name and kind finds it again exactly.
            let request = Request(serverId: connection.id, query: match.name, kind: match.kind, artist: match.artist)
            return [.success(with: mediaItem(request, title: match.name, type: itemType(match.kind)))]
        } catch {
            SiriRequestLog.add(heard: heard, outcome: "Couldn't reach \(connection.name): \(error.localizedDescription)")
            return [.unsupported(forReason: .serviceUnavailable)]
        }
    }

    func handle(intent: INPlayMediaIntent) async -> INPlayMediaIntentResponse {
        guard let identifier = intent.mediaItems?.first?.identifier,
              let request = try? JSONDecoder().decode(Request.self, from: Data(identifier.utf8)),
              let connection = SharedConnections.connection(id: request.serverId) else {
            SiriRequestLog.add(heard: describe(intent), outcome: "Play step got no item it could use")
            return INPlayMediaIntentResponse(code: .failure, userActivity: nil)
        }

        let api = APIClient.client(for: connection)
        do {
            if let query = request.query {
                guard let played = try await api.playMedia(query: query, kind: request.kind, artist: request.artist) else {
                    SiriRequestLog.add(heard: query, outcome: "\(connection.name) couldn't play it")
                    return INPlayMediaIntentResponse(code: .failureUnknownMediaType, userActivity: nil)
                }
                SiriRequestLog.add(heard: query, outcome: "Playing \(played.kind) \"\(played.name)\" on \(connection.name)")
            } else {
                try await api.resume()
                SiriRequestLog.add(heard: "(no name)", outcome: "Resumed \(connection.name)")
            }
            await SpeakerSurfaces.refresh(after: connection)
            return INPlayMediaIntentResponse(code: .success, userActivity: nil)
        } catch {
            SiriRequestLog.add(heard: request.query ?? "(no name)", outcome: "Play failed on \(connection.name): \(error.localizedDescription)")
            return INPlayMediaIntentResponse(code: .failure, userActivity: nil)
        }
    }

    /// Everything Siri passed, for the request log.
    private func describe(_ intent: INPlayMediaIntent) -> String {
        let search = intent.mediaSearch
        let parts: [(String, String?)] = [
            ("name", search?.mediaName),
            ("album", search?.albumName),
            ("artist", search?.artistName),
            ("genre", search?.genreNames?.joined(separator: ", ")),
            ("mood", search?.moodNames?.joined(separator: ", ")),
            ("type", search.map { String(describing: $0.mediaType.rawValue) }),
            ("container", intent.mediaContainer?.title),
            ("items", intent.mediaItems?.compactMap(\.title).joined(separator: ", ")),
        ]
        let described = parts.compactMap { label, value in
            value.flatMap { $0.isEmpty ? nil : "\(label): \($0)" }
        }
        return described.isEmpty ? "(nothing)" : described.joined(separator: "; ")
    }

    private func mediaItem(_ request: Request, title: String, type: INMediaItemType) -> INMediaItem {
        let identifier = (try? JSONEncoder().encode(request)).map { String(decoding: $0, as: UTF8.self) }
        return INMediaItem(identifier: identifier, title: title, type: type, artwork: nil)
    }

    /// "Hymns in the kitchen" or "Hymns upstairs" names a server; the rest is what to play.
    private func splitServer(_ text: String, from connections: [ServerConnection]) -> (rest: String, connection: ServerConnection)? {
        let lower = text.lowercased()
        for connection in connections {
            let name = connection.name.lowercased()
            for joiner in [" on the ", " in the ", " on ", " in ", " "] where lower.hasSuffix(joiner + name) {
                let rest = text.dropLast(joiner.count + name.count).trimmingCharacters(in: .whitespaces)
                return rest.isEmpty ? nil : (rest, connection)
            }
        }
        return nil
    }

    /// Siri's guess at what kind of thing was asked for, as the server's MediaKind name.
    private func kindName(_ type: INMediaItemType) -> String? {
        switch type {
        case .song: return "Song"
        case .album: return "Album"
        case .artist: return "Artist"
        case .playlist: return "Playlist"
        case .radioStation, .station, .musicStation: return "Stream"
        default: return nil
        }
    }

    private func itemType(_ kind: String) -> INMediaItemType {
        switch kind {
        case "Song": return .song
        case "Album": return .album
        case "Artist": return .artist
        case "Stream": return .radioStation
        default: return .playlist
        }
    }
}
