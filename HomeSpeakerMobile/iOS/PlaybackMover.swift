import Foundation

/// Moves what one speaker is playing to another. Each Pi has its own library,
/// so songs are matched on the target by path, then by name/artist/album.
/// Songs restart from the beginning: the server can't seek.
enum PlaybackMover {
    struct Result {
        let moved: Int
        let total: Int
        let description: String
    }

    enum MoveError: LocalizedError {
        case nothingPlaying(String)
        case notInLibrary(String)
        case stationNotFound(String)

        var errorDescription: String? {
            switch self {
            case .nothingPlaying(let room): return "Nothing is playing in \(room)."
            case .notInLibrary(let room): return "None of those songs are in \(room)'s library."
            case .stationNotFound(let room): return "\(room) doesn't have that radio station."
            }
        }
    }

    static func move(from source: ServerConnection, to target: ServerConnection) async throws -> Result {
        let sourceAPI = APIClient.client(for: source)
        let targetAPI = APIClient.client(for: target)
        let status = try await sourceAPI.getPlayerStatus()

        if let current = status.currentSong {
            let queue = (try? await sourceAPI.getQueue()) ?? []
            let wanted = [current] + queue
            let library = try await targetAPI.getSongs()
            let matched = match(wanted, in: library)
            guard let first = matched.first else { throw MoveError.notInLibrary(target.name) }

            try await targetAPI.playSong(first.songId)
            let rest = matched.dropFirst().compactMap(\.path)
            if !rest.isEmpty {
                try await targetAPI.updateQueue(songPaths: rest)
            }
            try await sourceAPI.stop()
            try? await sourceAPI.clearQueue()
            let missing = wanted.count - matched.count
            let note = missing > 0 ? " (\(missing) not in \(target.name)'s library)" : ""
            return Result(moved: matched.count, total: wanted.count,
                          description: "Moved \(matched.count) song\(matched.count == 1 ? "" : "s") to \(target.name)\(note).")
        }

        if status.isPlaying, let station = status.streamName, !station.isEmpty {
            let streams = try await targetAPI.getRadioStreams()
            guard let match = bestMatch(from: streams, query: station, key: { $0.name }) else {
                throw MoveError.stationNotFound(target.name)
            }
            try await targetAPI.playRadioStream(id: match.id)
            try await sourceAPI.stop()
            return Result(moved: 1, total: 1, description: "Moved \(match.name) to \(target.name).")
        }

        throw MoveError.nothingPlaying(source.name)
    }

    /// The target's copies of `songs`, in order, skipping ones it doesn't have.
    static func match(_ songs: [Song], in library: [Song]) -> [Song] {
        var byPath: [String: Song] = [:]
        var byTags: [String: Song] = [:]
        for song in library {
            if let path = song.path { byPath[path] = song }
            byTags[tagKey(song)] = byTags[tagKey(song)] ?? song
        }
        return songs.compactMap { song in
            song.path.flatMap { byPath[$0] } ?? byTags[tagKey(song)]
        }
    }

    private static func tagKey(_ song: Song) -> String {
        [song.name, song.artist ?? "", song.album ?? ""]
            .map { $0.lowercased().trimmingCharacters(in: .whitespaces) }
            .joined(separator: "\u{1F}")
    }
}
