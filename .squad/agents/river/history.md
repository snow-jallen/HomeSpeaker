# Project Context
- **Owner:** Jonathan Allen
- **Project:** HomeSpeaker — a home audio/music player system
- **Stack:** .NET 8 / C#, Blazor WebAssembly, ASP.NET Core, SQLite, native iOS/watchOS via SwiftUI
- **Created:** 2026-04-30T21:44:23-06:00

## Learnings

<!-- Append new learnings below. Each entry is something lasting about the project. -->
- **2026-04-30:** HomeSpeakerMobile is a native SwiftUI app generated from `project.yml`, with shared Swift sources under `HomeSpeakerMobile/Shared` and platform UI under `HomeSpeakerMobile/iOS`.
- **2026-04-30:** iOS app structure: `ContentView` → `MainTabView` (missing; needs creation) → five main screens (Library, Queue, Playlists, Radio, More). Navigation via `@Environment(ConnectionStore)`. Local playback via `@Observable LocalPlayer`.
- **2026-04-30:** Playlist state is currently immutable after fetch (`Playlist` lacks AI metadata). For AI Playlists feature, extend `Playlist` struct with `isAIGenerated: Bool` and `aiStatus: AIPlaylistStatus?` fields.
- **2026-04-30:** API polling pattern in iOS: Use Timer in `@State`, start in `.onAppear`, stop in `.onDisappear`. Polling overhead risk mitigated by adaptive backoff (1s → 2s → 5s) and stopping when status != .generating.
- **2026-04-30:** Touch-first design (squad decision): All interactive elements ≥44×44px. Thumbs up/down feedback buttons should be 48×48px minimum on mobile, placed horizontally in song rows in AI playlists.
- **2026-05-01:** AI Playlists implementation: Kept separate from user playlists (`/api/ai/*` vs `/api/homespeaker/playlists`). Extended `PlayerStatus` with nullable `aiContext` field to enable thumbs feedback only during AI playback sessions. AI mode applies only to server playback; device-local playback via `LocalPlayer` remains separate.
- **2026-05-01:** AI feedback UI pattern: Thumbs up/down buttons shown in `NowPlayingView` only when `status.aiContext?.allowFeedback == true`. Buttons sized 48×48px, horizontally arranged with label "How's this pick?" for clarity. Feedback sent immediately on tap without confirmation.
- **2026-05-01:** AI Playlists tab navigation: Added as 5th tab (tag 4) between "Playlists" and "More" in `MainTabView`. Uses "sparkles" SF Symbol for AI branding. Separate detail view (`AIPlaylistDetailView`) shows genre description and song list with play/enqueue actions.
- **2026-05-01:** AI Status screen pattern: Polling every 3 seconds when `status.isProcessing == true`, stops when idle. Shows progress bar, track counts by state (completed/queued/processing/failed), and manual "Resume Processing" button. Accessible via toolbar icon in AI Playlists view.
- **2026-05-01:** Swift model extensions: Added `AiPlayerContextDto`, `AiPlaylistSummaryDto`, `AiPlaylistDto`, `AiLibraryStatusDto`, `AiFeedbackRequest` to `Models.swift`. All conform to `Codable` and use optional properties to gracefully handle partial server responses.
- **2026-05-14:** Siri/App Shortcuts work better in this app when phrases are fixed and app-scoped (`HomeSpeakerShortcuts.swift`) while broad media-query intents stay undiscoverable in `HomeSpeakerIntents.swift`.
- **2026-05-14:** Offline playback is client-managed in `HomeSpeakerMobile/iOS/OfflineDownloadsStore.swift`: it reuses `GET /api/music/{songId}`, stores files under Application Support `HomeSpeakerOffline`, and injects the store into SwiftUI via `HomeSpeakerApp`.
- **2026-05-14:** Library offline affordances live in `MusicLibraryView.swift` with artist/album buttons, per-track status icons, and a management surface in `OfflineDownloadsView.swift` linked from `MoreView.swift`.
- **2026-05-14:** In `HomeSpeakerMobile/iOS/Intents/HomeSpeakerIntents.swift`, nested alias-matching closures must bind the playlist item explicitly (`playlist in`) before using an inner `alias` closure; mixing explicit inner args with outer `$0` breaks Swift compilation.



### 2026-05-01 — AI Playlists iOS UI Analysis

**By:** River  
**Status:** Analysis complete; implementation in progress

Mapped iOS SwiftUI structure for AI playlists. Analyzed state management patterns, adaptive UI for portrait/landscape, and API polling strategy. Building components now.

**Key outputs:**
- iOS architecture analysis
- State management and polling patterns
- SwiftUI component stubs started
- Ready for feature implementation
## Siri/Offline Release — Complete (2026-05-14T21:32:28Z)

**Status:** ✅ APPROVED FOR RELEASE

**Team completion summary:**
- Mal: Architecture & final release review → approved
- River: Siri commands & mobile UX → complete
- Wash: Backend offline contract & critical fixes → complete
- Kaylee: Offline keying revision → approved
- Book: Integration & legacy migration → complete
- Zoe: QA & final verdict → APPROVED FOR RELEASE

**Final decision:** All review criteria met. Feature approved for production deployment.

**Platform limitation:** Apple device/simulator validation required remote procedures (Windows host).

---
