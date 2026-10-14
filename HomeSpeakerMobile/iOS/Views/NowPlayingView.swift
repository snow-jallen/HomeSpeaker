import AVKit
import SwiftUI

struct NowPlayingView: View {
    @Environment(ConnectionStore.self) private var store
    @Environment(LocalPlayer.self) private var localPlayer
    @Environment(SpeakerStatusStore.self) private var speaker
    @State private var activities = SpeakerActivityController.shared
    @State private var features: Features?
    @State private var volume: Double = 50
    @State private var isDraggingVolume = false
    @State private var showSleepTimer = false
    @State private var showRooms = false
    @State private var message: String?

    private var status: PlayerStatus? { speaker.status }

    var body: some View {
        NavigationStack {
            Group {
                if localPlayer.destination == .device {
                    LocalNowPlayingView()
                } else if let api = store.api {
                    playerContent(api: api)
                } else {
                    noServerView
                }
            }
            .navigationTitle(localPlayer.destination == .device ? "This iPhone" : (store.selectedConnection?.name ?? "HomeSpeaker"))
            .navigationBarTitleDisplayMode(.inline)
            .toolbar {
                ToolbarItem(placement: .topBarLeading) {
                    roomsButton
                }
                ToolbarItemGroup(placement: .topBarTrailing) {
                    destinationMenu
                    if localPlayer.destination == .speaker, store.api != nil {
                        sleepTimerButton
                        moreMenu
                    }
                }
            }
            .sheet(isPresented: $showSleepTimer) {
                if let api = store.api {
                    SleepTimerSheet(api: api, status: status) {
                        showSleepTimer = false
                        Task { await speaker.refreshNow() }
                    }
                }
            }
            .sheet(isPresented: $showRooms) {
                RoomsView()
            }
            .task(id: store.selectedConnection?.id) {
                features = try? await store.api?.getFeatures()
            }
            .onChange(of: status?.volume) {
                if !isDraggingVolume, let level = status?.volume {
                    volume = Double(level)
                }
            }
        }
    }

    // MARK: - Toolbar

    private var roomsButton: some View {
        Button {
            showRooms = true
        } label: {
            HStack(spacing: 4) {
                Image(systemName: "hifispeaker.2")
                    .imageScale(.small)
                Text(store.connections.count > 1 ? "Rooms" : (store.selectedConnection?.name ?? "Select"))
                    .font(.caption)
            }
            .foregroundStyle(Color.accentColor)
        }
    }

    private var destinationMenu: some View {
        Menu {
            Button {
                localPlayer.destination = .speaker
            } label: {
                Label("Speaker", systemImage: "hifispeaker.fill")
            }
            Button {
                localPlayer.destination = .device
            } label: {
                Label("This iPhone", systemImage: "iphone")
            }
        } label: {
            Image(systemName: localPlayer.destination == .speaker ? "hifispeaker.fill" : "iphone")
        }
    }

    private var sleepTimerButton: some View {
        Button {
            showSleepTimer = true
        } label: {
            if status?.sleepTimerActive == true {
                Label("Sleep Timer", systemImage: "moon.fill")
                    .foregroundStyle(.orange)
            } else {
                Label("Sleep Timer", systemImage: "moon")
            }
        }
    }

    private var moreMenu: some View {
        Menu {
            if let connection = store.selectedConnection {
                if activities.isActive(for: connection) {
                    Button {
                        Task { await activities.end(for: connection) }
                    } label: {
                        Label("Remove from Lock Screen", systemImage: "rectangle.badge.xmark")
                    }
                } else {
                    Button {
                        Task { await startLiveActivity(connection) }
                    } label: {
                        Label("Show on Lock Screen", systemImage: "platter.filled.bottom.iphone")
                    }
                }
            }
        } label: {
            Label("More", systemImage: "ellipsis.circle")
        }
    }

    private var noServerView: some View {
        ContentUnavailableView {
            Label("No Server Selected", systemImage: "wifi.slash")
        } description: {
            Text("Tap Rooms above to add or select a server.")
        }
    }

    // MARK: - Speaker

    private func playerContent(api: APIClient) -> some View {
        VStack(spacing: 0) {
            ScrollView {
                VStack(spacing: 24) {
                    artwork(api: api)

                    songInfo

                    if status?.librarySong != nil {
                        TimelineView(.periodic(from: .now, by: 1)) { context in
                            progressSection(at: context.date)
                        }
                    }

                    transportControls(api: api)

                    extrasRow(api: api)

                    if let status, let remaining = status.sleepTimerRemainingMinutes, status.sleepTimerActive == true {
                        sleepTimerBadge(remainingMinutes: remaining, api: api)
                    }

                    if let note = message ?? activities.lastError {
                        Text(note)
                            .font(.caption)
                            .foregroundStyle(.secondary)
                            .multilineTextAlignment(.center)
                    }
                }
                .padding()
            }

            // Pinned below the scroll view so the volume slider is always visible.
            Divider()
            volumeSection(api: api)
                .padding(.horizontal)
                .padding(.vertical, 12)
        }
    }

    @ViewBuilder
    private func artwork(api: APIClient) -> some View {
        let placeholderIcon = status?.isStream == true || (status?.currentSong == nil && status?.isPlaying == true)
            ? "dot.radiowaves.left.and.right"
            : "music.note"
        Group {
            if let song = status?.librarySong {
                AsyncImage(url: api.albumArtURL(songId: song.songId)) { phase in
                    if let image = phase.image {
                        image.resizable().scaledToFill()
                    } else {
                        artworkPlaceholder(icon: placeholderIcon)
                    }
                }
                .id(song.songId)
            } else {
                artworkPlaceholder(icon: placeholderIcon)
            }
        }
        .frame(width: 160, height: 160)
        .clipShape(RoundedRectangle(cornerRadius: 16))
        .shadow(radius: 8)
    }

    private func artworkPlaceholder(icon: String) -> some View {
        Rectangle()
            .fill(Color(.systemGray5))
            .overlay {
                Image(systemName: icon)
                    .font(.system(size: 56))
                    .foregroundStyle(.secondary)
            }
    }

    private var songInfo: some View {
        VStack(spacing: 6) {
            if let song = status?.librarySong {
                Text(song.displayTitle)
                    .font(.title2.bold())
                    .multilineTextAlignment(.center)
                Text(song.displayArtist)
                    .font(.body)
                    .foregroundStyle(.secondary)
                Text(song.displayAlbum)
                    .font(.caption)
                    .foregroundStyle(.tertiary)
            } else if let status, status.isPlaying {
                Text(status.displayTitle)
                    .font(.title2.bold())
                    .multilineTextAlignment(.center)
                if !status.displaySubtitle.isEmpty {
                    Text(status.displaySubtitle)
                        .font(.body)
                        .foregroundStyle(.secondary)
                }
            } else {
                Text("Not Playing")
                    .font(.title2)
                    .foregroundStyle(.secondary)
            }
        }
    }

    private func progressSection(at date: Date) -> some View {
        let elapsed = speaker.elapsedSeconds(at: date)
        let total = (status?.elapsedSeconds ?? 0) + (status?.remainingSeconds ?? 0)
        let fraction = total > 0 ? min(1, elapsed / total) : 0
        return VStack(spacing: 4) {
            ProgressView(value: fraction)
                .progressViewStyle(.linear)
                .tint(Color.accentColor)
            HStack {
                Text(formatTime(elapsed))
                    .font(.caption.monospacedDigit())
                    .foregroundStyle(.secondary)
                Spacer()
                Text("-\(formatTime(max(0, total - elapsed)))")
                    .font(.caption.monospacedDigit())
                    .foregroundStyle(.secondary)
            }
        }
    }

    private func transportControls(api: APIClient) -> some View {
        HStack(spacing: 40) {
            Button {
                Task { await run { try await api.stop() } }
            } label: {
                Image(systemName: "stop.fill")
                    .font(.title)
            }
            .foregroundStyle(.primary)

            Button {
                Task {
                    await run {
                        if status?.isPlaying == true {
                            try await api.stop()
                        } else {
                            try await api.resume()
                        }
                    }
                }
            } label: {
                Image(systemName: status?.isPlaying == true ? "pause.circle.fill" : "play.circle.fill")
                    .font(.system(size: 64))
            }
            .foregroundStyle(Color.accentColor)

            Button {
                Task { await run { try await api.skipToNext() } }
            } label: {
                Image(systemName: "forward.fill")
                    .font(.title)
            }
            .foregroundStyle(.primary)
        }
    }

    @ViewBuilder
    private func extrasRow(api: APIClient) -> some View {
        let repeatOn = status?.repeatMode == true
        HStack(spacing: 12) {
            Button {
                Task { await run { try await api.setRepeatMode(enabled: !repeatOn) } }
            } label: {
                Label("Repeat", systemImage: "repeat")
                    .font(.subheadline)
            }
            .buttonStyle(.bordered)
            .tint(repeatOn ? Color.accentColor : .secondary)
            .accessibilityValue(repeatOn ? "On" : "Off")

            if features?.aiEnabled == true, status?.librarySong != nil {
                Button {
                    Task {
                        await run { try await api.startAiAutoplayFromCurrent() }
                        message = "Playing more like this."
                    }
                } label: {
                    Label("More Like This", systemImage: "sparkles")
                        .font(.subheadline)
                }
                .buttonStyle(.bordered)
            }
        }
    }

    private func volumeSection(api: APIClient) -> some View {
        HStack(spacing: 12) {
            Image(systemName: "speaker.fill")
                .foregroundStyle(.secondary)
            Slider(value: $volume, in: 0...100, step: 1) { editing in
                if editing {
                    isDraggingVolume = true
                } else {
                    // Keep the guard up until the server has accepted the new level,
                    // otherwise a status update landing between finger-up and the
                    // request completing snaps the slider back to the old value.
                    Task {
                        try? await api.setVolume(Int(volume))
                        await speaker.refreshNow()
                        isDraggingVolume = false
                    }
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
                Task { await run { try await api.cancelSleepTimer() } }
            }
            .font(.caption)
        }
        .padding(.horizontal, 16)
        .padding(.vertical, 8)
        .background(.orange.opacity(0.15), in: RoundedRectangle(cornerRadius: 10))
    }

    // MARK: - Helpers

    /// Runs a player command, then refreshes status (which also updates
    /// widgets and the Live Activity).
    private func run(_ command: () async throws -> Void) async {
        message = nil
        do {
            try await command()
        } catch {
            message = error.localizedDescription
        }
        await speaker.refreshNow()
    }

    private func startLiveActivity(_ connection: ServerConnection) async {
        if speaker.status == nil {
            await speaker.refreshNow()
        }
        guard let status = speaker.status else {
            message = "Couldn't reach \(connection.name)."
            return
        }
        await activities.start(for: connection, status: status)
    }

    private func formatTime(_ seconds: Double) -> String {
        let total = Int(seconds.rounded(.down))
        let h = total / 3600, m = (total % 3600) / 60, s = total % 60
        return h > 0 ? String(format: "%d:%02d:%02d", h, m, s) : String(format: "%d:%02d", m, s)
    }
}

// MARK: - This iPhone

/// Now Playing for music playing on the phone itself.
struct LocalNowPlayingView: View {
    @Environment(LocalPlayer.self) private var localPlayer
    @State private var scrubbing: Double?

    var body: some View {
        if let song = localPlayer.currentSong {
            ScrollView {
                VStack(spacing: 24) {
                    artwork(for: song)

                    VStack(spacing: 6) {
                        Text(song.displayTitle)
                            .font(.title2.bold())
                            .multilineTextAlignment(.center)
                        Text(song.displayArtist)
                            .foregroundStyle(.secondary)
                        Text(song.displayAlbum)
                            .font(.caption)
                            .foregroundStyle(.tertiary)
                    }

                    progress

                    HStack(spacing: 40) {
                        Button {
                            localPlayer.skipToPrevious()
                        } label: {
                            Image(systemName: "backward.fill")
                                .font(.title)
                        }
                        .foregroundStyle(.primary)

                        Button {
                            if localPlayer.isPlaying { localPlayer.pause() } else { localPlayer.resume() }
                        } label: {
                            Image(systemName: localPlayer.isPlaying ? "pause.circle.fill" : "play.circle.fill")
                                .font(.system(size: 64))
                        }
                        .foregroundStyle(Color.accentColor)

                        Button {
                            localPlayer.skipToNext()
                        } label: {
                            Image(systemName: "forward.fill")
                                .font(.title)
                        }
                        .foregroundStyle(.primary)
                        .disabled(localPlayer.currentIndex + 1 >= localPlayer.songs.count)
                    }

                    HStack {
                        Text("Song \(localPlayer.currentIndex + 1) of \(localPlayer.songs.count)")
                            .font(.caption)
                            .foregroundStyle(.secondary)
                        Spacer()
                        RoutePicker()
                            .frame(width: 32, height: 32)
                    }
                }
                .padding()
            }
        } else {
            ContentUnavailableView {
                Label("Nothing Playing on This iPhone", systemImage: "iphone")
            } description: {
                Text("Choose songs in Library or Playlists to play them here, or switch back to the speaker above.")
            }
        }
    }

    private func artwork(for song: Song) -> some View {
        Group {
            if let connection = localPlayer.connection {
                AsyncImage(url: APIClient.client(for: connection).albumArtURL(songId: song.songId)) { phase in
                    if let image = phase.image {
                        image.resizable().scaledToFill()
                    } else {
                        placeholder
                    }
                }
                .id(song.songId)
            } else {
                placeholder
            }
        }
        .frame(width: 160, height: 160)
        .clipShape(RoundedRectangle(cornerRadius: 16))
        .shadow(radius: 8)
    }

    private var placeholder: some View {
        Rectangle()
            .fill(Color(.systemGray5))
            .overlay {
                Image(systemName: "music.note")
                    .font(.system(size: 56))
                    .foregroundStyle(.secondary)
            }
    }

    private var progress: some View {
        let duration = max(localPlayer.duration, 0)
        let shown = scrubbing ?? localPlayer.elapsed
        return VStack(spacing: 4) {
            Slider(
                value: Binding(
                    get: { min(shown, duration) },
                    set: { scrubbing = $0 }
                ),
                in: 0...max(duration, 1)
            ) { editing in
                if !editing, let target = scrubbing {
                    localPlayer.seek(to: target)
                    scrubbing = nil
                }
            }
            .disabled(duration <= 0)
            HStack {
                Text(Self.format(shown))
                Spacer()
                Text("-\(Self.format(max(0, duration - shown)))")
            }
            .font(.caption.monospacedDigit())
            .foregroundStyle(.secondary)
        }
    }

    private static func format(_ seconds: Double) -> String {
        guard seconds.isFinite else { return "0:00" }
        let total = Int(seconds.rounded(.down))
        return String(format: "%d:%02d", total / 60, total % 60)
    }
}

/// The system AirPlay / Bluetooth output picker.
private struct RoutePicker: UIViewRepresentable {
    func makeUIView(context: Context) -> AVRoutePickerView {
        let view = AVRoutePickerView()
        view.prioritizesVideoDevices = false
        return view
    }

    func updateUIView(_ uiView: AVRoutePickerView, context: Context) {}
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
