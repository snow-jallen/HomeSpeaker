import Foundation

/// A HomeSpeaker found on the local network through its `_homespeaker._tcp`
/// Bonjour advertisement.
struct DiscoveredSpeaker: Identifiable, Hashable {
    /// The Bonjour service name, unique on the network.
    let id: String
    /// The Bonjour service name, e.g. "HomeSpeaker on kitchen".
    let name: String
    let host: String
    let port: Int
    let scheme: String

    /// A friendly default name for the saved server: the service name without
    /// the "HomeSpeaker" wording the installer adds.
    var suggestedName: String {
        var result = name
        for prefix in ["HomeSpeaker on ", "HomeSpeaker "] where result.hasPrefix(prefix) {
            result = String(result.dropFirst(prefix.count))
        }
        if result.hasSuffix(" HomeSpeaker") {
            result = String(result.dropLast(" HomeSpeaker".count))
        }
        result = result.trimmingCharacters(in: .whitespaces)
        return result.isEmpty ? name : result
    }

    /// What goes in the server's Host field: the bare host for http, or a
    /// full base URL for any other scheme (ServerConnection uses a host that
    /// carries its own scheme as-is).
    var hostForConnection: String {
        scheme.lowercased() == "http" ? host : "\(scheme)://\(host):\(port)"
    }

    /// True when `connection` already points at this speaker.
    func matches(_ connection: ServerConnection) -> Bool {
        let savedHost = connection.host.trimmingCharacters(in: .whitespaces)
        if savedHost.caseInsensitiveCompare(hostForConnection) == .orderedSame {
            return connection.port == port || savedHost.contains("://")
        }
        return false
    }
}
