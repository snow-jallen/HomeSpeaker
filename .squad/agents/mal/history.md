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

