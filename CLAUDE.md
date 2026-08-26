# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What this is

HomeSpeaker is a self-hosted music player that runs on Raspberry Pis around a house (kitchen, upstairs, etc.). It serves a Blazor web UI, plays local MP3s through the Pi's audio hardware, streams internet radio and YouTube audio, generates AI playlists, and doubles as an AirPlay receiver. Each Pi runs an independent instance — there is no central server.

## Solution layout

Two projects (`HomeSpeaker.sln`), both `net10.0`:

- **HomeSpeaker.Server2** — the ASP.NET Core app. Blazor Server (interactive + SSR) UI, minimal-API REST endpoints, SignalR hubs, EF Core/SQLite persistence, and all the hosted background services.
- **HomeSpeaker.Shared** — DTOs and domain records (`Song`, `PlayerStatus`, anchor/offline/AI DTOs) shared between server and (historically) clients. No logic, just contracts.

`HomeSpeaker.Server2/HomeSpeaker.Server2.sln` is a stale single-project solution; use the top-level `HomeSpeaker.sln`.

## Build / run / test commands

```powershell
dotnet build HomeSpeaker.sln
dotnet run --project HomeSpeaker.Server2 --launch-profile https   # https://localhost:7238, http://localhost:5028
dotnet run --project HomeSpeaker.Server2 --launch-profile http    # http://0.0.0.0:5280
```

There is **no test project** in this repo — do not assume `dotnet test` exists. Verify changes by building and running.

Local prerequisites (see `README.md`): install ffmpeg (`winget install gyan.ffmpeg.shared`) and optionally run the Aspire Dashboard container for telemetry. On Windows the app uses `WindowsMusicPlayer`; on Linux/Pi it uses `LinuxSoxMusicPlayer` (needs `sox`/ffmpeg, present in the Docker base image).

### Warnings-as-errors

Debug builds set `TreatWarningsAsErrors=true` — a warning will fail your local build. Release builds suppress a long `NoWarn` list so CI/Docker publishes cleanly. If a build fails locally on a warning that CI would tolerate, the fix is to resolve the warning, not to change the csproj.

## Database & migrations

EF Core with SQLite (`MusicContext`). The connection string comes from config key `SqliteConnectionString` (in the container, `Data Source=/music/HomeSpeaker.db` — the DB lives on the media volume). `MigrationApplier` (a hosted service) applies pending migrations automatically at startup; `Program.cs` then sets SQLite PRAGMAs (WAL, NORMAL sync, 64MB cache, mmap).

To add a migration:

```powershell
dotnet ef migrations add <Name> --project HomeSpeaker.Server2
```

## Architecture

### Audio playback abstraction

`IMusicPlayer` (`IMusicPlayer.cs`) is the central playback contract — play/enqueue/skip/stop, queue management, volume, sleep timer, repeat, and a `PlayerEvent` event. Implementations are selected by OS at DI registration in `Program.cs`:

- **`WindowsMusicPlayer`** — dev machines.
- **`LinuxSoxMusicPlayer`** — the Pi. Shells out via CliWrap; reads ICY metadata for radio streams (`IcyMetadataReader`); resolves the output device through **`AudioDeviceDetector`**, which auto-selects an ALSA card (USB audio → headphone jack → built-in/HDMI) and mixer control, overridable with the `ALSA_CARD` env var.

Both are wrapped in **`ChattyMusicPlayer`** (a decorator that adds logging/eventing) before being exposed as the singleton `IMusicPlayer`. When touching playback behavior, prefer changing the decorator or the interface rather than duplicating logic across the two OS players.

### Music library

**`Mp3Library`** (singleton) is the in-memory catalog. It scans the media folder via `IFileSource`, parses tags via `ITagParser` (TagLibSharp), persists derived data through `IDataStore` (`OnDiskDataStore`), and keeps itself fresh with a debounced `FileSystemWatcher` on `*.mp3`. `Song.SongId` values are assigned here and used throughout the REST/streaming endpoints.

### UI and the service facade

Blazor components live under `HomeSpeaker.Server2/Components` (layout, music, health/weather widgets) and routable pages under `HomeSpeaker.Server2/Pages` (`@page` directives; e.g. `Pages/Music/*`, `Pages/Admin/*`, `Pages/Health/*`). Uses both **MudBlazor** and **FluentUI** component libraries.

**`HomeSpeakerService`** (scoped) is the facade Blazor components call into — it composes `Mp3Library`, `IMusicPlayer`, YouTube, playlist, radio, and AI services and raises `QueueChanged` / `StatusChanged` / `LibraryChanged` events for the UI to subscribe to. New UI-facing functionality usually belongs here rather than in a component.

### REST API

Minimal APIs. The bulk are mapped in `Endpoints/HomeSpeakerRestEndpoints.cs` via `MapHomeSpeakerApi()` (route group `/api/homespeaker`, split into song/player/playlist/queue/youtube/radio/offline-download groups) and `Endpoints/AiRestEndpoints.cs` via `MapAiApi()`. A number of standalone endpoints (temperature, blood sugar, forecast, anchors, recently-played, the `/api/music/{songId}` range-aware streaming endpoint, image search/upload) are defined inline in `Program.cs`.

### Background hosted services

Registered in `Program.cs` and run for the process lifetime: `MigrationApplier`, `DailyAnchorWorker`, `AirPlayReceiverService`, `LifecycleEvents`, `AiMusicAnalysisWorker`, and `VolumeMonitorService`. AI playlist analysis is gated by config and signalled through `AiProcessingSignal`.

### AI integration

`AddChatClient` in `Program.cs` builds an `IChatClient` (Microsoft.Extensions.AI) from the `AI` config section — Azure OpenAI or OpenAI, wrapped with logging + OpenTelemetry decorators. When neither is configured it falls back to `NullChatClient` and AI features quietly disable. AI playlist code lives in `Services/AiMusic*` and `Services/AiPlayback*`.

### Auxiliary widgets

Temperature, blood sugar (Nightscout via `NIGHTSCOUT_URL`), and weather forecast are cached HTTP-backed services with `GET`/refresh/clear-cache endpoints, surfaced as dashboard widgets. "Anchors" are a daily-habit/checklist feature (`AnchorService`, `DailyAnchorWorker`, `AnchorHub` SignalR hub at `/anchorHub`).

### Configuration keys

Use the constants in `ConfigKeys.cs` (`MediaFolder`, `FFMpegLocation`). Required config: `MediaFolder`, `SqliteConnectionString`. Optional: `AI:*`, `NIGHTSCOUT_URL`, `Temperature:ApiBaseUrl`, `ALSA_CARD`, OTEL exporter vars. Secrets on the Pi come from `/home/piuser/homespeaker-secrets.env` (never committed; template in `homespeaker-secrets.env.example`).

## Deployment

GitHub Actions, not manual. Pushing to `master` triggers `build-and-push.yml`, which builds a **linux/arm64** image and pushes it to `ghcr.io/snow-jallen/homespeaker:latest`. On success, `deploy.yml` runs on **self-hosted runners** (`kitchen`, `upstairs`), pulls the image, renews the Tailscale TLS cert, restarts via `docker-compose`, and refreshes the touchscreen browser. The app image builds on a pre-built base (`ghcr.io/snow-jallen/homespeaker-base`, rebuilt via `build-base-image.yml`) that carries sox/ffmpeg/vlc so app builds stay fast.

`docker-compose.yml` runs three containers per Pi: the app, an Aspire dashboard (telemetry), and a `shairport-sync` AirPlay receiver sharing the ALSA device and PulseAudio socket.

The README also documents tag-based release (`git tag -a yyyy.m.d`) — the current `master`-push pipeline is the live path.

## Conventions

- **Naming** (enforced via `.editorconfig` + Workleap/Meziantou analyzers): private fields are `camelCase` **without** an underscore prefix; private methods are often `camelCase`. Match the surrounding file.
- `ImplicitUsings` and `Nullable` are enabled in both projects.
- The `.squad/`, `.copilot/`, and `.github/agents/` directories are remnants of an experimental multi-agent ("squad") workflow that was removed from CI (`5a5ec72`); they are not part of the build and generally should be ignored.
