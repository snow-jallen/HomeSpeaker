import Combine
import Foundation
import Network

/// Browses the local network for HomeSpeakers advertising `_homespeaker._tcp`
/// (the installer's Avahi service). Each result's address comes from its TXT
/// record (`host`, `port`, `scheme`), so no connection is needed to resolve it.
@MainActor
final class SpeakerBrowser: ObservableObject {
    @Published private(set) var found: [DiscoveredSpeaker] = []

    private var browser: NWBrowser?
    private let queue = DispatchQueue(label: "com.homespeaker.speaker-browser")

    func start() {
        guard browser == nil else { return }
        let browser = NWBrowser(
            for: .bonjourWithTXTRecord(type: "_homespeaker._tcp", domain: nil),
            using: .tcp)
        browser.browseResultsChangedHandler = { @Sendable [weak self] results, _ in
            let speakers = results
                .compactMap(SpeakerBrowser.speaker(from:))
                .sorted { $0.name.localizedCaseInsensitiveCompare($1.name) == .orderedAscending }
            Task { @MainActor in
                // Ignore a late update that arrives after stop().
                guard let self, self.browser != nil else { return }
                self.found = speakers
            }
        }
        browser.start(queue: queue)
        self.browser = browser
    }

    func stop() {
        browser?.cancel()
        browser = nil
        found = []
    }

    nonisolated private static func speaker(from result: NWBrowser.Result) -> DiscoveredSpeaker? {
        guard case let .service(name, _, _, _) = result.endpoint else { return nil }

        var txt: [String: String] = [:]
        if case let .bonjour(record) = result.metadata {
            txt = record.dictionary
        }

        // The TXT record is whatever the network sends, so only accept values
        // that make a valid URL (ServerConnection.baseURL builds one from them).
        let host = nonEmpty(txt["host"]) ?? "\(name).local"
        guard isValidHost(host) else { return nil }
        let port = nonEmpty(txt["port"]).flatMap { Int($0) }.flatMap { (1...65535).contains($0) ? $0 : nil } ?? 80
        let scheme = nonEmpty(txt["scheme"])?.lowercased() == "https" ? "https" : "http"
        guard let url = URL(string: "\(scheme)://\(host):\(port)"), url.host != nil else { return nil }
        return DiscoveredSpeaker(id: name, name: name, host: host, port: port, scheme: scheme)
    }

    /// A bare host name or address: no whitespace and none of the characters
    /// that would end the host part of a URL.
    nonisolated private static func isValidHost(_ host: String) -> Bool {
        !host.isEmpty && host.rangeOfCharacter(from: CharacterSet.whitespacesAndNewlines.union(CharacterSet(charactersIn: "/?#@\\"))) == nil
    }

    nonisolated private static func nonEmpty(_ value: String?) -> String? {
        guard let trimmed = value?.trimmingCharacters(in: .whitespacesAndNewlines), !trimmed.isEmpty else {
            return nil
        }
        return trimmed
    }
}
