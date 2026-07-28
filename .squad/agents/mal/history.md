# Project Context
- **Owner:** Jonathan Allen
- **Project:** HomeSpeaker — a home audio/music player system with Blazor WebAssembly frontend and .NET backend
- **Stack:** .NET 8 / C#, Blazor WebAssembly, ASP.NET Core, Bootstrap/Bootswatch CSS, Docker, SQLite, gRPC/SignalR
- **Created:** 2026-03-23

## Core Context

### Architecture Summary (2025-01 — Feature Complete & Released)
- **Feature-complete** core: playback controls, queue management, playlists, radio streams, folder browsing, YouTube integration, volume control, dual playback modes, search/filter, shuffle, repeat, sleep timer, keyboard shortcuts, recently-played tracking.
- **Architecture patterns:** IMusicPlayer interface (core abstraction), gRPC for music ops, REST API for supplementary features, EF Core + SQLite, component hierarchy, dual playback routing.
- **Cross-team context:** Wash completed security audit (recommended auth layer); Kaylee finalized Darkly UI theme with RPi touch optimization; Scribe documented squad setup.

### Major Milestones
- **2026-04-29:** SSR migration audit initiated; halfway migration left in rejected state; server host now has Blazor Web App wiring but old client architecture still in place.
- **2026-05-01:** AI playlists architecture locked; using `Microsoft.Extensions.AI` with OpenAI, background worker processing, SQLite persistence keyed on SongPath.

### AI Playlists (May 1-2, 2026)
- Architecture: In-process backend with `Microsoft.Extensions.AI`, OpenAI/Azure OpenAI provider, background worker, SQLite persistence by SongPath (not SongId).
- QA cycle: Initial trial-not-ready verdict (iOS data-contract issues, missing retry/recovery). Zoe + Wash implementation cycle closed all blocking issues.
- Final status: Production-ready with telemetry oversight.
- Key schema: AiGenreDefinition, AiTrackProfile, AiProcessingWorkItem, etc. 15 seed genres.

### 2026-04-29 SSR Migration Audit & Outcome
Analyzed WASM to SSR consolidation. Rejected: server has Blazor Web App wiring but WebAssembly project still referenced. gRPC client still in Server2 services. Build has 93 errors. Requires full removal of WebAssembly project and in-process service refactor before passing review.

### 2026-05-01 AI Playlists Architecture Decision
Locked architecture for AI playlist generation. No new microservice, no vector database. Use Microsoft.Extensions.AI with OpenAI, background worker processing, SQLite persistence keyed on SongPath (not SongId). 15 curated seed genres defined.

## Learnings
<!-- Siri/Offline and recent work below -->

### 2026-05-14 — Siri controls and offline downloads plan

Reviewed the iOS Siri/App Intents surface and the mobile/server library contract for offline playback. The current Siri path is too fuzzy: `MediaQueryEntity` accepts free-form text, `bestMatch` does loose matching, and the intent layer reads `hs_connections` / `hs_selectedId` from `UserDefaults.standard` instead of a shared app-group store. The pragmatic fix is to narrow Siri to five dedicated commands only: next song, play fun music, play hymns, quiet down, and stop.

Locked the Siri mapping to existing capabilities instead of inventing metadata: `play fun music` should call AI genre `family-singalong`, and `play hymns` should call AI genre `hymns`. `quiet down` should halve the current volume, clamped to at least 1 when non-zero. Intents should target the selected server only, avoid opening the app, and share a common control path with widget intents where practical.

Locked the offline design as device-owned, not server-owned. Persist offline selection rules locally for artist, album, and song; expand those rules client-side from `GET /api/homespeaker/songs`; key all durable download state by `Song.Path`, not `SongId`; and store downloaded audio in Application Support. Local iPhone playback should prefer a downloaded file for a matching `songPath`, then fall back to streaming from `/api/music/{songId}`.

User preference is clear: Siri should be reliable and narrow, not flexible and ambiguous. Key paths for this work: `HomeSpeakerMobile\iOS\Intents\HomeSpeakerIntents.swift`, `HomeSpeakerMobile\iOS\Intents\HomeSpeakerShortcuts.swift`, `HomeSpeakerMobile\iOS\Intents\IntentHelpers.swift`, `HomeSpeakerMobile\watchOS\Widget\WidgetIntents.swift`, `HomeSpeakerMobile\iOS\Views\MusicLibraryView.swift`, `HomeSpeakerMobile\iOS\Views\MoreView.swift`, `HomeSpeakerMobile\iOS\LocalPlayer.swift`, `HomeSpeakerMobile\Shared\ConnectionStore.swift`, `HomeSpeakerMobile\Shared\Models.swift`, `HomeSpeakerMobile\Shared\APIClient.swift`, `HomeSpeaker.Server2\Program.cs`, `HomeSpeaker.Server2\Endpoints\HomeSpeakerRestEndpoints.cs`, and `HomeSpeaker.Server2\Data\MusicContext.cs`.

### 2026-05-14 — Final release review rerun

Re-checked the working tree for release readiness instead of trusting the previous pass. Server-side validation is fine: `dotnet build HomeSpeaker.sln`, `dotnet test HomeSpeaker.sln`, and `dotnet ef migrations has-pending-model-changes` all came back clean enough for this repo, so the EF snapshot is not the problem.

Rejected anyway on three concrete ship bugs. `Play Fun Music` does not map directly to AI genre `family-singalong`; it relies on fuzzy alias matching and can miss the intended playlist entirely. `Quiet Down` on both iOS intents and watch widget halves volume with `max(0, volume / 2)` instead of preserving non-zero volume at a minimum of `1`. Offline summary math in `OfflineDownloadsStore` also counts failed downloads as “pending,” which makes the More screen lie about state.

Key re-review paths: `HomeSpeakerMobile\iOS\Intents\HomeSpeakerIntents.swift`, `HomeSpeakerMobile\watchOS\Widget\WidgetIntents.swift`, `HomeSpeakerMobile\iOS\OfflineDownloadsStore.swift`.
### 2026-05-14 — Offline contract unification

Replaced the iOS dual offline system with the server-owned `/api/homespeaker/offline*` contract. The mobile app now reads the server manifest/targets, posts artist/album/song target changes back to the server, downloads media from the server-provided offline URL, and only keeps device-local file presence plus failure state on the phone.

Kept durable local tracking keyed by `Song.Path`, including best-effort migration of legacy local selections/download records that were still stored by transient song IDs. `OfflineDownloadsStore` is now the reconciliation layer between server manifest state and local files, while `LocalPlayer` still prefers downloaded files first and otherwise streams by `songPath` through `/api/homespeaker/offline/media`.

Key paths: `HomeSpeakerMobile\iOS\OfflineDownloadsStore.swift`, `HomeSpeakerMobile\Shared\APIClient.swift`, `HomeSpeakerMobile\Shared\Models.swift`, `HomeSpeakerMobile\iOS\LocalPlayer.swift`, `HomeSpeakerMobile\iOS\Views\OfflineDownloadsView.swift`.
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

