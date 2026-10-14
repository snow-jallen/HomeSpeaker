import Foundation
import Observation

/// Live player status for the selected speaker. Follows the server's status
/// stream, falling back to polling every 2 s for servers without one, and
/// keeps widgets and the Live Activity current while the app is open.
@MainActor
@Observable
final class SpeakerStatusStore {
    private(set) var status: PlayerStatus?
    /// When `status` arrived, so elapsed time can tick locally between updates.
    private(set) var receivedAt: Date = .now
    private(set) var connection: ServerConnection?

    @ObservationIgnored private var task: Task<Void, Never>?
    @ObservationIgnored private var lastSurfaceKey: String?

    /// Starts following a speaker, or stops when `connection` is nil.
    func follow(_ connection: ServerConnection?) {
        guard connection != self.connection || task == nil else { return }
        task?.cancel()
        task = nil
        if connection?.id != self.connection?.id {
            status = nil
        }
        self.connection = connection
        guard let connection else { return }
        task = Task { [weak self] in
            await self?.run(connection)
        }
    }

    func pause() {
        task?.cancel()
        task = nil
    }

    /// Fetches status right away, e.g. after the user pressed a button.
    func refreshNow() async {
        guard let connection else { return }
        if let fresh = try? await APIClient.client(for: connection).getPlayerStatus() {
            apply(fresh, from: connection)
        }
    }

    /// Elapsed seconds now, counting on from the last update while playing.
    func elapsedSeconds(at date: Date) -> Double {
        guard let status else { return 0 }
        let base = status.elapsedSeconds
        guard status.isPlaying else { return base }
        let total = base + status.remainingSeconds
        let ticking = base + date.timeIntervalSince(receivedAt)
        return total > 0 ? min(ticking, total) : ticking
    }

    // MARK: - Private

    private func run(_ connection: ServerConnection) async {
        let api = APIClient.client(for: connection)
        while !Task.isCancelled {
            do {
                for try await update in api.statusUpdates() {
                    apply(update, from: connection)
                }
            } catch {
                // No stream (older server) or it dropped: poll once, then retry.
            }
            if Task.isCancelled { break }
            if let polled = try? await api.getPlayerStatus() {
                apply(polled, from: connection)
            }
            try? await Task.sleep(for: .seconds(2))
        }
    }

    private func apply(_ newStatus: PlayerStatus, from source: ServerConnection) {
        guard source.id == connection?.id else { return }
        status = newStatus
        receivedAt = .now

        // Widgets and the Live Activity only care about what's on screen there,
        // not elapsed time, so skip reloading them for heartbeats.
        let key = [
            newStatus.displayTitle,
            newStatus.displaySubtitle,
            String(newStatus.isPlaying),
            String(newStatus.volume),
            String(newStatus.sleepTimerActive ?? false),
            String(newStatus.repeatMode ?? false),
        ].joined(separator: "|")
        guard key != lastSurfaceKey else { return }
        let isFirst = lastSurfaceKey == nil
        lastSurfaceKey = key
        Task {
            await SpeakerActivityController.shared.update(for: source, status: newStatus)
        }
        if !isFirst {
            SpeakerSurfaces.reloadWidgets()
        }
    }
}
