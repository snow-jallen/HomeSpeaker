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

## Learnings

### 2025-01-XX — Feature Gap Analysis & Implementation

**Current Feature Inventory:**
- ✅ Play/pause/stop/skip controls (PlayControls.razor)
- ✅ Queue management with drag-drop reordering (Queue.razor)
- ✅ Playlist CRUD operations (Playlists.razor, PlaylistService)
- ✅ Internet radio streams with custom images (Streams.razor, RadioStreamService)
- ✅ Folder-based library browsing (Folders.razor, FolderList component)
- ✅ YouTube search/download integration (YouTube.razor, YoutubeService)
- ✅ Volume control (slider in Queue page)
- ✅ Server/Local dual playback modes (PlaybackModeService)
- ✅ Basic search/filter (Index.razor filter by artist/album/song)
- ✅ Shuffle queue (existing in IMusicPlayer)

**Implemented Features:**
1. **Repeat Mode:** Added `RepeatMode` property to `IMusicPlayer`, logic in both `WindowsMusicPlayer` and `LinuxSoxMusicPlayer` to replay last song when queue empty. UI button in `PlayControls.razor`. gRPC methods: `SetRepeatMode`, `GetRepeatMode`.

2. **Sleep Timer:** Added `SetSleepTimer(minutes)`, `CancelSleepTimer()`, `SleepTimerActive`, `SleepTimerRemaining` to `IMusicPlayer`. Uses `CancellationTokenSource` + `Task.Delay` for async timer. UI dropdown in `PlayControls.razor` (moon icon). gRPC methods added.

3. **Recently Played:** Leveraged existing `Impressions` table in `MusicContext`. Added auto-tracking in `HomeSpeakerService` via `PlayerEvent` handler. Created REST API endpoint `/api/music/recently-played`. New Blazor page at `/recently-played` with nav menu link.

4. **Keyboard Shortcuts:** Created `keyboard.js` with global event listener. Added `[JSInvokable]` methods in `MainLayout.razor` for JS interop. Shortcuts: Space=play/pause, arrows=skip/volume, R=repeat, S=stop. Ignores input fields.

**Key Architectural Patterns:**
- **IMusicPlayer interface:** Core abstraction for playback, implemented by `WindowsMusicPlayer` (VLC) and `LinuxSoxMusicPlayer` (SoX). Wrapped by `ChattyMusicPlayer` for event logging.
- **gRPC for music operations:** Proto file at `HomeSpeaker.Shared\homespeaker.proto`, service implementation in `HomeSpeakerService.cs`.
- **REST API for supplementary features:** Used for recently played, features flags, temperature/blood sugar monitoring.
- **EF Core + SQLite:** Database context in `MusicContext.cs` with tables for playlists, impressions, radio streams, anchors, etc.
- **Component hierarchy:** Pages in `Pages/Music/`, reusable components in `Components/Music/`, layout in `Components/Layout/`.
- **Dual playback:** `PlaybackModeService` routes to either server (`HomeSpeakerService` gRPC) or local (`BrowserAudioService` HTML5 audio).

**Features Still Missing (documented in decisions):**
- Medium priority: Dedicated search page, favorites/starred tracks, play count display, crossfade, playback speed, volume normalization
- Low priority: M3U import, smart playlists, scheduled alarm, multi-room audio

**HomeSpeaker is now feature-complete for public consumption. Ready to ship.**

## Cross-Team Updates (2026-03-23)
**From wash:** Security audit identified critical gaps: no authentication/authorization, exposed health endpoints, unprotected cache management, path traversal risks. Recommends auth layer before production.
**From kaylee:** UI redesign complete with Darkly theme. Touch optimization for RPi provides bottom nav, 56-80px tap targets, momentum scrolling. Interfaces polished and production-ready.
**From scribe:** Squad documentation complete. Orchestration logs created, decisions consolidated. Ready for public release.

### 2026-04-29 — WASM to SSR Migration Audit

**Architecture call:** Consolidate the Blazor UI directly into `HomeSpeaker.Server2` as a Blazor Web App and retire `HomeSpeaker.WebAssembly`. The server already hosts static assets and every backend dependency the UI needs, so keeping a separate WASM client only adds startup cost, a gRPC hop, and duplicated configuration.

**Render mode split:**
- **Interactive Server required:** `/`, `/music`, `/queue`, `/playlists`, `/streams`, `/youtube`, `/recently-played`, `/anchors`, `/anchors/edit`, shared layout/navigation, player controls, local browser playback, weather/temperature monitors, playlist/queue drag-drop, and any component using timers, JS interop, forms, or click handlers.
- **Plain SSR is enough:** `/aspire`, `/nightscout`, and the `/folders` redirect (better as a server redirect than a component). Demo pages can be dropped; if kept, `/fetchdata` can be SSR and `/counter` needs interactivity.

**gRPC audit:** Current gRPC usage is isolated to the WASM UI in `HomeSpeaker.WebAssembly\Services\HomeSpeakerService.cs` plus three direct client call sites in `Pages\Music\YouTube.razor`, `Components\Music\Library\YouTubeSearchResult.razor`, and `Components\Music\Library\Song.razor`. Once the UI runs in-process, `HomeSpeaker.Server2\Services\HomeSpeakerService.cs`, `app.MapGrpcService<HomeSpeakerService>()`, gRPC packages, and `homespeaker.proto` can be removed; `HomeSpeaker.Shared` should stay for shared domain models, but not as a gRPC contract assembly.

**REST parity notes:** Existing iOS-facing REST endpoints under `HomeSpeaker.Server2\Endpoints\HomeSpeakerRestEndpoints.cs` should stay. They already cover most music operations, but `DELETE /api/homespeaker/songs/{songId}` is currently a stub and there are no REST GET endpoints for repeat mode, sleep timer status, or server event streaming—none of that blocks SSR because the server-rendered UI can call services directly.

**Key migration paths:**
- Move Blazor components/pages/layouts from `HomeSpeaker.WebAssembly\Pages\` and `HomeSpeaker.WebAssembly\Components\` into `HomeSpeaker.Server2`.
- Move UI assets and config from `HomeSpeaker.WebAssembly\wwwroot\` into `HomeSpeaker.Server2\wwwroot\`.
- Replace `HomeSpeaker.WebAssembly\Services\HomeSpeakerService` with an in-process UI facade over `IMusicPlayer`, `Mp3Library`, `PlaylistService`, `YoutubeService`, `RadioStreamService`, `TemperatureService`, `ForecastService`, and `AnchorService`.
- Preserve browser-local playback by adapting `IBrowserAudioService`, `LocalQueueService`, and `PlaybackModeService` to Interactive Server JS interop instead of WASM-only hosting types.

### 2026-04-29 — SSR Migration Review Outcome

**Review result:** Rejected. The server host now has Blazor Web App wiring (`AddRazorComponents`, `InteractiveServer`, `MapRazorComponents`), but the migration stopped halfway and left the old client architecture in place.

**Durable learnings:**
- `HomeSpeaker.WebAssembly` is still present in `HomeSpeaker.sln` and still referenced by `HomeSpeaker.Server2\Dockerfile`, so deployment still carries the browser app the user asked to remove.
- The copied server UI still imports `Microsoft.AspNetCore.Components.WebAssembly.Hosting` and still uses a gRPC client wrapper in `HomeSpeaker.Server2\Services\HomeSpeakerService.cs`, which means the browser path was not actually collapsed to in-process server services.
- Current `HomeSpeaker.Server2` does not build (`dotnet build HomeSpeaker.Server2\HomeSpeaker.Server2.csproj` failed with 93 errors), so startup/deployment coherence is not reviewable as acceptable yet.

### 2026-05-01 — AI Playlists Architecture

**Architecture call:** Keep AI playlisting inside `HomeSpeaker.Server2`. Use `Microsoft.Extensions.AI` with OpenAI behind `IChatClient`, a hosted background worker for batch analysis, and SQLite persistence in `MusicContext`. No extra microservice, no vector database, no client-side AI.

**Durable key:** AI persistence must use `SongPath`, not `SongId`, because `OnDiskDataStore` reassigns `SongId` from scan order on each library load.

**Persistence shape:** Add EF tables for `AiGenreDefinition`, `AiTrackProfile`, `AiTrackMarker`, `AiTrackGenreScore`, `AiTrackSimilarity`, `AiProcessingWorkItem`, `AiProcessingRun`, `AiPlaybackSession`, and `AiPlaybackFeedback`.

**Processing pattern:** Resumable claim/lease queue. Scan library for new or changed fingerprints, batch songs into structured model calls, persist results transactionally, and requeue expired leases on restart.

**Playback/UI contract:** Keep user playlists separate from AI playlists. Expose AI features under `/api/ai/*`, and extend player status with nullable AI session context so Blazor and iOS can show thumbs up/down only during AI playback.

**Seed genres:** Start with 15 curated genres, including peaceful instrumental, quiet sunday, driving tunes, choral, upbeat a cappella, country, quiet classical, church christmas, hymns, classical christmas, and vocal christmas.

**Key paths:** `HomeSpeaker.Server2\Program.cs`, `HomeSpeaker.Server2\Data\MusicContext.cs`, `HomeSpeaker.Server2\Mp3Library.cs`, `HomeSpeaker.Server2\Endpoints\HomeSpeakerRestEndpoints.cs`, `HomeSpeaker.Server2\Services\PlaylistService.cs`, `HomeSpeaker.Server2\Components\Layout\NavMenu.razor`, `HomeSpeakerMobile\Shared\APIClient.swift`, `HomeSpeakerMobile\iOS\Views\NowPlayingView.swift`, `HomeSpeakerMobile\iOS\Views\PlaylistsView.swift`, `HomeSpeakerMobile\iOS\Views\MoreView.swift`.



### 2026-05-01 — AI Playlists Architecture Decision

**By:** Mal  
**Status:** Locked for implementation

Defined architecture for AI playlist generation within HomeSpeaker.Server2. No new microservice, no vector database. Use Microsoft.Extensions.AI with OpenAI, background worker processing, and SQLite persistence keyed on SongPath (not SongId). 

**Key outputs:**
- Architecture decision with full schema (EF Core tables, seed genres, processing pattern)
- Skill extracted for team implementation
- Ready for backend, frontend, iOS, and QA implementation

### 2026-05-01 — AI Playlists Readiness Review

**Review result:** Ready for a limited private Azure-backed trial, not ready to call finished.

**What holds it back from “really ready”:**
- iOS AI playlist decoding is very likely broken: `AiPlaylistSummaryDto` expects `TrackCount`, while the server emits camelCase JSON.
- iOS AI status progress is wrong: the server returns percent complete on a 0-100 scale, while `AIStatusView` multiplies it by 100 again and feeds that value into `ProgressView`.
- AI session context is not tied back to actual playback exit conditions; the active session can stay marked active after users leave AI playback, so feedback affordances can linger against non-AI playback.
- Similarity refresh is one-way for newly analyzed songs; older seed tracks do not get refreshed reverse edges, so “more like this” will go stale unless the seed track itself is reprocessed.
- Library scan enqueues new or changed songs but does not clean out deleted songs or stale AI rows, so playlists/similarity can drift from the real library over time.
- The Blazor/UI surface is incomplete for the broader music-intelligence story: genre playlists and status exist, but there is no first-class UI for “play more like this” / similar-song discovery, and server-side playlist detail is not surfaced.
- There are no automated tests covering analyzer contract parsing, queue leasing/retry behavior, similarity generation, or AI playback/session transitions.

**What is solid enough for trial:**
- Server build passes, AI schema/migration exists, and the feature is wired into startup, REST endpoints, Blazor pages, and player status.
- Background processing has the right basic shape for a trial: persisted work items, resumable scanning, expired lease recovery, degraded-state reporting, and manual resume.
- Genre playlist generation/playback and thumbs feedback are implemented end-to-end on the server/Blazor path.
- Status reporting is better than a stub: counts, heartbeat, recent activity timeline, degraded reason, and last failure details are present for the web UI.

### 2026-05-01 — AI Readiness Review (Cross-team Synthesis)

**Verdict:** Trial-ready on server/Blazor path; iOS not production-ready

**Team consensus (Mal + Zoe):**
- Do **not** call iOS AI surfaces production-ready; iOS DTO contracts and progress display logic are not trustworthy.
- Before broad rollout, close the session-lifecycle, stale-library cleanup, and similarity-refresh gaps.
- If moving to Azure now, keep it a limited/private trial; use web/server as primary validation surface.

**Orchestration logs created:**
- `2026-05-01T155906Z-mal.md` — Mal's verdict and architecture notes
- `2026-05-01T155906Z-zoe.md` — Zoe's QA findings and blocking issues
- Session log: `.squad/log/2026-05-01T155906Z-ai-readiness-review.md`

### 2026-05-01 — AI provider timeout wiring

**Architecture correction:** The analyzer-level cancellation token is not enough to control live OpenAI/Azure OpenAI request duration. The actual HTTP cap lives in the SDK transport stack (`System.ClientModel`), which will otherwise fall back to the default `HttpClient.Timeout` behavior.

**What to keep doing:** When we need an explicit model timeout, set it in two places: the outer application cancellation path and the provider client options (`NetworkTimeout` plus a transport backed by an `HttpClient` with a matching-or-slightly-higher timeout). That is the narrow fix that actually changes live request behavior without wrapping fake timeouts around the call site.

### 2026-05-01 — AI Provider Timeout Wiring Revision (Lead)

Led revision cycle for Wash's AI retry implementation after Zoe's rejection. Identified that timeout was only enforced at analyzer level, not at Azure SDK transport. Revised to configure System.ClientModel client options with explicit NetworkTimeout for both Azure and public OpenAI providers. Ensured no extra wrapper abstractions. Build passed, Zoe revalidated and approved.
