# Project Context
- **Owner:** Jonathan Allen
- **Project:** HomeSpeaker — a home audio/music player system with Blazor WebAssembly frontend and .NET backend
- **Stack:** .NET 8 / C#, Blazor WebAssembly, ASP.NET Core, Bootstrap/Bootswatch CSS, Docker, SQLite, gRPC/SignalR
- **Created:** 2026-03-23

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

