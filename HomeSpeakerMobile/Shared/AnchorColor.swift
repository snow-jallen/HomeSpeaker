import SwiftUI

/// The server's colour keys, matching wwwroot/css/anchors.css.
enum AnchorColor {
    static func of(_ key: String) -> Color {
        switch key {
        case "teal": return Color(red: 0.18, green: 0.83, blue: 0.75)
        case "amber": return Color(red: 0.98, green: 0.75, blue: 0.14)
        case "rose": return Color(red: 0.98, green: 0.44, blue: 0.52)
        case "violet": return Color(red: 0.65, green: 0.55, blue: 0.98)
        case "sky": return Color(red: 0.22, green: 0.74, blue: 0.97)
        case "lime": return Color(red: 0.64, green: 0.90, blue: 0.21)
        case "orange": return Color(red: 0.98, green: 0.57, blue: 0.24)
        default: return Color(red: 0.80, green: 0.84, blue: 0.88)
        }
    }
}
