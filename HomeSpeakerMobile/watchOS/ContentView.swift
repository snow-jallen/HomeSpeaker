import SwiftUI

struct WatchContentView: View {
    @Environment(ConnectionStore.self) private var store
    @State private var crownVolume: Double = 50
    @State private var showVolumeHUD = false
    @State private var volumeTask: Task<Void, Never>?

    var body: some View {
        ZStack(alignment: .top) {
            if store.connections.isEmpty {
                WatchServerPickerView()
            } else {
                TabView {
                    WatchNowPlayingView()
                    WatchLibraryView()
                    WatchPlaylistsView()
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
            guard let api = store.api,
                  let status = try? await api.getPlayerStatus() else { return }
            crownVolume = Double(status.volume)
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
            try? await Task.sleep(for: .seconds(1.5))
            guard !Task.isCancelled else { return }
            withAnimation { showVolumeHUD = false }
        }
    }
}
