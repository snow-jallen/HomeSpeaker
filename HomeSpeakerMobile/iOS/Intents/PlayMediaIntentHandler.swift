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
        /// The Siri Command History entry the search step recorded, finished by the play step.
        var logId: UUID?
    }

    func resolveMediaItems(for intent: INPlayMediaIntent) async -> [INPlayMediaMediaItemResolutionResult] {
        let captured = describe(intent)
        let connections = storedConnections()
        guard var connection = SharedConnections.selected() ?? connections.first else {
            SiriCommandLog.add(command: "Play", captured: captured, translated: "No server", result: "No servers are saved in the app")
            return [.unsupported(forReason: .serviceUnavailable)]
        }

        let search = intent.mediaSearch
        // Siri usually puts the name in mediaName, but albums, moods and names it already
        // resolved land elsewhere, and some names come as genres: "classical kids" arrives as
        // the genres "classical" and "kidsfamily", so every genre is sent together.
        let genresAndMoods = ((search?.genreNames ?? []) + (search?.moodNames ?? [])).joined(separator: " ")
        var text = [
            search?.mediaName, search?.albumName, genresAndMoods,
            intent.mediaContainer?.title, intent.mediaItems?.first?.title,
        ].compactMap { $0 }.first { !$0.trimmingCharacters(in: .whitespaces).isEmpty }
        if let spoken = text, let split = splitServer(spoken, from: connections) {
            text = split.rest
            connection = split.connection
        }
        let artist = search?.artistName

        if (text ?? "").isEmpty && (artist ?? "").isEmpty {
            let logId = SiriCommandLog.add(command: "Play", captured: captured,
                                           translated: "No name, so resume \(connection.name)",
                                           result: "Waiting for Siri's play step")
            let resume = Request(serverId: connection.id, logId: logId)
            return [.success(with: mediaItem(resume, title: connection.name, type: .music))]
        }

        let kind = kindName(search?.mediaType ?? .unknown)
        let translated = searchDescription(text: text ?? "", kind: kind, artist: artist, on: connection)
        do {
            let api = APIClient.client(for: connection)
            guard let match = try await api.findMedia(query: text ?? "", kind: kind, artist: artist) else {
                SiriCommandLog.add(command: "Play", captured: captured, translated: translated,
                                   result: "Nothing on \(connection.name) matched")
                return [.unsupported()]
            }
            let logId = SiriCommandLog.add(command: "Play", captured: captured, translated: translated,
                                           result: "Found \(describe(match)); waiting for Siri's play step")
            // Replaying the match's own name and kind finds it again exactly.
            let request = Request(serverId: connection.id, query: match.name, kind: match.kind,
                                  artist: match.artist, logId: logId)
            return [.success(with: mediaItem(request, title: match.name, type: itemType(match.kind)))]
        } catch {
            SiriCommandLog.add(command: "Play", captured: captured, translated: translated,
                               result: "Couldn't reach \(connection.name): \(error.localizedDescription)")
            return [.unsupported(forReason: .serviceUnavailable)]
        }
    }

    func handle(intent: INPlayMediaIntent) async -> INPlayMediaIntentResponse {
        guard let identifier = intent.mediaItems?.first?.identifier,
              let request = try? JSONDecoder().decode(Request.self, from: Data(identifier.utf8)),
              let connection = SharedConnections.connection(id: request.serverId) else {
            SiriCommandLog.add(command: "Play", captured: describe(intent), translated: "Nothing to play",
                               result: "Siri's play step came without an item from the search step")
            return INPlayMediaIntentResponse(code: .failure, userActivity: nil)
        }

        let api = APIClient.client(for: connection)
        let result: String
        var code = INPlayMediaIntentResponseCode.success
        do {
            if let query = request.query {
                if let played = try await api.playMedia(query: query, kind: request.kind, artist: request.artist) {
                    result = "\(connection.name) is playing \(describe(played))"
                } else {
                    result = "\(connection.name) found nothing to play for \"\(query)\""
                    code = .failureUnknownMediaType
                }
            } else {
                try await api.resume()
                result = "\(connection.name) resumed"
            }
        } catch {
            result = "Play failed on \(connection.name): \(error.localizedDescription)"
            code = .failure
        }

        if let logId = request.logId {
            SiriCommandLog.update(logId, result: result)
        } else {
            SiriCommandLog.add(command: "Play", captured: describe(intent), translated: request.query ?? "Resume", result: result)
        }
        if code == .success {
            await SpeakerSurfaces.refresh(after: connection)
        }
        return INPlayMediaIntentResponse(code: code, userActivity: nil)
    }

    private func mediaItem(_ request: Request, title: String, type: INMediaItemType) -> INMediaItem {
        let identifier = (try? JSONEncoder().encode(request)).map { String(decoding: $0, as: UTF8.self) }
        return INMediaItem(identifier: identifier, title: title, type: type, artwork: nil)
    }

    private func searchDescription(text: String, kind: String?, artist: String?, on connection: ServerConnection) -> String {
        var parts = ["Search \(connection.name) for \"\(text)\""]
        if let kind { parts.append("as a \(kind.lowercased())") }
        if let artist, !artist.isEmpty { parts.append("by \(artist)") }
        return parts.joined(separator: " ")
    }

    private func describe(_ match: MediaMatch) -> String {
        let base = "\(match.kind.lowercased()) \"\(match.name)\""
        return match.artist.map { "\(base) by \($0)" } ?? base
    }

    /// Everything Siri passed, for the history.
    private func describe(_ intent: INPlayMediaIntent) -> String {
        let search = intent.mediaSearch
        let parts: [(String, String?)] = [
            ("name", search?.mediaName),
            ("album", search?.albumName),
            ("artist", search?.artistName),
            ("genre", search?.genreNames?.joined(separator: ", ")),
            ("mood", search?.moodNames?.joined(separator: ", ")),
            ("type", search.map { typeName($0.mediaType) }),
            ("container", intent.mediaContainer?.title),
            ("items", intent.mediaItems?.compactMap(\.title).joined(separator: ", ")),
        ]
        let described = parts.compactMap { label, value in
            value.flatMap { $0.isEmpty ? nil : "\(label): \($0)" }
        }
        return described.isEmpty ? "(nothing)" : described.joined(separator: "; ")
    }

    private func typeName(_ type: INMediaItemType) -> String {
        switch type {
        case .unknown: return "unspecified"
        case .song: return "song"
        case .album: return "album"
        case .artist: return "artist"
        case .genre: return "genre"
        case .playlist: return "playlist"
        case .music: return "music"
        case .radioStation, .station, .musicStation: return "station"
        default: return "other (\(type.rawValue))"
        }
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
