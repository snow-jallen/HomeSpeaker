# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What this is

HomeSpeaker is a self-hosted music player that runs on Raspberry Pis around a house (kitchen, upstairs, etc.). It serves a Blazor web UI, plays local MP3s through the Pi's audio hardware, streams internet radio and YouTube audio, generates AI playlists, and doubles as an AirPlay receiver. Each Pi runs an independent instance — there is no central server.

## Solution layout

Three projects (`HomeSpeaker.sln`), all `net10.0`:

- **HomeSpeaker.Server2** — the ASP.NET Core app. Blazor Server (interactive + SSR) UI, minimal-API REST endpoints, SignalR hubs, EF Core/SQLite persistence, and all the hosted background services.
- **HomeSpeaker.Shared** — DTOs and domain records (`Song`, `PlayerStatus`, anchor/offline/AI DTOs) shared between server and (historically) clients. No logic, just contracts.
- **HomeSpeaker.Server2.Tests** — xUnit tests for Server2.

Other top-level pieces: `install/` (the one-command `install.sh`/`uninstall.sh` for Pis, the Avahi `_homespeaker._tcp` template and the nightly-update systemd units), `docker-compose.yml` (generic HTTP stack with optional `airplay` and `telemetry` profiles), `docker-compose.https.yml` (HTTPS overlay), `homespeaker.env.example` (household settings template), `docs/owner-migration.md` (maintainer's one-time checklist) and `LICENSE` (MIT). `readme.md` is for a new household, `DEPLOYMENT.md` for the maintainer pipeline.

### iOS / watchOS app

`HomeSpeakerMobile/` is a SwiftUI iPhone app with a watch app, not part of `HomeSpeaker.sln`. The Xcode project is generated from `project.yml` by XcodeGen (`xcodegen generate` on a Mac); the generated `.xcodeproj` is checked in, so regenerate and commit it when targets or folders change. Targets: `HomeSpeaker` (iOS), `HomeSpeakerWidgetExtension` (Home/Lock Screen widget, Live Activity, iOS 18 controls), `HomeSpeakerWatch`, `HomeSpeakerWatchWidget`. Source folders: `Shared/` (all targets: models, `APIClient`, `SharedConnections`), `iOSShared/` (iOS app + widget extension: `ServerEntity`, playback intents, Live Activity attributes), `iOS/`, `iOS/Widget/`, `watchOS/`. Saved servers live in the `group.com.homespeaker` App Group so intents and widgets can read them. `.github/workflows/ios-build.yml` compile-checks the app on a macOS runner for pushes to non-master branches touching `HomeSpeakerMobile/`, and uploads the generated project as an artifact.

## Build / run / test commands

```powershell
dotnet build HomeSpeaker.sln
dotnet run --project HomeSpeaker.Server2 --launch-profile https   # https://localhost:7238, http://localhost:5028
dotnet run --project HomeSpeaker.Server2 --launch-profile http    # http://0.0.0.0:5280
```

Unit tests live in `HomeSpeaker.Server2.Tests` (xUnit): `dotnet test HomeSpeaker.Server2.Tests`. They cover pure logic (push notifications, the Immich photo pipeline, kiosk detection, dashboard models); UI is verified by building and running. App-level tests (endpoints, auth, settings, feature gating) boot the real app through `TestServerFactory` in `HomeSpeaker.Server2.Tests`; reuse it rather than building a host by hand.

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

### Ambient shell and photos

`MainLayout` is a glass-styled shell (`wwwroot/css/glass.css`, components in `Components/Glass`). On kiosk browsers (`body[data-kiosk=true]`: a browser counts as a kiosk on localhost, with the `?kiosk=1` cookie, or automatically on a 1024×600 screen (the server renders `data-kiosk="auto"` and `idleController.js` resolves it); `?kiosk=0` opts out — see `Kiosk.cs`) `wwwroot/js/idleController.js` switches to an ambient photo slideshow after `Ambient:IdleSeconds` idle, or a dim night clock during `QuietHours`. Photos come from an `IPhotoSource` chosen by `PhotoSourceSelector`: `Photos:Source` immich|folder, defaulting to Immich when configured, else the local folder `{MediaFolder}/photos` (`.jpg`/`.jpeg` only, subfolders OK). For Immich, `PhotoSyncWorker` indexes one album (`Immich:*` config) in `{MediaFolder}/.photo-cache/manifest.json` but keeps only `Immich:CacheSize` (default 40) photos on disk, rotating `Immich:RotatePercent` (default 25) of them for fresh ones each sync (videos are not part of that cache: they stream live from Immich, except that `VideoPrefetcher` pre-downloads the videos among the next `Immich:VideoPrefetchAhead` (default 5) slides, found by peeking `PhotoCycle`, to `{MediaFolder}/.photo-cache/videos/`, skipping any over `Immich:VideoPrefetchMaxMB` (default 300; 0 or less disables prefetch), any that would leave under 1 GB free, and any being streamed live, abandoning a download after 30 s with no response or no data, and cancelling it if the video is requested live meanwhile, and deletes each once 10 more slides have been served or it leaves the visible set; `/api/photos/{id}/video` serves a cached file with range support and falls back to the live proxy); `PhotoLibrary` serves it via `/api/photos/*` and `wwwroot/js/ambientSlideshow.js` crossfades it. Overlays and the Home page read live data from the scoped `DashboardState`. Home automation plugs in through `IHomeControl` (default `NullHomeControl`, which hides the Home row).

### REST API

Minimal APIs. The bulk are mapped in `Endpoints/HomeSpeakerRestEndpoints.cs` via `MapHomeSpeakerApi()` (route group `/api/homespeaker`, split into song/player/playlist/queue/youtube/radio/offline-download groups) `Endpoints/AiRestEndpoints.cs` via `MapAiApi()`, the ambient-photo endpoints (`/api/photos/*`) in `Endpoints/PhotoEndpoints.cs` via `MapPhotoApi()`, and play-by-name (`/api/homespeaker/media/match` and `/play`, what Siri's "Play X on Home Speaker" calls; `MediaSearchService` gathers playlists, streams, non-empty AI mixes, artists, albums and songs and `MediaMatcher` scores them) in `Endpoints/MediaSearchEndpoints.cs` via `MapMediaSearchApi()`. A number of standalone endpoints (temperature, blood sugar, forecast, anchors, recently-played, the `/api/music/{songId}` range-aware streaming endpoint, image search/upload) are defined inline in `Program.cs`.

### Background hosted services

Registered in `Program.cs` and run for the process lifetime: `MigrationApplier`, `DailyAnchorWorker`, `AirPlayReceiverService`, `LifecycleEvents`, `AutoPlayMonitorService`, `PhotoSyncWorker`, `VideoPrefetcher`, `PlayerStatusBroadcaster`, `LiveActivityPushWorker`, `HealthAlertWorker`, `AiMusicAnalysisWorker`, and `VolumeMonitorService`. AI playlist analysis is gated by config and signalled through `AiProcessingSignal`.

`PlayerStatusBroadcaster` polls the player (only while something is subscribed) and publishes *material* status changes (song, play state, volume, repeat, sleep timer; not elapsed time). It feeds `GET /api/homespeaker/player/events`, a server-sent-events stream with a 10 s heartbeat, and `LiveActivityPushWorker`, which sends APNs `liveactivity` pushes to iOS Live Activities registered via `PUT/DELETE /api/homespeaker/live-activities/{activityId}` (in memory, 12 h; the app re-registers on foreground). The push `content-state` keys are a contract with `SpeakerActivityAttributes.ContentState` in the iOS app.

### AI integration

`AddChatClient` in `Program.cs` builds an `IChatClient` (Microsoft.Extensions.AI) from the `AI` config section — Azure OpenAI or OpenAI, wrapped with logging + OpenTelemetry decorators. When neither is configured it falls back to `NullChatClient` and AI features quietly disable. AI playlist code lives in `Services/AiMusic*` and `Services/AiPlayback*`.

### Auxiliary widgets

Temperature, blood sugar (Nightscout via `NIGHTSCOUT_URL`), and weather forecast are cached HTTP-backed services with `GET`/refresh/clear-cache endpoints, surfaced as dashboard widgets. "Anchors" are a daily-habit/checklist feature (`AnchorService`, `DailyAnchorWorker`, `AnchorHub` SignalR hub at `/anchorHub`).

### Configuration and settings

Precedence, lowest to highest: `appsettings.json` -> database settings -> environment variables -> command line. The database layer is a configuration provider (`Settings/DatabaseSettingsConfigurationProvider`, SQLite table `AppSettings` in the DB on the media volume), so code just reads `IConfiguration`; an env var, even an empty one (`Admin__PinHash=`), overrides it. `Settings/SettingKeys.cs` is the allow-list of keys the `/settings` page and `/api/settings` may write (secrets are write-only); add a key there when you add an editable setting. `/settings` and `/setup` (first-run wizard, shown from Home until `Forecast` location is set or `Setup:Completed=true`) live under `Pages/Admin`.

**Admin PIN**: optional, 4-12 digits, stored as a PBKDF2 hash in `Admin:PinHash` (`Settings/AdminPin.cs`). With no hash, admin pages are open; with one, the `Admin` policy needs the cookie from `/unlock`. `PinAttemptLimiter` allows 5 wrong tries per minute per IP, then a 60 s lockout (a 4-digit PIN is still brute-forceable in a day or two; docs recommend 6+). Cookie data-protection keys persist in `{MediaFolder}/.keys`. Forgot-PIN: `Admin:ResetPin=true` (env `Admin__ResetPin`) makes `AdminPinReset.Apply` delete the saved hash at startup and log a warning; the user then removes the setting.

**Feature modules** are off unless configured (`FeatureFlags.cs`, surfaced by `GET /api/features`; the Swift client decodes new fields as optionals): Nightscout (`NIGHTSCOUT_URL`), Govee temperature (`Temperature:ApiBaseUrl` + `ApiKey`; rooms from `Temperature:Rooms`, parsed by `TemperatureRoomConfig`, or the settings device picker; `Units:Temperature` F|C), forecast (`Forecast:Latitude/Longitude`), Anchors (`Features:Anchors=true`), iOS push (all four `PushNotifications:Apns:*`). Govee's base URL is `https://openapi.api.govee.com/router/api/v1/`.

Use the constants in `ConfigKeys.cs` (`MediaFolder`, `FFMpegLocation`). Required: `MediaFolder`, `SqliteConnectionString` (set by compose). Other keys: `AI:*`, `Admin:PinHash`, `Admin:ResetPin`, `Photos:Source`, `Immich:*` (BaseUrl, AlbumId, ApiKey also as `IMMICH_API_KEY`, SyncIntervalMinutes, SlideSeconds, VideoPrefetchAhead/MaxMB, ...), `Ambient:IdleSeconds`, `ALSA_CARD`, OTEL exporter vars. In compose, optional settings come from `${HOMESPEAKER_ENV_FILE:-./homespeaker.env}` (needs Compose 2.24+); the maintainer's Pis use `/home/piuser/homespeaker-secrets.env` (never committed; template `homespeaker-secrets.env.example`).

## Deployment

Two paths share one image. **Households** run `install/install.sh` (Compose 2.24+ required): it writes `/opt/homespeaker` (compose file, `.env`, optional `homespeaker.env`), starts the stack, advertises `_homespeaker._tcp` over Avahi (TXT host/port/scheme/path, for iOS discovery) and installs `homespeaker-update.timer`, which runs plain `docker compose pull && up -d` nightly. HTTPS users add `COMPOSE_FILE=docker-compose.yml:docker-compose.https.yml` to that `.env` so the timer keeps the overlay. Containers have fixed `container_name`s, so one stack per host.

**The maintainer** deploys with GitHub Actions. Pushing to `master` triggers `build-and-push.yml`, which builds a multi-arch (linux/arm64 + amd64) image and pushes `ghcr.io/<owner>/homespeaker` (`latest`, `YYYY.M.D-sha`, and tags). On success, `deploy.yml` runs a matrix over the `DEPLOY_TARGETS` repo variable (JSON `[{runner, host, airplay_name}]`) on self-hosted runners, using `TAILNET_DOMAIN`, `DEPLOY_TZ` and `DEPLOY_USER`; it is skipped when `DEPLOY_TARGETS` is unset (forks). It checks Compose 2.24+, renews the Tailscale cert, writes `.env`, pulls, restarts with the HTTPS overlay and refreshes the touchscreen browser. The app image builds on `ghcr.io/<owner>/homespeaker-base` (rebuilt via `build-base-image.yml`, which must exist before the first app build). See `DEPLOYMENT.md`; the one-time move to this pipeline is `docs/owner-migration.md`.

`docker-compose.yml` has the app plus profiles `airplay` (`shairport-sync` sharing ALSA and the PulseAudio socket) and `telemetry` (Aspire dashboard).

## Conventions

- **Naming** (enforced via `.editorconfig` + Workleap/Meziantou analyzers): private fields are `camelCase` **without** an underscore prefix; private methods are often `camelCase`. Match the surrounding file.
- One top-level type per file, named after the type (Meziantou MA0048 is an error in Debug); `private const` names are PascalCase.
- `ImplicitUsings` and `Nullable` are enabled in both projects.
- The `.squad/`, `.copilot/`, and `.github/agents/` directories are remnants of an experimental multi-agent ("squad") workflow that was removed from CI (`5a5ec72`); they are not part of the build and generally should be ignored.
