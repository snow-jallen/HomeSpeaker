import SwiftUI

struct NowPlayingView: View {
    @Environment(ConnectionStore.self) private var store
    @State private var status: PlayerStatus?
    @State private var error: String?
    @State private var volume: Double = 50
    @State private var isDraggingVolume = false
    @State private var showSleepTimer = false
    @State private var showServerList = false

    var body: some View {
        NavigationStack {
            Group {
                if let api = store.api {
                    playerContent(api: api)
                } else {
                    noServerView
                }
            }
            .navigationTitle(store.selectedConnection?.name ?? "HomeSpeaker")
            .navigationBarTitleDisplayMode(.inline)
            .toolbar {
                ToolbarItem(placement: .topBarLeading) {
                    serverSelectorButton
                }
                ToolbarItem(placement: .topBarTrailing) {
                    if let active = status, active.sleepTimerActive == true {
                        Button {
                            showSleepTimer = true
                        } label: {
                            Label("Sleep Timer", systemImage: "moon.fill")
                                .foregroundStyle(.orange)
                        }
                    } else {
                        Button { showSleepTimer = true } label: {
                            Image(systemName: "moon")
                        }
                    }
                }
            }
            .sheet(isPresented: $showSleepTimer) {
                if let api = store.api {
                    SleepTimerSheet(api: api, status: status) {
                        showSleepTimer = false
                        Task { await refresh(api: api) }
                    }
                }
            }
            .sheet(isPresented: $showServerList) {
                ServerPickerSheet()
            }
        }
    }

    private var serverSelectorButton: some View {
        Button {
            showServerList = true
        } label: {
            HStack(spacing: 4) {
                Image(systemName: "hifispeaker.2")
                    .imageScale(.small)
                Text(store.selectedConnection?.name ?? "Select")
                    .font(.caption)
            }
            .foregroundStyle(Color.accentColor)
        }
    }

    private var noServerView: some View {
        ContentUnavailableView {
            Label("No Server Selected", systemImage: "wifi.slash")
        } description: {
            Text("Tap the server button above to add or select a server.")
        }
    }

    private func playerContent(api: APIClient) -> some View {
        ScrollView {
            VStack(spacing: 24) {
                artworkPlaceholder

                songInfo

                if let status {
                    progressSection(status: status)
                    transportControls(api: api, status: status)
                } else {
                    transportControls(api: api, status: nil)
                }

                volumeSection(api: api)

                if let status, let remaining = status.sleepTimerRemainingMinutes, status.sleepTimerActive == true {
                    sleepTimerBadge(remainingMinutes: remaining, api: api)
                }
            }
            .padding()
        }
        .task {
            volume = Double(status?.volume ?? 50)
            while !Task.isCancelled {
                await refresh(api: api)
                try? await Task.sleep(for: .seconds(2))
            }
        }
    }

    private var artworkPlaceholder: some View {
        RoundedRectangle(cornerRadius: 20)
            .fill(Color(.systemGray5))
            .frame(width: 240, height: 240)
            .overlay {
                Image(systemName: "music.note")
                    .font(.system(size: 80))
                    .foregroundStyle(.secondary)
            }
            .shadow(radius: 8)
    }

    private var songInfo: some View {
        VStack(spacing: 6) {
            if let song = status?.currentSong {
                Text(song.displayTitle)
                    .font(.title2.bold())
                    .multilineTextAlignment(.center)
                Text(song.displayArtist)
                    .font(.body)
                    .foregroundStyle(.secondary)
                Text(song.displayAlbum)
                    .font(.caption)
                    .foregroundStyle(.tertiary)
            } else if status?.stillPlaying == true {
                Text("Streaming")
                    .font(.title2.bold())
            } else {
                Text("Not Playing")
                    .font(.title2)
                    .foregroundStyle(.secondary)
            }
        }
    }

    private func progressSection(status: PlayerStatus) -> some View {
        VStack(spacing: 4) {
            ProgressView(value: status.percentComplete)
                .progressViewStyle(.linear)
                .tint(Color.accentColor)
            HStack {
                Text(status.elapsedFormatted)
                    .font(.caption.monospacedDigit())
                    .foregroundStyle(.secondary)
                Spacer()
                Text("-\(status.remainingFormatted)")
                    .font(.caption.monospacedDigit())
                    .foregroundStyle(.secondary)
            }
        }
    }

    private func transportControls(api: APIClient, status: PlayerStatus?) -> some View {
        HStack(spacing: 40) {
            Button {
                Task { try? await api.stop(); await refresh(api: api) }
            } label: {
                Image(systemName: "stop.fill")
                    .font(.title)
            }
            .foregroundStyle(.primary)

            Button {
                Task {
                    if status?.isPlaying == true {
                        try? await api.stop()
                    } else {
                        try? await api.resume()
                    }
                    await refresh(api: api)
                }
            } label: {
                Image(systemName: status?.isPlaying == true ? "pause.circle.fill" : "play.circle.fill")
                    .font(.system(size: 64))
            }
            .foregroundStyle(Color.accentColor)

            Button {
                Task { try? await api.skipToNext(); await refresh(api: api) }
            } label: {
                Image(systemName: "forward.fill")
                    .font(.title)
            }
            .foregroundStyle(.primary)
        }
    }

    private func volumeSection(api: APIClient) -> some View {
        HStack(spacing: 12) {
            Image(systemName: "speaker.fill")
                .foregroundStyle(.secondary)
            Slider(value: $volume, in: 0...100, step: 1) { editing in
                isDraggingVolume = editing
                if !editing {
                    Task { try? await api.setVolume(Int(volume)) }
                }
            }
            Image(systemName: "speaker.wave.3.fill")
                .foregroundStyle(.secondary)
        }
    }

    private func sleepTimerBadge(remainingMinutes: Double, api: APIClient) -> some View {
        HStack {
            Image(systemName: "moon.fill")
                .foregroundStyle(.orange)
            Text("Sleep in \(Int(remainingMinutes.rounded(.up))) min")
                .font(.caption)
            Spacer()
            Button("Cancel") {
                Task { try? await api.cancelSleepTimer(); await refresh(api: api) }
            }
            .font(.caption)
        }
        .padding(.horizontal, 16)
        .padding(.vertical, 8)
        .background(.orange.opacity(0.15), in: RoundedRectangle(cornerRadius: 10))
    }

    @MainActor
    private func refresh(api: APIClient) async {
        do {
            status = try await api.getPlayerStatus()
            if !isDraggingVolume {
                volume = Double(status?.volume ?? Int(volume))
            }
        } catch {
            // Silently ignore polling errors
        }
    }
}

struct SleepTimerSheet: View {
    let api: APIClient
    let status: PlayerStatus?
    let onDismiss: () -> Void

    @State private var selectedMinutes = 30
    let options = [15, 30, 45, 60, 90, 120]

    var body: some View {
        NavigationStack {
            Form {
                if status?.sleepTimerActive == true, let remaining = status?.sleepTimerRemainingMinutes {
                    Section("Active Timer") {
                        HStack {
                            Label("Stops in", systemImage: "moon.fill")
                                .foregroundStyle(.orange)
                            Spacer()
                            Text("\(Int(remaining.rounded(.up))) min")
                                .foregroundStyle(.secondary)
                        }
                        Button("Cancel Timer", role: .destructive) {
                            Task { try? await api.cancelSleepTimer(); onDismiss() }
                        }
                    }
                }
                Section("Set Timer") {
                    Picker("Duration", selection: $selectedMinutes) {
                        ForEach(options, id: \.self) { min in
                            Text("\(min) minutes").tag(min)
                        }
                    }
                    .pickerStyle(.wheel)
                }
                Section {
                    Button("Start Timer") {
                        Task { try? await api.setSleepTimer(minutes: selectedMinutes); onDismiss() }
                    }
                }
            }
            .navigationTitle("Sleep Timer")
            .navigationBarTitleDisplayMode(.inline)
            .toolbar {
                ToolbarItem(placement: .cancellationAction) {
                    Button("Cancel") { onDismiss() }
                }
            }
        }
    }
}
