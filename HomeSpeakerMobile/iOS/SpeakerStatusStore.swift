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
    @ObservationIgnored private var lastWidgetKey: String?
    @ObservationIgnored private var lastActivityKey: String?

    /// Starts following a speaker, or stops when `connection` is nil.
    func follow(_ connection: ServerConnection?) {
        guard connection != self.connection || task == nil else { return }
        task?.cancel()
        task = nil
        if connection?.id != self.connection?.id {
            status = nil
            lastWidgetKey = nil
            lastActivityKey = nil
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
        var hadStream = false
        while !Task.isCancelled {
            do {
                var isFirstEvent = true
                for try await update in api.statusUpdates() {
                    // A stream coming back usually means the server restarted
                    // and forgot its Live Activity registrations.
                    if isFirstEvent && hadStream {
                        SpeakerActivityController.shared.reattach()
                    }
                    isFirstEvent = false
                    hadStream = true
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

        // Widgets and the Live Activity only care about what they show, not
        // elapsed time, so skip updating them for heartbeats. Widget reloads
        // are rationed by iOS, so they only follow the song and play state.
        let widgetKey = [
            newStatus.displayTitle,
            newStatus.displaySubtitle,
            String(newStatus.isPlaying),
        ].joined(separator: "|")
        let sleepEnds = newStatus.sleepTimerActive == true
            ? newStatus.sleepTimerRemainingMinutes.map { Int((Date.now.timeIntervalSince1970 + $0 * 60) / 30) }
            : nil
        let activityKey = [
            widgetKey,
            String(newStatus.volume),
            String(newStatus.repeatMode ?? false),
            sleepEnds.map(String.init) ?? "-",
        ].joined(separator: "|")

        if activityKey != lastActivityKey {
            lastActivityKey = activityKey
            Task {
                await SpeakerActivityController.shared.update(for: source, status: newStatus)
            }
        }
        if widgetKey != lastWidgetKey {
            let isFirst = lastWidgetKey == nil
            lastWidgetKey = widgetKey
            if !isFirst {
                SpeakerSurfaces.reloadWidgets()
            }
        }
    }
}
