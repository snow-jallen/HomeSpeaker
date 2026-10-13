# Ambient Shell & Immich Slideshow — Design

**Date:** 2026-10-13
**Status:** Draft — awaiting review
**Mockups:** https://claude.ai/artifact/Dks5LXrMiysouhJWSnVkp8 (design "A · Glass", both ambient and touched boards)

## Goal

Turn each HomeSpeaker Pi (1024×600 touchscreen) into an ambient smart display: when idle it shows a slideshow of family photos from Immich with glanceable overlays (clock, now playing, blood sugar, outside temperature); when touched it shows a glass-styled control screen. The design reserves a slot for home-automation controls (lights, scenes, climate) to be added later.

## Context

- Each Pi runs its own instance; the UI never needs to say which speaker it is ("Kitchen" labels are omitted).
- Primary device: 1024×600 Pi touchscreen in kiosk Chromium. Secondary: occasional phone/laptop access over Tailscale — must stay usable, ambient mode not required there.
- Photos come from one Immich album ("TV"): server `https://immich.boston-cloud.ts.net`, album id `c7fcba71-bdc5-4de0-9511-aa45ea22959f`.

## Decomposition

The user chose a full shell replacement. That is split into sub-projects, each with its own spec → plan → implementation:

1. **Foundation (this spec):** new glass shell, idle/ambient/night modes, Immich sync + cache, new Home (controls) page, home-automation slot (null implementation). Existing pages keep working inside the new shell, unrestyled.
2. **Music pages restyle:** Music, Queue, Playlists, Folders, Streams, YouTube, RecentlyPlayed, AI playlist pages.
3. **Admin/health restyle + cleanup:** Anchors, AnchorsEdit, Autoplay, NightScout, AspireDashboard; delete `Pages/Demo/*`; drop Bootswatch/FluentUI/Font Awesome once unused.
4. **Home-automation provider** (e.g., Home Assistant) implementing `IHomeControl`.

## 1. Shell and screen modes

### Shell

- New `MainLayout` replaces the sidebar (`NavMenu`), bottom nav, and mobile menu overlay (all deleted).
- **Top bar:** left control, menu button, clock on the right. On the Home page the left control is “Photos” (enters Ambient; kiosk only, hidden elsewhere); on other pages it is “‹ Home”.
- **Navigation:** the Home page is the hub; its tiles link into existing pages. A menu button opens `AppMenu`, a glass sheet listing every page (Music, Queue, Streams, Playlists, Folders, Recently played, YouTube, AI Playlists, AI Status, Autoplay, Anchors, NightScout, Climate, Aspire), replacing `NavMenu` in `MainLayout`. (`NoTopLayout` still uses `NavMenu`; it is restyled in sub-project 3.) Unrestyled pages render inside a glass content panel (`.glass-panel`, scrollable) so they remain functional.
- **Background:** a blurred, darkened copy of the current photo sits behind the shell on every page (static fallback color when no photos are available).
- **Design tokens** in new `wwwroot/css/glass.css`: colors (`--glass-bg`, `--glass-border`, `--text`, `--text-muted`, glucose state colors), radii (`--radius-card`, `--radius-tile`, `--radius-pill`), type scale. Font: Manrope (Google Fonts), replacing Inter/Poppins for new components.
- Keep loading Bootswatch, MudBlazor, FluentUI, Font Awesome for now (existing pages depend on them). MudBlazor providers stay in the layout.
- **Sizing:** new glass components size text and spacing in `px`, not `rem`, because `app.css` sets the root font size to 24px at exactly 1024×600 for the legacy pages.
- **Responsive:** below 700px width the Home page reflows to one column; top bar stays.
- Preserve existing layout behaviors: keyboard shortcuts (`homeSpeakerKeyboard.init` + `[JSInvokable]` methods), circuit heartbeat, kiosk circuit watchdog in `App.razor`.

### Screen modes

A JS module `wwwroot/js/idleController.js` (evolved from `sleepyTime.js`, which is removed) owns three states:

| State | Shown | Entered when |
|---|---|---|
| `Active` | shell + current page | any pointerdown / keydown |
| `Ambient` | photo slideshow + overlays | kiosk device, idle ≥ `Ambient:IdleSeconds` (default 120) outside quiet hours |
| `Night` | dim night clock + glucose on black | kiosk device, idle ≥ 60s during quiet hours |

- Quiet-hours status comes from the server (`QuietHours.IsQuietTime`); the layout passes it to the JS module and refreshes it on the existing 60s layout timer.
- Ambient and Night layers are always in the DOM above the shell and toggled by CSS class, so waking is instant with no navigation.
- The tap that wakes the screen is swallowed (capture-phase handler, `preventDefault` + `stopPropagation`) so it does not activate the control underneath.
- When entering Ambient/Night from any page, the app stays on that page; waking returns to it.
- **Kiosk detection** (server-side, emitted as `<body data-kiosk="true|false">`): a request is a kiosk when `?kiosk=1` is present, else when the `hs-kiosk` cookie is `1`, else when the request host is `localhost`/`127.0.0.1`/`::1`. `?kiosk=1` / `?kiosk=0` set the cookie to `1` / `0` (10-year expiry), so `?kiosk=0` also opts a localhost browser out. Non-kiosk devices never enter Ambient or Night.
- Night mode replaces the current `.sleepy-overlay`. The backlight stub (`ToggleBrightness`) is unchanged.

## 2. Immich sync, photo cache, endpoints

### Configuration

New section `Immich`, bound with `Configure<ImmichOptions>(GetSection("Immich"))` (same pattern as `AiMusicOptions`):

| Key | Default | Notes |
|---|---|---|
| `Immich:BaseUrl` | — | e.g. `https://immich.boston-cloud.ts.net`. If empty, photo features disable silently. |
| `Immich:ApiKey` | — | Secret; lives in `homespeaker-secrets.env`, never committed. Add placeholder to `homespeaker-secrets.env.example`. |
| `Immich:AlbumId` | — | `c7fcba71-bdc5-4de0-9511-aa45ea22959f` |
| `Immich:SyncIntervalMinutes` | 15 | |
| `Immich:SlideSeconds` | 30 | |

Also `Ambient:IdleSeconds` (default 120).

Cache directory: `{MediaFolder}/.photo-cache` (on the media volume, so it survives container restarts). The leading dot keeps it out of normal browsing; `Mp3Library` only watches `*.mp3`, so no interaction.

### ImmichClient

Typed `HttpClient` (registered like `ForecastService`, 30s timeout) sending `x-api-key`. Operations:

- `GetAlbumImagesAsync(albumId)` → `POST /api/search/metadata` with `{ albumIds: [albumId], type: "IMAGE", withExif: true, size: 250, page: n }`, following `assets.nextPage` until null; trashed assets skipped. Returns id, `localDateTime`, EXIF city/state/country. (Current Immich album responses no longer embed assets, so the album endpoint is not used.)
- `DownloadPreviewAsync(assetId, destination)` → `GET /api/assets/{id}/thumbnail?size=preview` (~1440px long edge, already orientation-corrected).
- `GetFacesAsync(assetId)` → `GET /api/faces?id={id}` → bounding boxes (`boundingBoxX1..Y2`) with the `imageWidth`/`imageHeight` they are relative to.

Verified against the Immich OpenAPI spec (v3.3); all calls send the `x-api-key` header.

### PhotoSyncWorker (hosted service)

Runs at startup, then every `SyncIntervalMinutes`:

1. Fetch album asset list.
2. Diff against `manifest.json` in the cache directory.
3. Download new previews to `{assetId}.jpg` (write to temp file, then rename, so a partial download never appears in the cache).
4. For new assets, fetch faces and store normalized boxes (0–1 coordinates).
5. Delete cache entries whose assets left the album.
6. Write the manifest atomically (temp + rename) and notify `PhotoLibrary` to reload.

Failure handling: any error logs a warning and leaves the existing cache and manifest untouched. Per-asset failures skip that asset and continue. Disabled entirely when `Immich:BaseUrl` is empty.

Manifest entry: `AssetId`, `TakenAt`, `City`, `State`, `Country`, `ImageWidth`, `ImageHeight` (dimensions of the image the face boxes were measured on; 0 when no faces), `Faces[]` (normalized `X1,Y1,X2,Y2`).

### PhotoLibrary (singleton)

- Loads the manifest at startup and after each sync.
- Serves photos in a shuffled cycle: no repeats until every photo has been shown; reshuffles at the end of a cycle and after a sync changes the set.
- Excludes ids in the local hide list (`hidden.json` in the cache directory).
- Computes the caption: place (`City, State` or `City, Country`, whichever is present) and date (`August 2021 · 5 years ago`; just `October 2026` when less than a year ago; `· 1 year ago` singular).
- Computes the **overlay layout**: chooses whichever of two layout variants overlaps face boxes the least (overlap measured against the overlay regions: clock, chips, now-playing panel), accounting for `object-fit: cover` cropping at 1024×600. Two variants: *default* (clock top-left, chips top-right, now-playing bottom-left, caption bottom-right) and *mirrored* (left/right swapped). Default when no faces.

### Endpoints

| Method | Route | Purpose |
|---|---|---|
| GET | `/api/photos/next` | `{ id, url, caption, place, layout: "default"\|"mirrored" }`; `204` when no photos |
| GET | `/api/photos/{id}.jpg` | Image from cache with `Cache-Control: public, max-age=31536000, immutable`; `404` if missing |
| POST | `/api/photos/{id}/hide` | Add to local hide list (Immich untouched) |
| POST | `/api/photos/sync` | Trigger an immediate sync (admin convenience) |

`/api/features` gains `photos: bool` (true when Immich is configured and the cache has at least one photo).

### Client slideshow

`wwwroot/js/ambientSlideshow.js`: two stacked `<img>` layers. While one shows, it requests `/api/photos/next`, preloads the image (`img.decode()`), then crossfades over 1.5s and applies the returned `layout` class to the overlay container during the fade. Only advances while in Ambient state (pauses in Active/Night to save the Pi's CPU). On fetch failure it keeps the current photo and retries on the next tick. It also reports the current photo URL so the shell's blurred background can use it.

## 3. Overlays, Home page, home-automation slot

### DashboardState (scoped service)

Single source of live data for the overlay, night clock, and Home page, replacing the separate timers in `Index.razor`, `BloodSugarMonitor`, and `TemperatureMonitor` (those components keep working on their own pages but the new components don't use them):

- Player status: subscribes to `HomeSpeakerService.StatusChanged` for immediate updates, with 5s polling as fallback; writes through `PlayerStateService` as today.
- Blood sugar (`BloodSugarService`), temperature (`TemperatureService`), and forecast (`ForecastService`), refreshed every 60s (services already cache).
- Feature flags (blood sugar / temperature / photos) loaded once.
- Raises `Changed`; disposes timers and unsubscribes with the circuit.

### AmbientOverlay.razor

Matches mockup A (no room label):

- Clock (large) and date, top-left.
- Glucose chip: value, trend arrow (`DirectionIcon`), age; chip tint follows `BloodSugarStatus.StatusColor` (in range = neutral glass; high = amber; low = red). Stale readings show the age prominently and dim the value.
- Temperature chip: outside temperature, today's high / tonight's low, conditions.
- Now-playing panel: art from `/api/homespeaker/songs/{id}/art`, title, artist/album, progress bar. Hidden when nothing is playing.
- Photo caption (place, date), bottom-right.
- Each chip hidden when its feature is disabled. With no photos, the overlay sits on a dark static background.

### NightClock.razor

Black background; large dim warm-red clock (≤ ~20% brightness, red/orange hues only); glucose value + arrow beneath, tinted only when out of range. No photo, no now-playing.

### Home page (`Pages/Index.razor`, rebuilt)

Layout per mockup A "touched" (without room picker):

- **Now-playing card (left, ~420px):** art, title, artist, "Up next", scrub bar with elapsed/remaining, shuffle / play-stop / next / repeat / queue buttons, volume slider. Wired to existing `HomeSpeakerService` methods (`ShuffleQueueAsync`, `StopPlayingAsync`/`ResumePlayAsync`, `SkipToNextAsync`, `SetRepeatModeAsync`, `SetVolumeAsync`). There is no "previous" (the player has no history) and the scrub bar is display-only (no seek support). Streams show the station name and a radio glyph instead of art. Queue button navigates to `/queue`.
- **Source tiles:** Playlists (`/playlists`), Radio (`/streams`), AI Mix (`/ai-playlists`), Library (`/music`).
- **Glucose and weather tiles:** glucose links to `/nightscout`; weather links to a new `/climate` page that hosts the existing `TemperatureMonitor` (room temperatures, close-the-windows advice) and `ForecastMonitor`, so nothing the current home page shows is lost.
- **Home row:** `HomeTileRow` (below). When unavailable, the row is not rendered and the tiles above expand to fill.
- Touch targets ≥ 48px. Below 700px: single column.

### Home-automation slot

```csharp
public interface IHomeControl
{
    bool IsAvailable { get; }
    Task<IReadOnlyList<HomeTile>> GetTilesAsync(CancellationToken ct = default);
    Task InvokeAsync(string tileId, HomeAction action, CancellationToken ct = default);
    event Action? TilesChanged;
}

public record HomeTile(string Id, string Name, HomeTileKind Kind, string? State);
public enum HomeTileKind { Light, Scene, Climate }
public enum HomeAction { Toggle, TurnOn, TurnOff, Activate }
```

- `NullHomeControl` (`IsAvailable = false`, empty list) registered as the default singleton.
- `HomeTileRow.razor` renders tiles from `IHomeControl`, re-renders on `TilesChanged`.
- Adding a provider later = one class + changing the registration line.

## 4. Error handling summary

| Failure | Behavior |
|---|---|
| Immich not configured | No sync; `photos=false`; ambient shows overlays on dark background |
| Immich/Tailscale down | Sync logs warning, cache unchanged; slideshow continues |
| Album emptied | Cache emptied on next sync; ambient falls back to dark background |
| Corrupt/missing manifest | Treated as empty; next sync rebuilds |
| `/api/photos/next` fails in browser | Keep current photo; retry next tick |
| Blood sugar / temperature unavailable | Chip hidden (feature off) or shows stale/error state as the services already report |
| Circuit disconnect | Existing kiosk watchdog reloads; JS slideshow keeps cycling meanwhile |

## 5. Testing

Unit tests in the existing `HomeSpeaker.Server2.Tests` xUnit project cover the pure logic: Immich client request/paging, cache store, sync diff, photo cycle, caption, overlay layout choice, kiosk detection, glucose tone. Everything else is verified by building (Debug, warnings-as-errors) and running locally:

1. Immich configured against the TV album: initial sync populates cache; slideshow cycles with crossfades; captions correct; overlays flip to mirrored layout on photos with faces on the left.
2. No `Immich` config: app starts cleanly, ambient shows overlays on dark background.
3. Immich unreachable after a successful sync (bad BaseUrl): warning logged, slideshow continues from cache.
4. Remove a photo from the album → it disappears after the next sync (`POST /api/photos/sync`).
5. Idle → Ambient after `IdleSeconds`; tap wakes without triggering the control underneath; quiet hours → Night clock.
6. From a non-localhost host without `?kiosk=1`: never enters Ambient/Night; `?kiosk=0` on localhost opts out.
7. Layout at 1024×600 and ~390px phone width; existing pages still usable inside the glass panel.
8. Home page controls: play/pause, next/previous, volume, queue navigation all work.

## 6. Deployment notes

- Add `Immich__BaseUrl`, `Immich__ApiKey`, `Immich__AlbumId` to `/home/piuser/homespeaker-secrets.env` on each Pi; add placeholders to `homespeaker-secrets.env.example`.
- Kiosks loading `https://localhost` are detected automatically; a kiosk loading a Tailscale hostname needs `?kiosk=1` once (cookie persists).
- The `.photo-cache` folder lives on the existing media volume; no compose changes needed.

## Out of scope

- Restyling pages other than Home (sub-projects 2–3).
- Any real home-automation provider (sub-project 4).
- Multiple albums, people filters, Immich memories, per-Pi photo settings UI.
- Syncing slideshow position across Pis.
- Hardware backlight dimming.
