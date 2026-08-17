import SwiftUI

struct WatchContentView: View {
    @Environment(ConnectionStore.self) private var store
    @State private var crownVolume: Double = 50
    @State private var showVolumeHUD = false
    @State private var volumeTask: Task<Void, Never>?
    @State private var syncingFromServer = false
    @State private var lastCrownActivity: Date = .distantPast

    var body: some View {
        ZStack(alignment: .top) {
            if store.connections.isEmpty {
                WatchServerPickerView()
            } else {
                TabView {
                    WatchNowPlayingView()
                    WatchLibraryView()
                    WatchPlaylistsView()
                    WatchAIPlaylistsView()
                    WatchRadioStreamsView()
                    WatchQueueView()
                    WatchServerPickerView()
                }
                .tabViewStyle(.page)
                .focusable()
                .digitalCrownRotation(
                    $crownVolume,
                    from: 0, through: 100, by: 2,
                    sensitivity: .medium,
                    isContinuous: false
                )
                .onChange(of: crownVolume) { _, newValue in
                    // Distinguish real crown input from the server-sync loop below
                    // writing into the same binding - only the former should send
                    // a set-volume request back to the server.
                    if syncingFromServer {
                        syncingFromServer = false
                        return
                    }
                    lastCrownActivity = Date()
                    scheduleVolumeUpdate(Int(newValue))
                }
            }

            if showVolumeHUD {
                volumeHUD
                    .transition(.opacity)
                    .animation(.easeInOut(duration: 0.15), value: showVolumeHUD)
            }
        }
        .task {
            // Keep the crown's notion of volume in sync with the server so a level
            // changed from another client (or the physical knob) isn't yanked back
            // to a stale value by the next crown detent. Skip syncing while the
            // user is actively using the crown or a set request is settling.
            while !Task.isCancelled {
                if let api = store.api,
                   Date().timeIntervalSince(lastCrownActivity) > 3,
                   let status = try? await api.getPlayerStatus(),
                   Date().timeIntervalSince(lastCrownActivity) > 3,
                   Int(crownVolume) != status.volume {
                    syncingFromServer = true
                    crownVolume = Double(status.volume)
                }
                try? await Task.sleep(for: .seconds(3))
            }
        }
    }

    private var volumeHUD: some View {
        HStack(spacing: 4) {
            Image(systemName: volumeIcon)
                .font(.caption2)
            Text("\(Int(crownVolume))")
                .font(.caption2.monospacedDigit())
                .frame(minWidth: 22, alignment: .trailing)
        }
        .padding(.horizontal, 8)
        .padding(.vertical, 3)
        .background(.ultraThinMaterial, in: Capsule())
        .padding(.top, 2)
    }

    private var volumeIcon: String {
        if crownVolume == 0 { return "speaker.slash.fill" }
        if crownVolume < 40 { return "speaker.wave.1.fill" }
        if crownVolume < 75 { return "speaker.wave.2.fill" }
        return "speaker.wave.3.fill"
    }

    private func scheduleVolumeUpdate(_ volume: Int) {
        showVolumeHUD = true
        volumeTask?.cancel()
        volumeTask = Task { @MainActor in
            try? await Task.sleep(for: .milliseconds(350))
            guard !Task.isCancelled else { return }
            try? await store.api?.setVolume(volume)
            lastCrownActivity = Date()
            try? await Task.sleep(for: .seconds(1.5))
            guard !Task.isCancelled else { return }
            withAnimation { showVolumeHUD = false }
        }
    }
}
