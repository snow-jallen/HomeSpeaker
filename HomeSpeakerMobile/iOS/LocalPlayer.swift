import AVFoundation
import MediaPlayer
import Observation

enum PlaybackDestination: String {
    case speaker, device
}

@Observable
final class LocalPlayer {

    // MARK: - Destination (persisted across launches)

    var destination: PlaybackDestination = .speaker {
        didSet { UserDefaults.standard.set(destination.rawValue, forKey: "hs_destination") }
    }

    // MARK: - Queue state (drives SwiftUI)

    private(set) var songs: [Song] = []
    private(set) var currentIndex: Int = -1
    private(set) var isPlaying: Bool = false
    private(set) var elapsed: Double = 0
    private(set) var duration: Double = 0

    var currentSong: Song? {
        guard currentIndex >= 0, currentIndex < songs.count else { return nil }
        return songs[currentIndex]
    }
    var progress: Double { duration > 0 ? elapsed / duration : 0 }

    // MARK: - Internal AVFoundation objects (not observed)

    @ObservationIgnored private let player = AVPlayer()
    @ObservationIgnored private var timeObserver: Any?
    @ObservationIgnored private var rateObservation: NSKeyValueObservation?
    @ObservationIgnored var baseURL: URL?

    // MARK: - Init

    init() {
        if let raw = UserDefaults.standard.string(forKey: "hs_destination"),
           let saved = PlaybackDestination(rawValue: raw) {
            destination = saved
        }
        configureAudioSession()
        configureRemoteCommands()
        addTimeObserver()
        rateObservation = player.observe(\.rate, options: .new) { [weak self] player, _ in
            DispatchQueue.main.async { self?.isPlaying = player.rate > 0 }
        }
        NotificationCenter.default.addObserver(
            self,
            selector: #selector(itemDidEnd),
            name: .AVPlayerItemDidPlayToEndTime,
            object: nil
        )
    }

    deinit {
        if let timeObserver { player.removeTimeObserver(timeObserver) }
        rateObservation?.invalidate()
    }

    // MARK: - Public queue operations

    func play(songs: [Song], from index: Int = 0, baseURL: URL) {
        self.baseURL = baseURL
        self.songs = songs
        loadItem(at: index)
    }

    func enqueue(songs newSongs: [Song], baseURL: URL) {
        if self.baseURL == nil { self.baseURL = baseURL }
        let wasEmpty = songs.isEmpty
        songs.append(contentsOf: newSongs)
        if wasEmpty { loadItem(at: 0) }
    }

    func pause() { player.pause() }
    func resume() { player.play() }

    func skipToNext() {
        let next = currentIndex + 1
        guard next < songs.count else { return }
        loadItem(at: next)
    }

    func skipToPrevious() {
        if elapsed > 3 {
            player.seek(to: .zero)
        } else if currentIndex > 0 {
            loadItem(at: currentIndex - 1)
        }
    }

    func remove(at index: Int) {
        guard index < songs.count else { return }
        songs.remove(at: index)
        if index < currentIndex {
            currentIndex -= 1
        } else if index == currentIndex {
            if songs.isEmpty { clearQueue() }
            else { loadItem(at: min(currentIndex, songs.count - 1)) }
        }
    }

    func clearQueue() {
        player.replaceCurrentItem(with: nil)
        songs = []
        currentIndex = -1
        isPlaying = false
        elapsed = 0
        duration = 0
        MPNowPlayingInfoCenter.default().nowPlayingInfo = nil
    }

    // MARK: - Private

    private func loadItem(at index: Int) {
        guard index >= 0, index < songs.count, let base = baseURL else { return }
        currentIndex = index
        let song = songs[index]
        guard let url = streamURL(songId: song.songId, base: base) else { return }
        let item = AVPlayerItem(url: url)
        player.replaceCurrentItem(with: item)
        player.play()
        updateNowPlayingInfo()
    }

    private func streamURL(songId: Int, base: URL) -> URL? {
        let str = base.absoluteString.hasSuffix("/")
            ? String(base.absoluteString.dropLast())
            : base.absoluteString
        return URL(string: "\(str)/api/music/\(songId)")
    }

    @objc private func itemDidEnd() {
        DispatchQueue.main.async { [weak self] in self?.skipToNext() }
    }

    private func configureAudioSession() {
        do {
            try AVAudioSession.sharedInstance().setCategory(.playback, mode: .default)
            try AVAudioSession.sharedInstance().setActive(true)
        } catch {
            print("LocalPlayer: audio session error: \(error)")
        }
    }

    private func configureRemoteCommands() {
        let center = MPRemoteCommandCenter.shared()

        center.playCommand.isEnabled = true
        center.playCommand.addTarget { [weak self] _ in
            guard let self, !self.songs.isEmpty else { return .commandFailed }
            DispatchQueue.main.async { self.resume() }
            return .success
        }

        center.pauseCommand.isEnabled = true
        center.pauseCommand.addTarget { [weak self] _ in
            guard let self, !self.songs.isEmpty else { return .commandFailed }
            DispatchQueue.main.async { self.pause() }
            return .success
        }

        center.nextTrackCommand.isEnabled = true
        center.nextTrackCommand.addTarget { [weak self] _ in
            guard let self, self.currentIndex + 1 < self.songs.count else { return .commandFailed }
            DispatchQueue.main.async { self.skipToNext() }
            return .success
        }

        center.previousTrackCommand.isEnabled = true
        center.previousTrackCommand.addTarget { [weak self] _ in
            guard let self, !self.songs.isEmpty else { return .commandFailed }
            DispatchQueue.main.async { self.skipToPrevious() }
            return .success
        }

        center.changePlaybackPositionCommand.isEnabled = true
        center.changePlaybackPositionCommand.addTarget { [weak self] event in
            guard let self, let e = event as? MPChangePlaybackPositionCommandEvent else { return .commandFailed }
            let time = CMTime(seconds: e.positionTime, preferredTimescale: CMTimeScale(NSEC_PER_SEC))
            self.player.seek(to: time)
            return .success
        }
    }

    private func addTimeObserver() {
        let interval = CMTime(seconds: 0.5, preferredTimescale: CMTimeScale(NSEC_PER_SEC))
        timeObserver = player.addPeriodicTimeObserver(forInterval: interval, queue: .main) { [weak self] time in
            guard let self else { return }
            let secs = time.seconds
            if secs.isFinite { self.elapsed = secs }
            if let dur = self.player.currentItem?.duration.seconds, dur.isFinite, !dur.isNaN {
                self.duration = dur
            }
            self.updateNowPlayingInfo()
        }
    }

    private func updateNowPlayingInfo() {
        guard let song = currentSong else { return }
        var info: [String: Any] = [
            MPMediaItemPropertyTitle: song.displayTitle,
            MPMediaItemPropertyArtist: song.displayArtist,
            MPMediaItemPropertyAlbumTitle: song.displayAlbum,
            MPNowPlayingInfoPropertyElapsedPlaybackTime: elapsed,
            MPNowPlayingInfoPropertyPlaybackRate: isPlaying ? 1.0 : 0.0,
        ]
        if duration > 0 { info[MPMediaItemPropertyPlaybackDuration] = duration }
        MPNowPlayingInfoCenter.default().nowPlayingInfo = info
    }
}
