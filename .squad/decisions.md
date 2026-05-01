# Squad Decisions

## Active Decisions

### 20260323104717: User Directive — Touch-First Design
**By:** Jonathan Allen (via Copilot)  
**Status:** Active

Primary interface is a 7" Raspberry Pi touch screen. Optimize UI primarily for that. Secondary: mobile phones. Tertiary: desktop. Touch-first, large tap targets, finger-friendly controls, no hover-only interactions.

---

### Theme Selection — Darkly Dark Theme
**Date:** 2025-03-23  
**Author:** Kaylee (Frontend Dev)  
**Status:** Implemented

Switched from Sandstone (light, neutral) to **Darkly** (dark theme). Music players traditionally use dark interfaces — reduces eye strain during evening listening, feels more atmospheric, common in Spotify/Apple Music/YouTube Music. Better for OLED screens. Provides better contrast for colorful album art.

**Color Palette:**
- Primary: `#1DB954` (Spotify green) — interactive elements, active states
- Secondary: `#535bf2` (Purple) — highlights, secondary actions
- Accent: `#ff6b6b` (Coral) — warnings, delete actions
- Backgrounds: `#121212` → `#282828` (hierarchy)

**Typography:** Inter (body) + Poppins (headings) via Google Fonts.

---

### Layout: Bottom Navigation for Mobile/RPi
**Date:** 2025-03-23  
**Author:** Kaylee (Frontend Dev)  
**Status:** Implemented

Converted sidebar navigation to bottom navigation bar for screens <1024px wide. On 800×480 landscape, sidebar wastes 25% horizontal space. Bottom nav is standard mobile pattern.

**Implementation:**
- Bottom bar: 70px tall with 56px tap targets (5 items: Library, Queue, Folders, Streams, More)
- Desktop (≥1024px): Traditional sidebar layout maintained
- Desktop breakpoint: 1024px

---

### Touch Target Standards — WCAG AAA Compliance
**Date:** 2025-03-23  
**Author:** Kaylee (Frontend Dev)  
**Status:** Implemented

Enforced strict minimum sizes across all interactive elements:
- Standard buttons/links: 44×44px minimum (WCAG AAA)
- Player icon buttons: 56×56px (primary actions)
- Play/pause button: 80×80px desktop, 72×72px RPi
- Nav items: 56px min-height
- List items: 56px min-height
- Volume slider: 40px track height, 44px thumb (up from 6px/16px)

Rationale: Larger targets reduce tap errors and improve user confidence on touch devices.

---

### Touch-Specific Interactions
**Date:** 2025-03-23  
**Author:** Kaylee (Frontend Dev)  
**Status:** Implemented

Replaced hover-based interactions with active states and touch optimizations:
- All elements: `touch-action: manipulation` (prevents 300ms tap delay)
- Active states: scale(0.97) + opacity 0.85 for immediate feedback
- Removed all :hover-only features
- Added `-webkit-overflow-scrolling: touch` for momentum scrolling
- Added `overscroll-behavior: contain` to prevent body scroll during touch

Rationale: Hover states don't exist on touch screens. Active states provide instant visual feedback that a tap registered.

---

### Typography Minimum Size
**Date:** 2025-03-23  
**Author:** Kaylee (Frontend Dev)  
**Status:** Implemented

No text smaller than 14px anywhere. Base font remains 16px.

Rationale: 7-inch screen at arm's length (30-50cm) requires readable text. 14px is minimum for comfortable reading without strain.

---

### Feature Implementation: Repeat Mode
**Date:** 2025-03-23  
**Author:** Mal (Architect)  
**Status:** Implemented

Toggle to loop the current song indefinitely. Added `RepeatMode` property to `IMusicPlayer` interface. After song ends, if RepeatMode = true and queue is empty, replay last song. Both `WindowsMusicPlayer` and `LinuxSoxMusicPlayer` updated. gRPC methods: `SetRepeatMode`, `GetRepeatMode`. UI: New repeat button in `PlayControls.razor` (yellow when active).

---

### Feature Implementation: Sleep Timer
**Date:** 2025-03-23  
**Author:** Mal (Architect)  
**Status:** Implemented

Schedule automatic stop after N minutes (15, 30, 45, 60, 90, 120 min). Added `SetSleepTimer(minutes)`, `CancelSleepTimer()` to `IMusicPlayer`. Uses `CancellationTokenSource` + `Task.Delay` for clean async timer. After expiration: stops playback, clears queue. gRPC methods: `SetSleepTimer`, `CancelSleepTimer`, `GetSleepTimer`. UI: Dropdown menu in `PlayControls.razor` (moon icon, shows remaining time).

---

### Feature Implementation: Recently Played History
**Date:** 2025-03-23  
**Author:** Mal (Architect)  
**Status:** Implemented

View history of last 20 songs played. Leverages existing `Impressions` table in database. Auto-tracking added via event handler in `HomeSpeakerService`. API endpoint: `GET /api/music/recently-played?limit=20`. New page at `/recently-played`. Nav menu: Added "Recently Played" link (clock icon).

---

### Feature Implementation: Keyboard Shortcuts
**Date:** 2025-03-23  
**Author:** Mal (Architect)  
**Status:** Implemented

Control playback without clicking. Shortcuts:
- **Space** = Play/Pause toggle
- **Right Arrow** = Skip forward
- **Left Arrow** = Previous/restart
- **Up Arrow** = Volume up (+5%)
- **Down Arrow** = Volume down (-5%)
- **S** = Stop
- **R** = Toggle repeat mode

Implementation: JavaScript `keyboard.js` with global event listener (ignores typing in input fields). Blazor interop: `MainLayout.razor` receives JS callbacks via `[JSInvokable]` methods. Works globally across all pages.

---

### Browser Auto-Refresh Strategy for Kiosk Deployments
**Date:** 2026-03-24  
**Author:** Wash (Backend Dev)  
**Status:** Implemented

The GitHub Actions deploy workflow now uses a **multi-strategy fallback approach** for refreshing the kiosk-mode Chromium browser on Raspberry Pi runners.

**Problem:** Previous `xdotool key F5` calls failed silently due to X11 permission issues — the self-hosted runner (service user) couldn't access the X display owned by the desktop session user.

**Solution:**
1. **Strategy 1 (Primary):** Chrome Remote Debugging Protocol — HTTP POST to `localhost:9222` to trigger `location.reload()`
2. **Strategy 2 (Secondary):** xdotool with discovered XAUTHORITY — searches `/home/piuser` and `/run/user` for `.Xauthority` file
3. **Strategy 3 (Fallback):** xdotool with hardcoded path — tries `/home/piuser/.Xauthority` directly

**Supporting Changes:**
- Enhanced service readiness polling: 12 attempts × 5s (curl to `https://localhost/`)
- Removed `continue-on-error: true` — failures now visible in GitHub Actions logs
- Detailed logging for each strategy attempt
- Exit code 1 if all strategies fail

**One-Time Pi Setup (Recommended):**
Add `--remote-debugging-port=9222` to Chromium launch command to enable Strategy 1 (most reliable).

**Implementation File:** `.github/workflows/deploy.yml`

---

### Home Page — Remove Quick-Links, Compact Now Playing
**Date:** 2026-03-24  
**Author:** Kaylee (Frontend Dev)  
**Status:** Implemented

Optimized home page layout for RPi 7" touch screen (800×480 landscape).

**Decision 1: Remove Quick-Access Nav Buttons**
Removed redundant 4-button quick-link grid (Music, Queue, Playlists, Streams). Navigation is always available via sidebar (≥992px) or bottom nav (<992px). On 480px height-constrained screen, redundant UI consumed space needed for health info.

**Decision 2: Compact Now Playing Card**
Reduced vertical footprint of Now Playing section to prioritize health data displays.

**Changes (scoped to Index.razor local styles):**
- Card padding: `var(--hs-space-lg)` → `var(--hs-space-md)`
- Status section min-height: 80px → 56px
- Song title: 1.4rem → 1.1rem (1rem on <600px)
- Artist: 1rem → 0.875rem (≈14px)
- Album: 0.875rem → 0.8rem (≈12.8px, acceptable for tertiary)
- Idle icon: 2.5rem → 1.75rem
- Progress track: 4px → 3px height
- All margins/padding-tops tightened from `var(--hs-space-md)` to `var(--hs-space-sm)`

**Touch Targets Preserved:** PlayControls buttons remain ≥44px; no button sizing changed.

**Component Isolation:** Changes scoped to Index.razor only — no impact on PlayControls when used in sidebar.

**Implementation File:** `Pages/Index.razor`

---

## Governance

- All meaningful changes require team consensus
- Document architectural decisions here
- Keep history focused on work, decisions focused on direction


## AI Playlists Discovery — 2026-05-01

Cross-platform discovery wave output. Decisions locked for implementation.

---


**Date:** 2026-05-01  
**Author:** Mal  
**Status:** Proposed for implementation

## Call

Keep this in **HomeSpeaker.Server2**. No new service, no vector database, no client-side AI.  
Use OpenAI through the **Microsoft.Extensions.AI** abstraction, run library analysis in a **resumable background worker**, and persist durable AI facts in the existing SQLite database.

That gets us:
- full-library enrichment
- resume across restarts
- new-track pickup
- per-genre AI playlists
- similarity-based autoplay
- thumbs feedback that actually changes future picks

## Why this is the simplest sound architecture

1. **The server already owns the library, database, and playback state.** AI belongs there.
2. **SongId is not durable.** `OnDiskDataStore` reassigns it during every library sync, so all AI persistence must key on `SongPath`.
3. **We do not need Qdrant/Aspire/AppHost ceremony for this feature.** The user asked for composable-stack patterns, not a science project. `IChatClient` + EF Core + hosted worker is enough.
4. **Do not write AI playlists into the existing user playlist tables.** User playlists are authored content; AI playlists are generated catalog views and should stay separate.

## Recommended server shape

Add these server-side components:

### 1. `AiMusicOptions`
Bound from configuration under `AI`.

Owns:
- `OpenAI:ApiKey`
- `OpenAI:ChatModel`
- `Processing:Enabled`
- `Processing:BatchSize`
- `Processing:MaxParallelBatches`
- `Processing:ScanIntervalMinutes`
- `Processing:StaleLeaseMinutes`
- `AnalysisVersion`

### 2. `AiMusicCatalogService`
Read/query service for:
- genre playlist summaries
- genre playlist contents
- similar-song lookups
- processing status

### 3. `AiMusicAnalysisWorker`
`BackgroundService` that:
- scans `Mp3Library.Songs`
- upserts pending work items for new/changed tracks
- claims work in small batches
- calls OpenAI once per batch for structured analysis
- saves per-song markers, genre memberships, and similarity edges
- can recover abandoned batches after restart

### 4. `AiPlaybackService`
Owns AI playback sessions:
- start genre playlist mode
- start “play something similar” mode
- choose next track from stored similarity + genre scores
- apply thumbs up/down feedback bias

## Persistence model

Use EF Core tables in `MusicContext`. Key all song-linked rows by **SongPath**.

### `AiGenreDefinition`
Seeded catalog of 15 genres:
1. peaceful-instrumental
2. quiet-sunday
3. driving-tunes
4. choral
5. upbeat-a-cappella
6. country
7. quiet-classical
8. church-christmas
9. hymns
10. classical-christmas
11. vocal-christmas
12. worship-ensemble
13. reflective-piano
14. family-singalong
15. warm-folk-acoustic

Fields:
- `Key`
- `DisplayName`
- `Description`
- `SortOrder`
- `IsActive`

### `AiTrackProfile`
One durable row per analyzed song.

Fields:
- `SongPath` (PK)
- `Fingerprint` (path + file length + last write time + optional tag snapshot)
- `AnalysisVersion`
- `Status` (`Pending`, `Processing`, `Completed`, `Failed`)
- `Attempts`
- `LastError`
- `LastAnalyzedUtc`
- `Summary`
- `TempoLabel`
- `PrimaryMood`
- `Energy`
- `Acousticness`
- `Instrumentalness`
- `VocalPresence`
- `Sacredness`
- `SeasonalityChristmas`
- `Danceability`
- `Warmth`
- `Confidence`

### `AiTrackMarker`
Queryable flexible markers so we are not repainting the schema every time we add one.

Fields:
- `Id`
- `SongPath`
- `MarkerKey`
- `MarkerValue`
- `Confidence`

Examples:
- `mood.peaceful`
- `vibe.driving`
- `style.choral`
- `season.christmas`
- `context.church`

### `AiTrackGenreScore`
Many-to-many song/genre membership. A song can land in several genres.

Fields:
- `SongPath`
- `GenreKey`
- `Score`
- `Rank`
- `Why`

### `AiTrackSimilarity`
Top-N nearest neighbors per song, computed on the server from stored markers after each batch.

Fields:
- `SongPath`
- `SimilarSongPath`
- `Score`
- `ReasonsJson`
- `UpdatedUtc`

Store maybe the top 20-40 neighbors per song. Enough for autoplay. No need for a vector store yet.

### `AiProcessingWorkItem`
Resumable queue.

Fields:
- `Id`
- `SongPath`
- `Fingerprint`
- `Status` (`Pending`, `Processing`, `Completed`, `Failed`)
- `BatchId`
- `LeaseExpiresUtc`
- `Attempts`
- `QueuedUtc`
- `StartedUtc`
- `CompletedUtc`
- `LastError`

### `AiProcessingRun`
Cheap aggregate row for the status page.

Fields:
- `Id`
- `State` (`Idle`, `Scanning`, `Processing`, `Degraded`)
- `TotalTracks`
- `QueuedTracks`
- `ProcessingTracks`
- `CompletedTracks`
- `FailedTracks`
- `CurrentBatchId`
- `LastHeartbeatUtc`
- `LastScanUtc`

### `AiPlaybackSession`
Tracks when the user is in AI mode.

Fields:
- `SessionId`
- `Mode` (`Genre`, `Similar`)
- `GenreKey`
- `SeedSongPath`
- `StartedUtc`
- `LastAdvancedUtc`
- `IsActive`

### `AiPlaybackFeedback`
Thumbs events. Keep the raw signal.

Fields:
- `Id`
- `SessionId`
- `SongPath`
- `Feedback` (`Up`, `Down`)
- `PreviousSongPath`
- `GenreKey`
- `CreatedUtc`

## Resumable processing approach

Use a **claim/lease** queue. Anything else is fragile.

Flow:
1. Periodic scan compares current library songs against `AiTrackProfile` by `SongPath + Fingerprint + AnalysisVersion`.
2. Missing or changed songs get/keep a `Pending` work item.
3. Worker claims up to `BatchSize` pending rows by setting `Status=Processing`, `BatchId`, `LeaseExpiresUtc`.
4. Worker sends the batch to OpenAI and requires strict JSON output.
5. Save all song results transactionally.
6. Mark work items `Completed`.
7. On startup, any expired `Processing` lease goes back to `Pending`.

That gives restart safety, retry safety, and new-track pickup without manual babysitting.

## AI analysis contract

Do **batch analysis**, not one API call per song.

Batch input per song:
- `SongPath`
- title
- artist
- album
- optional folder hints

Expected structured output per song:
- short description
- normalized marker scores
- 0..N genre scores from the seeded genre list
- optional notes for pairing/similarity

Then compute similarity locally from marker vectors and feedback. Do not ask the model for pairwise song-vs-song comparisons. That would be expensive nonsense.

## API surface

Add a separate `/api/ai` surface. Leave `/api/homespeaker/playlists` alone.

### Status
- `GET /api/ai/status`
  - returns processing counts, current state, last scan, percent complete, failed count
- `POST /api/ai/process/resume`
  - manual nudge; safe if already running

### Genre playlists
- `GET /api/ai/playlists`
  - returns the seeded genre list with track counts and freshness
- `GET /api/ai/playlists/{genreKey}`
  - returns playlist metadata + songs
- `POST /api/ai/playlists/{genreKey}/play`
  - starts AI genre playback session and queues songs

### Similar autoplay
- `GET /api/ai/similar/{songId}`
  - returns best matches for the current song
- `POST /api/ai/autoplay/from-current`
  - starts similar-song mode from the currently playing track

### Feedback
- `POST /api/ai/feedback`
  - body: `sessionId`, `songId`, `feedback`

### Player contract extension
Extend the existing player status payload with nullable AI context instead of inventing a second polling endpoint for now:

- `aiContext.mode`
- `aiContext.sessionId`
- `aiContext.genreKey`
- `aiContext.seedSongId`
- `aiContext.allowFeedback`

That lets both Blazor and iOS light up thumbs buttons from the same polling loop they already use.

## Client-facing contract

Do **not** mutate the base `Song` shape just to smuggle AI state everywhere.

Use:
- existing `Song`
- new `AiPlaylistSummaryDto`
- new `AiPlaylistDto`
- new `AiLibraryStatusDto`
- new `AiFeedbackRequest`
- new `AiPlayerContextDto`

That keeps the mobile client changes surgical.

## Configuration call

Follow the composable-stack pattern, but keep it lean:

### `Program.cs`
- bind `AI` options from `IConfiguration`
- register OpenAI via `Microsoft.Extensions.AI` / `Microsoft.Extensions.AI.OpenAI`
- register `IChatClient` with logging + OpenTelemetry middleware
- register `AiMusicCatalogService`, `AiPlaybackService`, and `AiMusicAnalysisWorker`

### `appsettings.json`
Keep only non-secret defaults:

```json
"AI": {
  "OpenAI": {
    "ChatModel": "gpt-4o-mini"
  },
  "Processing": {
    "Enabled": true,
    "BatchSize": 12,
    "MaxParallelBatches": 1,
    "ScanIntervalMinutes": 30,
    "StaleLeaseMinutes": 10
  },
  "AnalysisVersion": "2026-05-01-v1"
}
```

### Secret
`AI:OpenAI:ApiKey` comes from user secrets / env vars / other `IConfiguration` providers.  
Do not hardcode it. Do not build a separate secrets system.

## UI call

### Blazor
- add nav item: **AI Playlists**
- new page: `/ai-playlists`
- new page: `/ai-status`
- show thumbs up/down only when `PlayerStatus.AiContext.AllowFeedback == true`

### iOS
- add **AI Playlists** destination
- add **AI Status** page
- same thumbs logic from `PlayerStatus.aiContext`

The Blazor app gets the primary nav item. The iOS app can expose AI Playlists from its main navigation without pretending it has the same layout model as the web app.

## Implementation slices

### Wash
Backend only.
- Add EF entities + migration in `MusicContext`
- Add `AiMusicOptions`
- Add `IChatClient` registration in `Program.cs`
- Add `AiMusicCatalogService`, `AiPlaybackService`, `AiMusicAnalysisWorker`
- Add `/api/ai/*` endpoints
- Extend player status contract with nullable AI context
- Persist feedback and bias next-track selection

### Kaylee
Blazor UI only.
- `NavMenu.razor`: add **AI Playlists**
- build `/ai-playlists` and `/ai-status`
- add AI status cards/progress UI
- add thumbs up/down component in current-player surface
- keep touch-first sizing from active squad decisions

### River
iOS only.
- extend `APIClient.swift` and shared Swift models for `/api/ai/*`
- add AI Playlists screen
- add AI Status screen
- surface thumbs feedback on now-playing when `aiContext.allowFeedback`
- keep device-local playback separate; AI mode applies to server playback

### Zoe
Validation only.
- restart-resume test: kill server mid-batch, restart, confirm queue recovers
- new-track test: add files, rescan, confirm only new/changed tracks queue
- multi-genre test: verify one song can appear in several AI playlists
- feedback test: repeated thumbs down should suppress similar picks
- cross-client parity test: Blazor and iOS show same counts/status/AI mode flags
- degraded-config test: missing API key should leave feature visible but status = degraded, not crash startup

## Non-goals for the first pass

- no external vector database
- no agent framework
- no live SignalR status requirement unless polling proves too ugly
- no client-side model calls
- no rewriting existing user playlists into AI-managed data

That’s the line. Ship the feature, not a platform.


---


**Decision ID:** 20260324-001  
**Date:** 2026-03-24  
**Author:** Wash (Backend Developer / Security Analyst)  
**Status:** PROPOSAL (awaiting team approval)  
**Priority:** HIGH (blocks implementation phase)

---

## Problem Statement

HomeSpeaker needs AI-powered music features:
1. Genre classification per track
2. Resumable batch processing (handles service interruption)
3. Song similarity markers (track relationships)
4. AI-generated playlists (auto-curated)
5. Batch progress/status visibility

Current architecture has no persistence layer for AI metadata, no batch job tracking, and no similarity data structures.

---

## Proposed Solution (Summary)

**5 new EF Core entities** to persist AI results + track batch progress:
- `SongMetadataEntity` — genre, energy, acousticness, danceability per track
- `SongSimilarityEntity` — pre-computed similarity scores between track pairs
- `AiPlaylistEntity` + `AiPlaylistItemEntity` — immutable AI-generated playlists
- `ClassificationBatchEntity` — resumable batch job progress with checkpoints

**3 new services:**
- `AIClassificationService` — single-song and batch classification with resume capability
- `AISimilarityService` — similarity computation and AI playlist generation
- `AIBackgroundProcessor` (HostedService) — runs batches asynchronously, handles restart

**1 database migration** (adds 5 tables + indexes)

**10+ new REST endpoints** across 3 groups (classification, similarity, AI playlists)

---

## Key Decisions

### 1. Similarity Storage (Explicit vs. Implicit)

**Decision:** Pre-compute and store all similarity pairs in `SongSimilarityEntity`.

**Rationale:**
- ✅ O(1) lookup at runtime (query by SongPathA, SongPathB)
- ✅ Enables fast "similar songs" endpoint
- ✅ Enables fast "AI playlist generation" (pick top N by score)
- ❌ Storage cost: 5k songs = 25M pairs (~2.5 GB SQLite)
- ❌ Computation cost: O(n²) batched background job (hours for large library)

**Alternative Considered:** Compute similarity on-demand (no storage).
- ✅ Zero storage overhead
- ❌ Slow queries (O(n) per request, unsuitable for Raspberry Pi)
- ❌ Violates "responsive UI" requirement

**Decision:** Explicit pre-computed storage. Background job acceptable; real-time queries required.

---

### 2. Batch Resumability

**Decision:** Use `ClassificationBatchEntity.LastCheckpoint` (SongPath) to enable resume from last-processed song.

**How it works:**
1. Start batch → Create `ClassificationBatchEntity(Status='pending')`
2. Process songs 1-100 → Update `ProcessedSongs=100, LastCheckpoint='path/to/song100.mp3'`
3. Service crashes after song 105
4. On restart → Query batch with Status='in_progress', find LastCheckpoint
5. Resume endpoint → Skip songs 1-105, process songs 106+ from same batch

**Rationale:**
- ✅ Handles service interruption gracefully (no lost work)
- ✅ Idempotent (re-processing same song twice is OK for classification)
- ✅ Simpler than distributed queue (no external dependency)
- ✅ Works with SQLite (no need for Redis/RabbitMQ)

**Alternative Considered:** Offset-based pagination.
- ❌ Fragile (song list order changes if library modified)
- ❌ Doesn't work with deleted songs

**Decision:** Path-based checkpoint. Robust and simple.

---

### 3. AI Playlists vs. Manual Playlists

**Decision:** Separate entities (`AiPlaylistEntity` vs. existing `Playlist`).

**Rationale:**
- ✅ AI playlists are immutable snapshots (don't change when user edits)
- ✅ Manual playlists are mutable (user adds/removes songs)
- ✅ Different generation methods (similarity, genre filter, seed song)
- ✅ Clearer semantics (user understands "this was auto-generated")
- ❌ Code duplication (UI must handle both playlist types)

**Alternative Considered:** Single Playlist entity with `IsAiGenerated` flag.
- ❌ Confusing (auto-generated playlist looks mutable but isn't)
- ❌ Hard to distinguish in schema (IsAiGenerated flag scattered across code)

**Decision:** Separate entities. Clean schema, clear semantics.

---

### 4. Similarity Metric

**Decision:** Defer to product/architect decision (BLOCKING).

**Options:**
1. **Cosine distance** (embeddings-based) — Requires embedding service (OpenAI, Hugging Face)
2. **Feature blend** (Energy × Acousticness × Danceability) — Uses SongMetadata fields, no external API
3. **Spotify API** (if authenticated) — Requires Spotify integration
4. **Local ML model** (e.g., librosa) — Requires Python subprocess or ONNX runtime

**Recommendation:** Feature blend (simplest, no external dependency). Can be replaced later.

---

### 5. AI Model/Service

**Decision:** Defer to product/architect decision (BLOCKING).

**Options:**
1. **OpenAI API** (text-davinci-003 or GPT-4) — Expensive ($), requires API key
2. **Spotify/MusicBrainz API** — Requires user auth, rate limits
3. **Local model** (librosa, AudioSet) — Requires model weights + compute (slow on Pi)
4. **Mock service** (hardcoded test data) — For MVP/testing only

**Recommendation:** Mock service for MVP (allows endpoint testing), then integrate chosen model later.

---

## Risk Matrix

| Risk | Severity | Mitigation |
|------|----------|-----------|
| **Storage explosion (O(n²))** | HIGH | Pagination for queries. Archive old similarity data. Monitor SQLite file size. |
| **Background job OOM** | MEDIUM | Chunk size limit (max 20 songs/iteration). Monitor memory. |
| **Stale data** | MEDIUM | Add TTL or "recompute after X days" flag. Log last computation. |
| **Batch never resumes** | MEDIUM | Implement auto-retry: if batch in_progress for >24h, reset to pending. |
| **Similarity scores invalid** | LOW | Validate range (0.0-1.0) in service. Add CHECK constraint in migration. |
| **Song deleted (orphaned metadata)** | MEDIUM | Cascade delete SongMetadata/SongSimilarity on song removal. |
| **Duplicate batch creation** | LOW | Unique constraint on BatchId. Return 409 Conflict if already running. |
| **AI API credential leak** | HIGH | Never commit API keys. Use environment variables. Validate input before external API call. |

---

## Implementation Sequence

1. **Phase 1:** Entities + Migration + Service Registration (6h)
   - Add 5 entities to MusicContext
   - Create migration
   - Register services in Program.cs

2. **Phase 2:** Classification Service + Endpoints (8h)
   - Implement AIClassificationService
   - Add `/classify/start`, `/status/{id}`, `/resume/{id}` endpoints
   - Test with mock AI service

3. **Phase 3:** Similarity Computation + AI Playlists (10h)
   - Implement AISimilarityService
   - Generate similarity matrix (background job)
   - Add `/similarities/compute`, `/similar`, `/ai/playlists/*` endpoints

4. **Phase 4:** Background Processor + Resumability (6h)
   - Implement AIBackgroundProcessor
   - Test crash/resume scenarios
   - Verify checkpoint logic

5. **Phase 5:** Integration + Testing (8h)
   - Integration tests (Zoe)
   - Endpoint testing (iOS app compatibility)
   - Performance testing on Raspberry Pi

---

## Open Questions for Team

**For Mal (Architect):**
1. Which AI model should we integrate? (OpenAI, local, mock, or other?)
2. Similarity metric preference? (cosine, feature blend, or custom?)
3. Batch frequency: on startup, daily, on-demand, or always background?
4. Max songs per AI playlist? (default: 50, configurable?)
5. Should we archive old similarity data? (keep all vs. TTL?)

**For Kaylee (Frontend):**
1. Should AI playlists appear in the same nav as manual playlists?
2. How should we visualize "why this song was included"? (show relevance score?)
3. Should users be able to edit/delete AI-generated playlists?
4. Batch progress UI: show in separate tab, or integrated into playlist creation flow?

**For Zoe (QA):**
1. Should we test resumability with forced service kills (oom-killer)?
2. Performance baseline for 5k-song library?
3. Stress test: 50k-song library (scalability)?

---

## Blocking Decisions

**Before implementation can begin, team must decide:**

- [ ] AI model/service (OpenAI API, local, mock, Spotify, other?)
- [ ] Similarity metric (cosine, feature blend, custom, Spotify API?)
- [ ] Batch frequency (startup, daily, on-demand, continuous background?)
- [ ] UI integration (separate nav for AI playlists, or merged?)
- [ ] Data retention (keep all similarity data, or archival policy?)

---

## Files Affected

### New Files (6)
- `HomeSpeaker.Server2/Services/AIClassificationService.cs`
- `HomeSpeaker.Server2/Services/AISimilarityService.cs`
- `HomeSpeaker.Server2/Services/AIBackgroundProcessor.cs`
- `HomeSpeaker.Server2/Endpoints/AiClassificationEndpoints.cs`
- `HomeSpeaker.Server2/Endpoints/AiSimilarityEndpoints.cs`
- `HomeSpeaker.Server2/Endpoints/AiPlaylistEndpoints.cs`

### Modified Files (3)
- `HomeSpeaker.Server2/Data/MusicContext.cs` (add 5 DbSets + indexes)
- `HomeSpeaker.Server2/Program.cs` (register 3 services + 1 hosted service)
- `HomeSpeaker.Server2/Endpoints/HomeSpeakerRestEndpoints.cs` (call mapAiEndpoints)

### Migrations (1)
- `HomeSpeaker.Server2/Migrations/{timestamp}_AddAiMusicFeatures.cs`

---

## Approval Checklist

- [ ] Mal: Architecture approved?
- [ ] Kaylee: UI integration design confirmed?
- [ ] Jonathan: AI model selected?
- [ ] Wash: Ready to implement? (conditional on blocking decisions)
- [ ] Zoe: Testing strategy reviewed?

---

## References

- Full analysis: `BACKEND_AI_ANALYSIS.md`
- Entity schemas: See MusicContext entity definitions
- Current data layer: `MusicContext.cs`, `PlaylistService.cs`, `Mp3Library.cs`
- Existing patterns: `DailyAnchorWorker.cs` (HostedService), `PlaylistService.cs` (scoped service)


---


**Date:** 2026-03-24  
**Author:** Kaylee (Frontend Dev)  
**Status:** Proposed (Design Phase)

---

## Feature Overview

Three interconnected features for AI-generated playlists:
1. **AI Playlists Menu Option** — New nav item linking to genre-based AI playlist selector
2. **Thumbs Up/Down Feedback** — In-song feedback buttons during AI playlist playback
3. **AI Processing Status Page** — Real-time progress tracker for AI playlist generation jobs

---

## UI File Map

### New Pages (Routes)

#### 1. Pages/Music/AIPlaylists.razor (NEW)
- **Route:** `/ai-playlists`
- **Purpose:** Genre-based AI playlist browser and selector
- **Content Layout:**
  - Header: "AI Playlists" with description
  - Genre grid: 6-8 genre cards (Rock, Pop, Jazz, Classical, Electronic, Hip-Hop, Country, R&B)
  - Each card: Genre name + thumbnail color + tap target ≥56×56px
  - Action: Tap genre → generates playlist → auto-plays (OR shows status page if generation takes >2s)
- **Touch-First:** Genre cards are large, finger-friendly tap targets; responsive grid (2 cols on RPi, 3+ on desktop)
- **Loading State:** Spinner + "Generating playlist..." message if generation async
- **Integration Points:**
  - Calls new `AIPlaylistService.GeneratePlaylistAsync(genre)`
  - Passes playlist to `HomeSpeakerService.UpdateQueueAsync()`
  - Navigates to `/ai-status` if generation is long-running

#### 2. Pages/Music/AIStatus.razor (NEW)
- **Route:** `/ai-status`
- **Purpose:** Real-time status tracker for ongoing AI playlist generation jobs
- **Content Layout:**
  - Active Jobs List:
    - Job card per genre being processed
    - Genre name + progress bar (0-100%)
    - Status text: "Analyzing [Genre]... 35% complete"
    - Estimated time remaining
    - Cancel button (56×56px touch target)
  - Completed Jobs:
    - Genre name + "Complete" checkmark
    - Play button (56×56px) to queue the playlist
  - Empty State: "No active jobs" if nothing processing
- **Refresh Rate:** Real-time updates via SignalR or polling (recommend SignalR for responsiveness)
- **Touch-First:** Large buttons, high contrast status indicators
- **Integration Points:**
  - Calls new `AIPlaylistService.GetActiveJobsAsync()`
  - Calls new `AIPlaylistService.CancelJobAsync(jobId)`
  - Subscribes to status updates (SignalR or polling)

### New Components

#### 1. Components/Music/AIFeedback.razor (NEW)
- **Purpose:** Thumbs up/down buttons shown during AI playlist playback
- **Placement:** In `PlayControls.razor` or as sibling in `Pages/Index.razor` when in AI mode
- **Content:**
  - Icon buttons: 👍 (green when active) + 👎 (red when active)
  - Minimum 56×56px touch targets (WCAG AAA)
  - Hover/Active states: scale(0.97), opacity 0.85 (per decisions.md)
  - Disabled when no song playing or not in AI mode
- **Integration:**
  - Receives current song ID from `PlayerState`
  - Calls `AIPlaylistService.SubmitFeedbackAsync(songId, liked: bool)`
  - Visual feedback on tap (color change + slight scale)
  - No modal/confirmation (immediate action)

#### 2. Components/Music/GenreCard.razor (NEW)
- **Purpose:** Genre tile for AI Playlists page
- **Content:**
  - Genre name (centered, bold)
  - Optional: Genre emoji or color-coded background
  - Minimum 56×56px on mobile, 80×80px on desktop
  - Ripple effect on tap (or scale(0.97) per touch decisions)
  - Active state: color shift to primary green (#1DB954)
- **Props:** `Genre`, `OnGenreSelected` callback

#### 3. Components/Music/JobStatus.razor (NEW)
- **Purpose:** Individual job card in AIStatus page
- **Content:**
  - Genre name (header)
  - Progress bar (Bootstrap progress or custom CSS)
  - Status text + % complete
  - Conditional: Cancel button (pending) or Play button (complete)
  - Checkmark icon when done
- **Props:** `Job` (AIPlaylistJob model)

### Modified Components/Pages

#### 1. Components/Layout/NavMenu.razor (MODIFIED)
- **Change:** Add "AI Playlists" nav item
- **Icon:** `fa-sparkles` (sparkle icon, indicates AI/magic)
- **Position:** After "Playlists", before "YouTube"
- **Href:** `ai-playlists`
- **Touch Compliance:** Existing 56px min-height maintained

#### 2. Components/Music/PlayControls.razor (MODIFIED)
- **Change:** Conditionally render `AIFeedback` component when in AI mode
- **Logic:** Check `PlayerState.IsAIPlaylistMode` or similar flag
- **Placement:** Below main play/pause/skip buttons
- **Touch Compliance:** Buttons remain ≥56px

#### 3. Pages/Index.razor (MODIFIED)
- **Change (Alternative):** Add `AIFeedback` component to home page "Now Playing" section
- **Rationale:** Users see feedback buttons immediately during AI playlist playback
- **Implementation:** Show component only when `PlayerState.IsAIPlaylistMode == true`
- **Touch Compliance:** Button sizing preserved

#### 4. Pages/Music/Music.razor (MODIFIED)
- **Change (Optional):** Add "Browse AI Playlists" CTA button or quick link
- **Placement:** Search header section or as a "Featured" row above library
- **Rationale:** Quick discovery path to AI feature from main music library
- **Touch Compliance:** 56×56px min button size

---

## Route/Navigation Plan

### New Routes

```
/ai-playlists          — Genre browser (main entry point)
/ai-status             — Job monitoring dashboard
/ai-status/[jobId]     — (Optional) Deep-link to specific job
```

### Navigation Flow

**Flow 1: Happy Path (Quick Generation)**
```
User taps "AI Playlists" nav item
  ↓
Route to /ai-playlists
  ↓
User taps genre card (e.g., "Rock")
  ↓
AIPlaylistService.GeneratePlaylistAsync("Rock")
  ↓
[< 2s] Playlist ready immediately
  ↓
HomeSpeakerService.UpdateQueueAsync(playlist)
  ↓
Auto-navigate to Home or Queue (show "Now Playing")
  ↓
AIFeedback buttons visible during playback
```

**Flow 2: Long-Running Generation**
```
User taps genre card
  ↓
AIPlaylistService.GeneratePlaylistAsync("Rock")
  ↓
[> 2s] Generation in progress
  ↓
Show spinner + "Generating playlist..."
  ↓
Auto-navigate to /ai-status
  ↓
User sees progress: "Analyzing Rock... 45% complete"
  ↓
User can Cancel job or wait for completion
  ↓
On completion: Play button appears
  ↓
User taps Play → queue updates → auto-navigate to Home
```

**Flow 3: Feedback During Playback**
```
AI playlist now playing
  ↓
User sees song with thumbs up/down buttons
  ↓
User taps 👍 or 👎
  ↓
AIPlaylistService.SubmitFeedbackAsync(songId, liked)
  ↓
Backend stores feedback for model improvement
  ↓
UI shows brief success indicator (color flash)
```

### Bottom Nav / Mobile Menu Changes

**Current bottom nav items (on RPi):**
- Home, Queue, Music, Streams, More

**Sidebar menu items:**
- Home, Music, Queue, Streams, Playlists, YouTube, Anchors, NightScout (if configured)

**Addition:**
- Add "AI Playlists" nav item in both sidebar and "More" menu on bottom nav
- **Icon:** `fa-sparkles` (sparkle)
- **Position:** After Playlists, before YouTube (sidebar); in "More" menu (mobile)

---

## Touch-First UX Patterns Applied

### From decisions.md Compliance

| Decision | Application |
|----------|-------------|
| **Touch-First Design** | Genre cards ≥56×56px, feedback buttons ≥56×56px, nav items 56px min-height |
| **WCAG AAA Targets** | All buttons, cards, and interactive elements meet 44×44px minimum (most 56×56px) |
| **Active States** | Tap feedback: `scale(0.97) + opacity 0.85` (no hover-only interactions) |
| **No Hover-Only Features** | All interactive states work on touch (no `:hover` without `:active`) |
| **Bottom Nav RPi** | AI Playlists accessible via "More" button, sidebar link on desktop |
| **Typography ≥14px** | All genre names, status text, progress labels ≥14px (complies with existing standard) |
| **Momentum Scrolling** | Genre grid and job list inherit `-webkit-overflow-scrolling: touch` from body |

### Component Spacing (RPi Optimization)

- **Genre Grid on RPi (800×480):** 2 columns × variable rows
- **Genre Card Size:** 150×150px (or 45% width) with 0.75rem gap
- **Status Page List:** Full width with 56px min-height per job card
- **Button Padding:** `var(--hs-space-md)` (0.75rem) for finger-friendly targets

---

## UX Risks & Mitigation

### Risk 1: AI Generation Timeout (User Frustration)
- **Problem:** User expects instant gratification; long generation times feel broken
- **Severity:** Medium
- **Mitigation:**
  - Show clear progress page if generation > 2s
  - Display ETA and real-time % complete
  - Allow cancellation (user can retry)
  - Success animation/sound on completion (optional delight)
- **Recommendation:** Set backend timeout to 60s max; auto-cancel if exceeded

### Risk 2: Feedback Button Discoverability
- **Problem:** Thumbs up/down buttons may not be obvious; users don't know they can provide feedback
- **Severity:** Low-Medium
- **Mitigation:**
  - Show buttons prominently in "Now Playing" section
  - Highlight with contrasting color (#1DB954 green / #ff6b6b red)
  - Optional: Toast notification on first AI playlist: "Help improve recommendations — tap 👍 or 👎"
  - Keyboard shortcut: `Shift+U` (up/thumbs-up), `Shift+D` (down/thumbs-down) if keyboard used

### Risk 3: Status Page Navigation Confusion
- **Problem:** User lands on /ai-status during generation; unclear how to return to music after job completes
- **Severity:** Low
- **Mitigation:**
  - Auto-navigate to Home or Queue after job completion
  - Add "Back to Music" button if user wants to leave manually
  - Keep status page non-modal (user can navigate away anytime via nav menu)
  - Breadcrumb or header link back to /ai-playlists

### Risk 4: Mobile Menu Navigation (Bottom Nav)
- **Problem:** "AI Playlists" hidden under "More" menu on RPi; user may not find it
- **Severity:** Low-Medium
- **Mitigation:**
  - Ensure "More" menu is clearly labeled and accessible
  - Consider: Temporarily promote "AI Playlists" to main bottom nav (swap out "Music" or "Streams" if low-priority)
  - Alternative: Add quick action button on home page: "Try AI Playlists" CTA
  - Keyboard shortcut: `A` key (if keyboard shortcuts extended for this)

### Risk 5: Accessibility (Screen Readers)
- **Problem:** Emoji/icon buttons may not be clear; progress bar may lack ARIA labels
- **Severity:** Medium (applies to all blind/low-vision users)
- **Mitigation:**
  - Thumbs buttons: `aria-label="Thumbs up - love this song"` and `"Thumbs down - skip similar"`
  - Progress bar: `aria-valuenow`, `aria-valuemin`, `aria-valuemax` attributes
  - Genre cards: `role="button"` + `aria-label="Generate Rock playlist"`
  - Status text always visible (don't rely on color alone)

### Risk 6: Playlist Queue Clearing
- **Problem:** AI playlist generation may clear/replace current queue; user loses context
- **Severity:** Medium
- **Mitigation:**
  - Confirm before clearing queue: "Replace current queue with AI playlist?"
  - Alternative: Append AI playlist to existing queue (let user choose)
  - Show what will be replaced: "Current queue (5 songs) → Rock AI Playlist (25 songs)"
  - Undo button for 10s post-generation (quick revert)

---

## Service/Backend Integration Points

### New Service: AIPlaylistService (Backend + Blazor Wrapper)

**Methods to Implement:**

```csharp
// Generate new AI playlist for genre
Task<Playlist> GeneratePlaylistAsync(string genre);

// Check ongoing job status
Task<AIPlaylistJob> GetJobAsync(string jobId);

// List all active and recent jobs
Task<List<AIPlaylistJob>> GetActiveJobsAsync();

// Cancel generation job
Task CancelJobAsync(string jobId);

// Submit feedback for a song
Task SubmitFeedbackAsync(string songId, bool liked);

// Get feedback statistics (optional)
Task<FeedbackStats> GetFeedbackStatsAsync();
```

**Models:**

```csharp
public record AIPlaylistJob(
    string Id,
    string Genre,
    int PercentComplete,
    DateTime StartedAt,
    DateTime? CompletedAt,
    string Status, // "Pending", "Processing", "Complete", "Cancelled", "Failed"
    string? ErrorMessage,
    Playlist? GeneratedPlaylist
);

public record FeedbackEntry(
    string SongId,
    bool Liked,
    DateTime SubmittedAt
);
```

### Integration with Existing Services

**HomeSpeakerService:**
- Already has `UpdateQueueAsync()` — use for AI playlist queue population
- Already has `GetStatusAsync()` — extend to include `IsAIPlaylistMode` flag

**PlaybackModeService:**
- No changes needed (AI playlists use standard playback)

**Program.cs:**
- Register new `AIPlaylistService` as scoped: `builder.Services.AddScoped<AIPlaylistService>();`

---

## File Manifest

### New Files to Create

```
Pages/Music/AIPlaylists.razor              (genre browser page)
Pages/Music/AIStatus.razor                 (job monitoring page)
Components/Music/AIFeedback.razor          (thumbs up/down component)
Components/Music/GenreCard.razor           (genre tile component)
Components/Music/JobStatus.razor           (job status card component)
Services/AIPlaylistService.cs              (backend service wrapper)
Models/AIPlaylistJob.cs                    (job model)
Models/FeedbackEntry.cs                    (feedback model)
```

### Modified Files

```
Components/Layout/NavMenu.razor            (+ AI Playlists nav item)
Components/Music/PlayControls.razor        (+ conditional AIFeedback)
Pages/Index.razor                          (+ conditional AIFeedback)
Pages/Music/Music.razor                    (+ optional CTA to AI Playlists)
Program.cs                                 (+ AIPlaylistService registration)
```

---

## CSS Classes Needed

```css
.genre-grid              /* 2-col responsive grid for RPi, 3+ on desktop */
.genre-card              /* 56×56px+ tap target, flex column, center content */
.genre-card:active       /* scale(0.97) opacity 0.85 per touch decisions */
.ai-feedback-buttons     /* inline flex, gap for buttons */
.feedback-btn            /* 56×56px, circular or square with icon */
.feedback-btn.liked      /* primary green background */
.feedback-btn.disliked   /* accent red background */
.status-job-card         /* card-like container for each job */
.progress-bar-ai         /* green progress indicator */
.status-text             /* left-aligned, ≥14px font */
.status-badge            /* "Processing", "Complete", "Failed" state indicator */
.loading-spinner-ai      /* extends existing .loading-spinner class */
```

---

## Accessibility (WCAG 2.1 Level AA)

- ✅ Color contrast: All text ≥ 4.5:1 ratio against backgrounds
- ✅ Touch targets: ≥44×44px (most ≥56×56px)
- ✅ Keyboard navigation: Tab order, Enter/Space to activate
- ✅ Screen reader support: ARIA labels on icons, progress bars, status
- ✅ Motion: No auto-animations; animations are optional (respects `prefers-reduced-motion`)
- ✅ Focus visible: All interactive elements have visible focus indicator
- ⚠️ Status updates: Use live regions (`aria-live="polite"`) for job status updates

---

## Summary

**What changes:**
- **+3 new pages** (AI Playlists, Status, maybe QR-scan join)
- **+3 new components** (Genre card, Feedback buttons, Job status)
- **+1 new service** (AIPlaylistService)
- **Navigation update** (1 menu item)

**What stays the same:**
- Existing playback, queue, and playlist logic
- Player state and controls
- Touch-first design system (reuse existing design tokens)

**Key UX wins:**
- One-tap access to genre-based playlists
- Real-time job progress visibility
- Lightweight feedback loop (thumbs buttons always ready)
- Compliant with RPi touch-screen constraints

**Risks to watch:**
- Generation timeout UX (mitigate with progress page)
- Feedback button discovery (mitigate with prominent placement + toast)
- Queue clearing consent (mitigate with confirmation modal)
- Accessibility (mitigate with ARIA labels + keyboard support)


---


**For UI Designer / Frontend Developer Review**

---

## Visual Layout Mockups

### 1. AI Playlists Page (`/ai-playlists`)

```
┌─────────────────────────────────────────┐
│  [≡] Home Speaker      [Aspire Link]   │  ← NavMenu Header (existing)
├─────────────────────────────────────────┤
│ Playback Section with PlayControls      │  ← Existing component
├─────────────────────────────────────────┤
│ Main Content Area:                      │
│                                         │
│  AI Playlists                           │
│  ───────────────────────────────────   │
│  Discover music by genre. Let AI curate │
│  a playlist tailored to your mood.      │
│                                         │
│  ┌──────────┐  ┌──────────┐             │
│  │   Rock   │  │   Pop    │             │
│  │  🎸 🔥   │  │  🎤 ✨   │             │
│  │ 25 songs │  │ 30 songs │             │
│  └──────────┘  └──────────┘             │
│                                         │
│  ┌──────────┐  ┌──────────┐             │
│  │  Jazz    │  │ Classical│             │
│  │  🎷 🎵   │  │  🎻 🎼   │             │
│  │ 20 songs │  │ 22 songs │             │
│  └──────────┘  └──────────┘             │
│                                         │
│  ┌──────────┐  ┌──────────┐             │
│  │Electronic│  │ Hip-Hop  │             │
│  │  🎛️ ⚡   │  │  🎤 🔥   │             │
│  │ 28 songs │  │ 24 songs │             │
│  └──────────┘  └──────────┘             │
│                                         │
│  ┌──────────┐  ┌──────────┐             │
│  │ Country  │  │  R&B     │             │
│  │  🎸 🌾   │  │  🎹 💫   │             │
│  │ 26 songs │  │ 23 songs │             │
│  └──────────┘  └──────────┘             │
│                                         │
└─────────────────────────────────────────┘
     [Home] [Queue] [Music] [Streams] [≡]    ← Bottom Nav (mobile)

```

**Design Notes:**
- **Grid:** 2 columns on RPi (800×480), 3+ on desktop
- **Card Size:** ~150×150px on mobile, ~180×180px on desktop
- **Tap Target:** Each card ≥56×56px (easily reached by thumb)
- **Typography:** Genre name (bold, 18px min), song count (14px secondary)
- **Active State:** On tap: `scale(0.97)` + opacity fade, color shifts to primary green
- **Color:** Each genre gets a subtle gradient or themed color (Rock=red, Pop=pink, Jazz=blue, etc.)

---

### 2. Loading Spinner During Generation

```
┌─────────────────────────────────────────┐
│  AI Playlists                           │
├─────────────────────────────────────────┤
│                                         │
│              ⟳ Loading...               │
│                                         │
│         Generating Rock Playlist        │
│                                         │
│       This may take up to 1 minute.     │
│                                         │
└─────────────────────────────────────────┘

[Auto-navigates to /ai-status if >2 seconds]
```

---

### 3. AI Status Page (`/ai-status`) — Active Jobs

```
┌─────────────────────────────────────────┐
│  [≡] Home Speaker                       │
├─────────────────────────────────────────┤
│ Playback Section                        │
├─────────────────────────────────────────┤
│ Main Content Area:                      │
│                                         │
│  AI Playlist Status                     │
│  ───────────────────────────────────   │
│                                         │
│  Active Jobs:                           │
│                                         │
│  ┌─────────────────────────────────┐   │
│  │ 🎸 Rock                         │   │
│  │ ████████░░░░░░░░ 50%           │   │
│  │ Analyzing hits... Est. 20s      │   │
│  │              [❌ Cancel]        │   │
│  └─────────────────────────────────┘   │
│                                         │
│  ┌─────────────────────────────────┐   │
│  │ ✓ Pop                           │   │
│  │ █████████████████░ 95%          │   │
│  │ Almost done... Est. 5s          │   │
│  │                    [⏯️ Play]    │   │
│  └─────────────────────────────────┘   │
│                                         │
│  Completed:                             │
│                                         │
│  ┌─────────────────────────────────┐   │
│  │ ✓ Jazz                          │   │
│  │ █████████████████████ 100%      │   │
│  │ Complete! 20 songs ready        │   │
│  │                    [⏯️ Play]    │   │
│  └─────────────────────────────────┘   │
│                                         │
│  [← Back to Genres] [Home]              │
│                                         │
└─────────────────────────────────────────┘
     [Home] [Queue] [Music] [Streams] [≡]
```

**Design Notes:**
- **Job Card:** Full width, padding 12px, min-height 80px
- **Progress Bar:** Bootstrap progress bar (or custom CSS with primary green fill)
- **Status Text:** "Analyzing Rock..." (14px secondary color), ETA (12px tertiary)
- **Action Buttons:** 
  - Cancel (red, 56×56px) — only when `status == "Processing"`
  - Play (green, 56×56px) — only when `status == "Complete"`
- **Live Updates:** Progress % and ETA update every 1-2 seconds (SignalR or polling)
- **Success Animation:** Optional: Brief checkmark animation on completion

---

### 4. Now Playing with Feedback Buttons

```
┌─────────────────────────────────────────┐
│ Home Speaker                            │
├─────────────────────────────────────────┤
│ Now Playing                             │
│                                         │
│ Song Title: "Hotel California"          │
│ Artist: Eagles                          │
│ Album: Hotel California                 │
│                                         │
│ [███████████░░░░░░░░] 3:24 / 6:30      │
│                                         │
│ Volume: [███████░░] 70%                 │
│                                         │
│ [⏮️] [⏯️] [⏭️]                           │
│  56px  80px  56px (touch targets)       │
│                                         │
│ [👍]       [👎]        ← AI Feedback    │
│  56px      56px        ← (if AI mode)   │
│                                         │
│ Idle · 7 minutes                        │
│                                         │
└─────────────────────────────────────────┘
```

**Design Notes:**
- **Feedback Buttons:** Only visible during AI playlist playback
- **Colors:** 
  - Default: Gray (--bs-secondary)
  - Liked (👍): Primary green (#1DB954) — shows user rated positively
  - Disliked (👎): Accent red (#ff6b6b) — shows user rated negatively
- **Size:** 56×56px minimum, flex container with gap
- **Icon Source:** Font Awesome icons or emoji
- **Interaction:**
  - Tap to toggle (if liked, tap again to unlove)
  - Immediate color feedback (no confirmation modal)
  - Toast notification: "👍 Thanks for the feedback!" (optional delight)

---

### 5. Mobile Menu (Bottom Nav) Changes

```
┌─────────────────────────────────────────┐
│  [≡] Home Speaker                       │
├─────────────────────────────────────────┤
│ Playback & Content                      │
│                                         │
│                                         │
│                                         │
└─────────────────────────────────────────┘
┌─────────────────────────────────────────┐
│ [🏠] [▶️] [🎵] [📻] [≡]                │  ← Bottom Nav
│ Home Queue Music Streams More           │
└─────────────────────────────────────────┘

When "More" (≡) is tapped:

┌─────────────────────────────────────────┐
│ ╔═════════════════════════════════════╗ │
│ ║ Menu                          [✕]  ║ │  ← Mobile menu overlay
│ ╠═════════════════════════════════════╣ │
│ ║ [🏠] Home                           ║ │
│ ║ [🎵] Music                          ║ │
│ ║ [▶️] Queue                          ║ │
│ ║ [📻] Streams                        ║ │
│ ║ [📋] Playlists                      ║ │
│ ║ [✨] AI Playlists        ← NEW!     ║ │
│ ║ [📺] YouTube                        ║ │
│ ║ [⚓] Anchors                         ║ │
│ ║ [❤️] NightScout (if configured)    ║ │
│ ╚═════════════════════════════════════╝ │
│                                         │
└─────────────────────────────────────────┘
```

**Design Notes:**
- **Sparkle Icon:** `fa-sparkles` (✨) — indicates AI/magic
- **Position:** After Playlists, before YouTube (alphabetical + thematic grouping)
- **Mobile:** Hidden in "More" menu; appears as 5th nav item in sidebar (desktop ≥1024px)

---

## Color & Icon Reference

### Theme Colors (from decisions.md)

| Element | Hex | Usage |
|---------|-----|-------|
| Primary | #1DB954 | Active states, positive feedback (👍), success indicators |
| Secondary | #535bf2 | Highlights, secondary actions, secondary text |
| Accent | #ff6b6b | Warnings, negative feedback (👎), cancel actions |
| BG Dark | #121212 | Page background |
| BG Mid | #282828 | Card background, input backgrounds |
| BG Light | #181818 | Alternate background |

### Icons (Font Awesome 6.4.0)

| Icon | Usage |
|------|-------|
| `fa-sparkles` ✨ | AI Playlists nav item |
| `fa-circle-notch` | Loading spinner (animate rotation) |
| `fa-thumbs-up` 👍 | Positive feedback button |
| `fa-thumbs-down` 👎 | Negative feedback button |
| `fa-check-circle` ✓ | Job completion indicator |
| `fa-times-circle` | Job failure indicator |
| `fa-pause-circle` | Job cancelled indicator |
| `fa-clock` | ETA countdown |
| `fa-arrow-left` ← | Back button |

---

## Responsive Breakpoints

**RPi (800×480 landscape):**
- Genre grid: 2 columns × variable rows
- Card size: ~150×150px
- Bottom nav: Always visible, 70px tall
- Sidebar: Hidden (shown via mobile menu toggle)
- Content area height: 480px - 70px (bottom nav) - playback section ≈ 350px

**Tablet (600×800 portrait):**
- Genre grid: 2 columns
- Card size: ~180×180px
- Bottom nav: Visible OR sidebar depending on screen width
- Breakpoint: <1024px = bottom nav + mobile menu

**Desktop (≥1024px):**
- Genre grid: 3 columns (or 4 on ultra-wide)
- Card size: ~200×200px
- Sidebar: Always visible, 12em width
- Bottom nav: Hidden

---

## Animation & Microinteractions

### Genre Card Tap
```css
.genre-card:active {
  transform: scale(0.97);
  opacity: 0.85;
  transition: all 100ms ease-out;
}
```

### Progress Bar Update
```css
.progress-bar-ai {
  background-color: var(--hs-primary, #1DB954);
  transition: width 0.5s ease-in-out;
  height: 6px;
  border-radius: 3px;
}
```

### Feedback Button Click
```css
.feedback-btn:active {
  transform: scale(0.95);
  box-shadow: inset 0 2px 4px rgba(0, 0, 0, 0.2);
}
```

### Spinner (Loading)
```css
.spinner {
  animation: spin 1s linear infinite;
}

@keyframes spin {
  from { transform: rotate(0deg); }
  to { transform: rotate(360deg); }
}
```

---

## Font Sizing (Touch-Friendly)

| Element | Size | Usage |
|---------|------|-------|
| Genre name | 18px | Genre cards, clear and readable |
| Song count | 14px | Secondary info on cards |
| Job status | 14px | "Analyzing Rock..." text |
| Status badge | 12px | "Processing", "Complete" |
| Page title | 24px | "AI Playlists", "AI Status" |
| Progress % | 14px | "50% complete" |
| Aria labels | — | Not visible, for screen readers |

---

## Accessibility — Focus States

All interactive elements must have visible focus indicator:

```css
a:focus,
button:focus,
[role="button"]:focus {
  outline: 2px solid var(--hs-primary, #1DB954);
  outline-offset: 2px;
}
```

**Touch-friendly:** Outline offset prevents overlap with content.

---

## Notes for CSS Implementation

### New CSS Classes to Add

```css
.genre-grid {
  display: grid;
  grid-template-columns: repeat(2, 1fr);  /* RPi: 2 cols */
  gap: var(--hs-space-lg, 1rem);
  padding: var(--hs-space-md);
}

@media (min-width: 600px) {
  .genre-grid {
    grid-template-columns: repeat(3, 1fr);
  }
}

@media (min-width: 1024px) {
  .genre-grid {
    grid-template-columns: repeat(4, 1fr);
  }
}

.genre-card {
  min-height: 150px;
  min-width: 150px;
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  border-radius: var(--hs-radius, 0.5rem);
  background-color: var(--hs-bg-secondary, #282828);
  cursor: pointer;
  touch-action: manipulation;
  transition: all 150ms ease-out;
  padding: var(--hs-space-md);
}

.genre-card:active {
  transform: scale(0.97);
  opacity: 0.85;
  background-color: var(--hs-primary, #1DB954);
}

.ai-feedback-buttons {
  display: flex;
  gap: var(--hs-space-md);
  margin-top: var(--hs-space-md);
}

.feedback-btn {
  min-width: 56px;
  min-height: 56px;
  border-radius: 50%;
  border: 2px solid transparent;
  background-color: var(--bs-secondary);
  color: white;
  font-size: 24px;
  cursor: pointer;
  transition: all 100ms ease-out;
  touch-action: manipulation;
}

.feedback-btn:active {
  transform: scale(0.95);
}

.feedback-btn.liked {
  background-color: var(--hs-primary, #1DB954);
  border-color: var(--hs-primary, #1DB954);
}

.feedback-btn.disliked {
  background-color: var(--hs-accent, #ff6b6b);
  border-color: var(--hs-accent, #ff6b6b);
}

.status-job-card {
  border-radius: var(--hs-radius, 0.5rem);
  background-color: var(--hs-bg-secondary, #282828);
  padding: var(--hs-space-md);
  margin-bottom: var(--hs-space-md);
  min-height: 80px;
  display: flex;
  flex-direction: column;
  justify-content: space-between;
}

.progress-bar-ai {
  height: 6px;
  background-color: var(--hs-bg-primary, #121212);
  border-radius: 3px;
  overflow: hidden;
  margin: var(--hs-space-sm) 0;
}

.progress-bar-ai-fill {
  height: 100%;
  background-color: var(--hs-primary, #1DB954);
  transition: width 0.5s ease-in-out;
}
```

---

## Edge Cases & Fallbacks

1. **No internet during generation:** Show error state, allow retry
2. **Long generation (>60s):** Auto-cancel with error message
3. **Feedback submission fails:** Toast notification "Could not save feedback. Tap to retry?"
4. **Status page polling lags:** Show "Last updated: 2 seconds ago"
5. **Mobile viewport too narrow:** Genre cards stack to 1 column (or hide overflow with horizontal scroll)

---

This document is for design validation and implementation reference. Submit questions to Kaylee before coding.


---


**Quick Reference for Developers**

## The Ask

Add three interconnected features:
1. **AI Playlists menu option** — Navigate to genre-based AI playlist generator
2. **Thumbs up/down feedback** — Rate songs during AI playlist playback
3. **Progress/status page** — Monitor AI playlist generation in real-time

---

## UI Changes at a Glance

### Navigation Changes
- **NavMenu.razor:** Add `<NavLink href="ai-playlists" class="nav-item">` with `fa-sparkles` icon
- **Position:** After "Playlists" link, before "YouTube"
- **Mobile:** Accessible via "More" menu button on bottom nav

### New Pages (Routes)
| Route | File | Purpose |
|-------|------|---------|
| `/ai-playlists` | Pages/Music/AIPlaylists.razor | Genre selector (Rock, Pop, Jazz, etc.) |
| `/ai-status` | Pages/Music/AIStatus.razor | Real-time job progress tracker |

### New Components
| Component | Purpose | Placement |
|-----------|---------|-----------|
| AIFeedback.razor | Thumbs up/down buttons | PlayControls.razor or Index.razor (conditional) |
| GenreCard.razor | Genre tile (selectable) | AIPlaylists.razor |
| JobStatus.razor | Individual job card | AIStatus.razor (per active job) |

### Modified Components
| File | Change |
|------|--------|
| NavMenu.razor | +1 nav item (AI Playlists) |
| PlayControls.razor | +AIFeedback component (conditional, when in AI mode) |
| Index.razor | +AIFeedback component (alternative placement, conditional) |
| Music.razor | +Optional CTA to AI Playlists |
| Program.cs | +AIPlaylistService registration |

---

## Touch-First Compliance Checklist

All changes must respect `/squad/decisions.md` decisions:

- ✅ **Button/tap targets:** Minimum 44×44px (most should be 56×56px or larger)
- ✅ **Active states:** `scale(0.97) + opacity: 0.85` (immediate tactile feedback)
- ✅ **No hover-only interactions:** All states work on touch (`:active`, `:focus`, not just `:hover`)
- ✅ **Typography:** Minimum 14px for all text (except tiny secondary labels)
- ✅ **Responsive layout:** Genre grid = 2 cols on RPi (800×480), 3+ on desktop
- ✅ **Momentum scrolling:** Inherit `-webkit-overflow-scrolling: touch` from body
- ✅ **Touch action:** `touch-action: manipulation` to prevent 300ms tap delay

---

## User Flows

### Quick Path (Playlist ready in <2s)
```
Tap "AI Playlists" nav item
→ See genre grid (Rock, Pop, Jazz, etc.)
→ Tap genre card (e.g., "Rock")
→ [Spinner briefly shows] Playlist generated
→ Auto-navigate to Home or Queue
→ Songs playing, thumbs buttons visible
→ Tap 👍 or 👎 to rate song
```

### Long Path (Playlist takes >2s)
```
Tap genre card
→ [Spinner shows + auto-navigate]
→ Land on /ai-status page
→ See job card: "Rock AI Playlist - 35% complete"
→ User can wait or cancel
→ On completion: "Complete" badge + Play button
→ Tap Play → Queue updated → Auto-navigate home
```

---

## Backend Service Interface

**New Service: AIPlaylistService**

```csharp
// All methods are async Task<T>

// Core generation
GeneratePlaylistAsync(string genre) → Playlist

// Job tracking
GetActiveJobsAsync() → List<AIPlaylistJob>
GetJobAsync(string jobId) → AIPlaylistJob
CancelJobAsync(string jobId) → void

// Feedback loop
SubmitFeedbackAsync(string songId, bool liked) → void
GetFeedbackStatsAsync() → FeedbackStats  [optional]
```

**Models:**
```csharp
public record AIPlaylistJob(
    string Id,
    string Genre,
    int PercentComplete,        // 0-100
    DateTime StartedAt,
    DateTime? CompletedAt,
    string Status,              // "Pending", "Processing", "Complete", "Cancelled", "Failed"
    string? ErrorMessage,
    Playlist? GeneratedPlaylist
);
```

---

## CSS Design Tokens (Reuse Existing)

```css
/* Colors (from decisions.md) */
--hs-primary: #1DB954;          /* Spotify green */
--hs-accent: #ff6b6b;           /* Coral for negatives *)
--hs-bg-primary: #121212;
--hs-bg-secondary: #282828;

/* Spacing Scale */
--hs-space-sm: 0.5rem;
--hs-space-md: 0.75rem;
--hs-space-lg: 1rem;

/* Component sizes */
min-height: 56px;               /* Touch target minimum */
min-width: 56px;                /* Touch target minimum */
```

---

## Accessibility Requirements

**Minimum WCAG 2.1 AA compliance:**

- Color contrast: All text ≥ 4.5:1 (except <14px text, which must be ≥3:1)
- Focus indicators: Visible on all interactive elements (2px outline in primary color)
- ARIA labels: 
  - Genre cards: `aria-label="Generate Rock playlist"`
  - Thumbs buttons: `aria-label="Love this song"` and `"Skip similar"`
  - Progress bar: `aria-valuenow`, `aria-valuemin`, `aria-valuemax`
  - Job status: Live region `aria-live="polite"` for updates
- Keyboard support: Tab navigation, Enter/Space to activate
- Status messages: Always visible (don't rely on color alone)

---

## Known Risks & Mitigations

| Risk | Severity | Mitigation |
|------|----------|-----------|
| Long generation time feels broken | Medium | Show progress page, ETA, allow cancel |
| Thumbs buttons not discoverable | Low-Med | Prominent placement, highlight color, optional toast |
| Queue clearing without consent | Medium | Confirmation modal before replacing queue |
| Status page navigation confusion | Low | Auto-nav on completion, "Back to Music" button |
| Mobile menu hides AI Playlists | Low-Med | Consider promoting to main bottom nav or add home CTA |
| Accessibility gaps (screen readers) | Medium | ARIA labels + keyboard shortcuts |

---

## Testing Checklist (Before Handoff)

- [ ] Genre card tap works on touch device (RPi or phone)
- [ ] Fast generation (<2s): Auto-plays immediately
- [ ] Slow generation (>2s): Shows status page with live progress
- [ ] Thumbs buttons appear during AI playlist playback
- [ ] Thumbs feedback submits without error, shows visual feedback
- [ ] Status page cancellation works (job stops, UI updates)
- [ ] Back navigation from status page works (don't get stuck)
- [ ] All buttons meet 56×56px minimum (inspect with DevTools)
- [ ] Bottom nav still has 5 items (ensure no overflow on RPi)
- [ ] Keyboard shortcuts work (if extended for AI features)
- [ ] Contrast ratios pass (use axe DevTools or similar)
- [ ] Focus indicators visible on all interactive elements

---

## File Creation Order (Recommended)

1. **Models:** AIPlaylistJob.cs, FeedbackEntry.cs
2. **Service:** AIPlaylistService.cs (backend wrapper, not backend API itself)
3. **Components:** GenreCard.razor, JobStatus.razor, AIFeedback.razor
4. **Pages:** AIPlaylists.razor, AIStatus.razor
5. **Updates:** NavMenu.razor, PlayControls.razor, Program.cs
6. **Styling:** Add CSS classes to app.css (genre-grid, genre-card, ai-feedback-buttons, etc.)

---

## Notes for Mal (Architect)

- AIPlaylistService is a Blazor-side wrapper; the actual AI generation logic lives on the backend (gRPC or REST)
- Recommend SignalR for real-time job status updates (better UX than polling)
- Feedback storage: Add new DB table `SongFeedback` (SongId, UserId?, Liked, Timestamp)
- Consider: Genre detection from existing song metadata (avoid hard-coding 8 genres)

## Notes for Wash (Backend Dev)

- Auth/security: Who can generate AI playlists? Public? Authenticated only?
- Rate limiting: Prevent spam job submissions (e.g., 1 job per 5 seconds per user)
- Job timeout: Set max 60s generation time; auto-cancel and return error if exceeded
- Feedback loop: Is feedback used to improve model? Stored for analytics?
- DB schema: New tables for `AIPlaylistJobs` and `SongFeedback` (schema TBD)

---

## Decision Document

Full UI architecture analysis at:
`.squad/decisions/inbox/kaylee-ai-playlists-uimap.md`

This summary focuses on implementation; detailed design rationale in the full doc.


---


**By:** River (iOS Developer)  
**Date:** 2026-04-30  
**Status:** Awaiting implementation

## Summary
Analyzed iOS app structure for AI Playlists feature. Requires changes to Models, APIClient, and two Playlist-related views. No new Views needed. Risk is LOW with proper state isolation and polling cleanup.

## Changes Required

### Models.swift
- Add `AIPlaylistStatus` enum (generating, ready, failed)
- Extend `Playlist` struct: `isAIGenerated: Bool`, `aiStatus: AIPlaylistStatus?`
- Add `SubmitFeedbackRequest` struct

### APIClient.swift
- Add `createAIPlaylist(prompt:, count:) -> Playlist`
- Add `getAIPlaylistStatus(name:) -> AIPlaylistStatus`
- Add `submitFeedback(playlistName:, songId:, feedback:)`

### PlaylistsView.swift
- Display AI badge (sparkles icon) for AI playlists
- Show processing status (ProgressView if .generating, error icon if .failed)
- Implement polling with Timer (start .onAppear, stop .onDisappear)
- Adaptive backoff: 1s → 2s → 5s, stop when not .generating

### PlaylistDetailView.swift
- Show thumbs up/down buttons for songs (only in AI mode)
- Submit feedback via `api.submitFeedback()`
- Optional: show feedback summary header

### MainTabView.swift
- ContentView references this but it doesn't exist—must be created or verified

## Key Risks & Mitigations

| Risk | Mitigation |
|------|-----------|
| Polling drain | Adaptive backoff; stop when not generating |
| Stale status while backgrounded | Refresh on pull-to-refresh; accept brief staleness |
| Feedback during generation | Only show buttons if aiStatus == .ready |
| Race conditions on status update | Single @State source of truth; indexed updates |
| Submission failures silent | Error toast on failure; optional retry |

## Design Decisions

1. **Polling pattern:** Timer-based (not async sequence) for iOS 15+ compatibility. Consider server-sent events (SSE) in future.
2. **Feedback UI:** Horizontal thumbs in song rows (touch-first design, ≥44px targets).
3. **Backward compatibility:** Playlist struct extension uses optional fields; graceful decode of old playlists.
4. **Isolation:** AI feature is conditional on `isAIGenerated` flag; zero impact on existing playlist flows.

## Open Questions for Backend (Wash)

1. AI creation endpoint request/response format?
2. Possible `AIPlaylistStatus` values and any metadata?
3. Feedback endpoint format?
4. Does feedback immediately reorder songs or only log for training?
5. Error codes for failed generation?

## Implementation Order

1. Models.swift (foundation)
2. APIClient.swift (enables testing)
3. PlaylistsView.swift (status display + polling)
4. PlaylistDetailView.swift (feedback UI)
5. MainTabView.swift (fix missing view)

**Full analysis:** See `iOS_AI_PLAYLISTS_ANALYSIS.md`


---

**Date:** 2025-03-24  
**Author:** Zoe (QA Engineer)  
**Status:** Ready for Implementation Review  

---

## Executive Summary
This matrix defines **77 test cases** across **8 risk domains** for the AI Playlists feature. The feature requires resumable pipeline processing, incremental new-track pickup, multi-genre classification, playlist generation, and adaptive feedback behavior across both Blazor and iOS clients.

**Risk areas prioritized by likelihood of failure:**
1. **Restart safety** (pipeline recovery)
2. **Incremental pickup** (new track detection)
3. **State consistency** (genre markers, playlist sync)
4. **Progress visibility** (UI updates lag)
5. **Feedback loop** (thumbs up/down → autoplay adaptation)

---

## Part A: Restart & Resume Safety

### A1: Database Transaction Integrity
**Risk:** Pipeline crashes mid-song classification; database left in partial state.

| ID | Test Case | Acceptance Criteria | Pass? |
|---|---|---|---|
| **A1.1** | Start classification on 10 songs, kill process at song 5 | Restart: Completed songs marked `classified=true`, unprocessed songs remain `classified=false`. No duplicates. | — |
| **A1.2** | Playlist generation starts, kill process mid-insert | Restart: Partial playlists cleaned up (rollback or marked incomplete). Next run doesn't re-insert duplicates. | — |
| **A1.3** | Genre assignment in transaction fails on song N | Song N skipped, pipeline continues. Song N retried on next restart. No data corruption. | — |
| **A1.4** | SQLite database locked during batch update | Pipeline detects lock, retries (not crash). Logs retry attempt. | — |
| **A1.5** | Shutdown during similarity-link insert | Restart: Partial similarity edges cleaned up. No orphan pointers. Playlist generation works on clean graph. | — |

### A2: Pipeline State Persistence
**Risk:** Pipeline forgets progress; restarts from zero instead of resuming.

| ID | Test Case | Acceptance Criteria | Pass? |
|---|---|---|---|
| **A2.1** | Pipeline batch size 100. Process 60 songs, restart. | Next run: Skips first 60, processes songs 61+. Total run time ≈60% of initial. | — |
| **A2.2** | Pipeline crashes after writing `ProcessedAt` timestamp to 40/100 songs | Restart: Reads timestamp, skips those 40, starts at 41. | — |
| **A2.3** | Pipeline state table corrupted | Recovery: Reset state table, mark all songs unclassified, restart from song 1. Logs warning. | — |
| **A2.4** | Two pipeline instances start simultaneously (race condition) | Instance 1 acquires lock, instance 2 waits/exits. No duplicate classifications. | — |
| **A2.5** | Server restarts 3× in succession during classification | Restarts 2 & 3: Correctly resume from previous checkpoint. No exponential slowdown. | — |

### A3: Graceful Shutdown
**Risk:** Long-running batch job doesn't save progress before exit.

| ID | Test Case | Acceptance Criteria | Pass? |
|---|---|---|---|
| **A3.1** | SIGTERM sent to process during song 47/100 classification | Pipeline: Completes current song, saves state, shuts down cleanly. Restart resumes at song 48. | — |
| **A3.2** | Kill -9 (SIGKILL) during batch insert | Restart: Detects partial insert, rolls back to known good state. | — |
| **A3.3** | Kubernetes pod evicted mid-pipeline | Pod restart: Reads last good checkpoint, resumes. No data loss. | — |

---

## Part B: Incremental Pickup of New Tracks

### B1: New Song Detection
**Risk:** New songs added to library after initial classification are never processed.

| ID | Test Case | Acceptance Criteria | Pass? |
|---|---|---|---|
| **B1.1** | Classify 50 songs, add 10 new songs to folder, run pipeline again | All 10 new songs marked `classified=false`, pipeline processes them. Old songs skipped. | — |
| **B1.2** | New song metadata (title, artist) changed after classification | Pipeline re-checks file modified timestamp. If changed, re-classify. Or: Skip if timestamp unchanged. (Behavior should be documented.) | — |
| **B1.3** | Song file deleted after classification | Pipeline detects missing file, marks `deleted=true`, removes from playlists. No playlist references broken files. | — |
| **B1.4** | Batch job paused at song 50, 100 new files added, resume | Pipeline: Continues from song 51 (original batch). Next scheduled run processes new 100. | — |
| **B1.5** | Folder monitoring: New file detected 0.5s after pipeline ends | Next pipeline run (hourly? scheduled?) picks it up. Grace period documented. | — |

### B2: Incremental Playlist Updates
**Risk:** New classified songs aren't added to existing genre playlists.

| ID | Test Case | Acceptance Criteria | Pass? |
|---|---|---|---|
| **B2.1** | Genre "Rock" has 15 songs. 5 new "Rock" songs classified. | Playlist "Rock" updated to 20 songs. UI shows new count. | — |
| **B2.2** | Genre playlist capped at 50 songs. New songs added but cap hit. | Playlists: Either extend to 60 (if no hard limit) or track "overflow" and rotate oldest. Behavior documented. | — |
| **B2.3** | Song re-classified into different genre. | Old genre playlist: Song removed (if it was the only genre). New genre playlist: Song added. | — |

### B3: Edge Cases — File Handling
**Risk:** Symlinks, invalid files, permission errors break incremental pickup.

| ID | Test Case | Acceptance Criteria | Pass? |
|---|---|---|---|
| **B3.1** | Song is a symlink to another file | Pipeline: Classifies symlink once. If target changes, symlink behavior is defined (skip or re-process). | — |
| **B3.2** | File is unreadable (permission denied) | Log error, skip file, continue. Next run retries. | — |
| **B3.3** | Directory structure changes; song moved to subfolder | Pipeline: Detects new path, updates DB reference. Or: Re-imports as new song (behavior documented). | — |

---

## Part C: Multi-Genre Classification

### C1: Genre Assignment
**Risk:** A song assigned to only 1 genre despite fitting multiple.

| ID | Test Case | Acceptance Criteria | Pass? |
|---|---|---|---|
| **C1.1** | Song (e.g., "Blinding Lights" by The Weeknd) classified | DB record shows `genres = ["synth-pop", "electronic", "dance-pop"]`. All 3 genres linked. | — |
| **C1.2** | Song has no clear genre fit | Assigned to "Uncategorized" or "Other". Behavior consistent. Not left null. | — |
| **C1.3** | Genre list has 18 items. Song fits only 2. | Song linked to those 2 genres only. Null/empty genres not counted. | — |
| **C1.4** | Same song classified twice (edge case) | Second classification: Overwrites first. No duplicate genre links. Transaction clean. | — |

### C2: Genre Playlist Creation (12-18 Genres)
**Risk:** Not all 12-18 genres get playlists, or duplicate playlists created.

| ID | Test Case | Acceptance Criteria | Pass? |
|---|---|---|---|
| **C2.1** | Classify 200 songs with AI model; 15 unique genres emerge | Playlists: Exactly 15 created (1 per genre). No duplicates. | — |
| **C2.2** | Manual genre tags + AI classification conflict | Precedence: Defined (e.g., AI wins, or manual tags preserved). Consistent. | — |
| **C2.3** | Genre "Jazz" has 1 song (below minimum threshold?) | Behavior: Still create playlist if no minimum is enforced. Or: Merge into "Other". Documented. | — |
| **C2.4** | Playlists persist across server restarts | GET /playlists: Returns all 15 genre playlists with correct song counts. | — |

### C3: Multi-Genre UI & Navigation
**Risk:** Genre playlists not surfaced in Blazor or iOS UI.

| ID | Test Case | Acceptance Criteria | Pass? |
|---|---|---|---|
| **C3.1** | Blazor: Playlists page shows "AI Genre Playlists" section | All 15 genre playlists listed with song count and play button. | — |
| **C3.2** | iOS: PlaylistsView includes AI playlists | All AI-generated playlists shown. Play/rename/delete actions available. | — |
| **C3.3** | Click "Rock" playlist in Blazor | Navigates to playlist detail, lists songs, allows play. | — |
| **C3.4** | Click "Electronic" playlist on iOS | Navigates to playlist detail, shows songs, allows queue/play. | — |

---

## Part D: Similarity-Based Autoplay & Song Recommendations

### D1: Similarity Calculation & Scoring
**Risk:** Similarity markers are wrong or unused; "similar songs" are actually dissimilar.

| ID | Test Case | Acceptance Criteria | Pass? |
|---|---|---|---|
| **D1.1** | Song A classified: [rock, alternative, indie]. Song B classified: [rock, indie]. | Similarity score ≥ 0.66 (shared 2/3 genres). Configurable threshold. | — |
| **D1.2** | Calculate similarity for 5,000 songs (all pairs) | Completes in <5 min on typical hardware. Results persisted. | — |
| **D1.3** | Similarity graph updated after new songs classified | New song: Similarity links created to all existing songs. Existing songs get new links back. | — |
| **D1.4** | Similarity A→B = 0.8; B→C = 0.9; A→C = 0.5 | No assumption of transitivity. Stored correctly. | — |

### D2: Autoplay & Up-Next Queue
**Risk:** Autoplay picks dissimilar songs, or doesn't trigger when it should.

| ID | Test Case | Acceptance Criteria | Pass? |
|---|---|---|---|
| **D2.1** | Song ends, queue empty, autoplay enabled | Next song: Top-1 similar song added to queue, plays. | — |
| **D2.2** | Song ends, queue has 3 songs, autoplay enabled | Autoplay: Doesn't trigger (queue not empty). Next song in queue plays. | — |
| **D2.3** | Autoplay disabled | Song ends, queue empty: Stops playback. No auto-add. | — |
| **D2.4** | Song with 0 similar matches (new/unique) | Autoplay: Adds random unplayed song from library. Or: Stops. Documented. | — |

### D3: "Play Something Similar" Mode
**Risk:** User taps "Play Similar", nothing happens or wrong songs play.

| ID | Test Case | Acceptance Criteria | Pass? |
|---|---|---|---|
| **D3.1** | Blazor: Playing song X. Click "Play Similar" button | Queue cleared. Top-5 similar songs added. First plays. | — |
| **D3.2** | iOS: Playing song X. Tap "More" → "Play Similar" | Queue cleared. Top-5 similar songs enqueued. Playback continues/restarts. | — |
| **D3.3** | Play Similar on song with <2 similar matches | Add available similar songs. If <1, add random or stop (documented). | — |

---

## Part E: Thumbs Up/Down Feedback & Adaptation

### E1: Feedback Capture
**Risk:** Feedback button not visible or clicks not recorded.

| ID | Test Case | Acceptance Criteria | Pass? |
|---|---|---|---|
| **E1.1** | Blazor: Playing song. Click thumbs-up button | Icon highlights. Event logged to DB. `feedback=1`. | — |
| **E1.2** | Blazor: Same song. Click thumbs-down button | Thumbs-up clears. Thumbs-down highlights. Event logged. `feedback=-1`. | — |
| **E1.3** | iOS: Playing song. Tap 👍 icon | Icon highlights. API call: POST /api/homespeaker/songs/{id}/feedback with `value=1`. | — |
| **E1.4** | Feedback on a song already rated | Second rating: Overwrites first (no duplicate entries). Count increments. | — |
| **E1.5** | Feedback sent while offline (iOS) | Saved locally, synced on reconnect. No duplicate submissions. | — |

### E2: Feedback Persistence
**Risk:** Feedback lost on restart or not visible next session.

| ID | Test Case | Acceptance Criteria | Pass? |
|---|---|---|---|
| **E2.1** | Rate song X as 👍. Restart server. | Server: Feedback persisted. GET /song/{X} returns `userFeedback=1`. | — |
| **E2.2** | Rate 50 songs over 2 days. View feedback stats. | All 50 ratings shown. Counts correct (e.g., 35 👍, 15 👎). | — |
| **E2.3** | Feedback export/backup | Can backup rating history. Portable format (CSV/JSON). | — |

### E3: Feedback-Driven Autoplay Adaptation
**Risk:** Thumbs-down songs still appear in "similar" recommendations; no learning occurs.

| ID | Test Case | Acceptance Criteria | Pass? |
|---|---|---|---|
| **E3.1** | Song X rated 👎. Autoplay triggers. | Similar songs to X: Excluded from recommendation. Lower-ranked similar songs prioritized. | — |
| **E3.2** | Rate 10 songs 👍, 5 songs 👎. View "Play Similar." | Recommendations: Biased toward 👍 genres. 👎 genres de-prioritized. | — |
| **E3.3** | Song X rated 👎, but user manually enqueues it later | Manual queue: Always overrides feedback rules. No block. | — |
| **E3.4** | Feedback reversal: 👎 → clear → 👍 | Recommendation model: Retrains (if real-time) or updates on next classification run. | — |

### E4: Feedback UI State
**Risk:** UI shows wrong feedback state; user thinks they rated when they didn't.

| ID | Test Case | Acceptance Criteria | Pass? |
|---|---|---|---|
| **E4.1** | Blazor: Play song X (rated 👍 previously). | Button shows 👍 highlighted. Clear visual state. | — |
| **E4.2** | iOS: Play song Y (no prior rating). | Buttons (👍 👎) both unhighlighted. Tappable. | — |
| **E4.3** | Real-time sync: Rate song on Blazor, switch to iOS. | iOS: Immediately shows correct rating state. No lag. | — |

---

## Part F: Progress & Status Visibility

### F1: Blazor Progress Page
**Risk:** No visible progress page; users don't know if pipeline is running or stuck.

| ID | Test Case | Acceptance Criteria | Pass? |
|---|---|---|---|
| **F1.1** | Blazor: Menu link "AI Processing Status" (or similar) | Page shows: Total songs, classified count, % complete, ETA. Refreshes every 2-5s. | — |
| **F1.2** | Pipeline running. Progress page shows 45/200 songs classified, 22% | ETA calculated. E.g., "~3 minutes remaining" (if 2 min/50 songs). | — |
| **F1.3** | User navigates away and back. Progress updates. | Page re-fetches status. Shows current progress. No stale data. | — |
| **F1.4** | Classification paused/stopped. Status shows "Paused" or "Idle" | Controls available: Resume / Cancel. Logging visible. | — |
| **F1.5** | Playlist generation active (separate phase). Progress shows "Generating playlists: 12/15" | Each phase (classification, playlist gen) tracked separately. | — |

### F2: iOS Progress Page
**Risk:** iOS doesn't show processing status; user assumes app is broken.

| ID | Test Case | Acceptance Criteria | Pass? |
|---|---|---|---|
| **F2.1** | iOS: "More" tab or settings. Link to "AI Processing Status" | Shows: Classified / Total, % done, phase (classifying / generating playlists). | — |
| **F2.2** | Pull-to-refresh on status page | Status updates instantly. Reflects latest server state. | — |
| **F2.3** | Status page offline (server unreachable) | Graceful error message. Last known status shown with timestamp (if cached). | — |

### F3: Status Accuracy & Latency
**Risk:** Progress percentage frozen; doesn't reflect actual pipeline work.

| ID | Test Case | Acceptance Criteria | Pass? |
|---|---|---|---|
| **F3.1** | Pipeline processing songs 45-65/200 at 2 sec/song. Status endpoint polled every 2s. | Counts increment by 1-2 every 2s. No jumps or freezes. | — |
| **F3.2** | Heavy system load. Progress page remains responsive (no 5+ sec lag). | UI updates within 3s of actual pipeline progress. | — |

### F4: Notification/Alert on Completion
**Risk:** Pipeline finishes silently; user unaware that AI playlists are ready.

| ID | Test Case | Acceptance Criteria | Pass? |
|---|---|---|---|
| **F4.1** | Pipeline finishes classifying all songs & generating playlists. | Blazor: Toast notification "AI Playlists ready!" with link to view playlists. | — |
| **F4.2** | iOS: Pipeline completes. | Notification (if enabled) or status page badge (e.g., red "1" if in background). | — |
| **F4.3** | Multiple classification runs. Notification only on final completion. | Don't spam on every resumption; only on full feature completion. | — |

---

## Part G: Data Consistency & Edge Cases

### G1: Playlist-Song Linkage
**Risk:** Playlists reference deleted songs; songs in multiple genres break when reassigned.

| ID | Test Case | Acceptance Criteria | Pass? |
|---|---|---|---|
| **G1.1** | Song deleted from library. Playlists containing it updated. | Playlist: Song removed from all genre playlists. Counts decremented. | — |
| **G1.2** | Song re-classified from [Rock, Indie] → [Indie, Electronic]. | Playlists: Removed from Rock, added to Electronic. Indie unchanged. Links clean. | — |
| **G1.3** | Playlist-song join table has orphan entries (data corruption). | Recovery: DELETE FROM playlist_songs WHERE song_id NOT IN (SELECT id FROM songs). | — |

### G2: Genre Tag Stability
**Risk:** Genres change mid-pipeline; inconsistency across playlists.

| ID | Test Case | Acceptance Criteria | Pass? |
|---|---|---|---|
| **G2.1** | Genre list fixed to 15 items before classification. Classification uses that list. | No new genres created mid-run. Consistent genre set across all songs. | — |
| **G2.2** | Admin adds 16th genre mid-pipeline. | Pipeline: Either ignores (uses original 15) or includes new genre. Behavior documented. | — |

### G3: Concurrency & Locks
**Risk:** Multiple clients (Blazor + iOS) both try to rate song; race condition.

| ID | Test Case | Acceptance Criteria | Pass? |
|---|---|---|---|
| **G3.1** | Blazor client rates song 👍, iOS rates 👎 simultaneously | Database: Last write wins (timestamp). One rating recorded. Or: Conflict resolved (documented). | — |
| **G3.2** | Two browsers open, both rate same song | No duplicate entries. Single rating record. | — |

---

## Part H: Integration & End-to-End Scenarios

### H1: Full Workflow
**Risk:** Feature works in isolation but breaks when combined with existing features.

| ID | Test Case | Acceptance Criteria | Pass? |
|---|---|---|---|
| **H1.1** | 1. Start with 50 unclassified songs. 2. Run pipeline. 3. Verify 15 genre playlists created. 4. Play genre playlist. 5. Rate songs 👍👎. 6. Restart server. 7. Verify ratings persisted, playlists intact. | All steps succeed. Data consistent throughout. | — |
| **H1.2** | Playing from AI playlist. Song ends, autoplay enabled. Next similar song plays from genre (not random). | Autoplay respects genre similarity. Queue managed. | — |
| **H1.3** | Radio stream playing. Switch to AI playlist. | Playback switches cleanly. No race condition. | — |

### H2: Cross-Client Consistency
**Risk:** Blazor and iOS show different playlist counts or ratings.

| ID | Test Case | Acceptance Criteria | Pass? |
|---|---|---|---|
| **H2.1** | Blazor: 150 songs classified. iOS: Fetch playlists. | iOS shows same playlists, same song counts. Data in sync. | — |
| **H2.2** | Blazor: Rate song 👍. Switch to iOS. | iOS: Shows correct rating state immediately. No lag. | — |

### H3: Boundary Conditions
**Risk:** Feature breaks with extreme data sizes.

| ID | Test Case | Acceptance Criteria | Pass? |
|---|---|---|---|
| **H3.1** | 10,000-song library. Run full classification & playlist generation. | Completes in reasonable time (<30 min). Memory usage stays <500MB. | — |
| **H3.2** | One song rated by 100+ users (simulated). | Feedback aggregation works. No slowdown. | — |
| **H3.3** | Playlist with 200 songs. Autoplay requests similar songs. | Calculation <500ms. Doesn't block playback. | — |

---

## Risk Summary & Test Execution Strategy

### High-Risk Domains (Test First)
1. **Restart safety (A1-A3)** — 12 tests — *Criticality:* CRITICAL
   - Without this, feature is unusable in production
   - Simulate crashes, power loss, process kills
2. **Incremental pickup (B1-B3)** — 12 tests — *Criticality:* CRITICAL
   - User adds 100 songs; AI must find & classify them
3. **Progress visibility (F1-F4)** — 13 tests — *Criticality:* HIGH
   - Kiosk mode (RPi) must show status; users need confidence feature is working

### Medium-Risk Domains
4. **Multi-genre classification (C1-C3)** — 14 tests — *Criticality:* HIGH
5. **Similarity & autoplay (D1-D3)** — 13 tests — *Criticality:* HIGH
6. **Feedback loop (E1-E4)** — 13 tests — *Criticality:* MEDIUM

### Lower-Risk Domains
7. **Data consistency (G1-G3)** — 8 tests — *Criticality:* MEDIUM
8. **E2E integration (H1-H3)** — 7 tests — *Criticality:* MEDIUM

### Execution Approach
- **Phase 1 (Implementation):** Wash/Kaylee build feature
- **Phase 2 (QA Validation):** Zoe runs high-risk domain tests manually (no automation framework yet)
- **Phase 3 (Regression):** If tests pass, mark feature "Ready for Release"
- **Phase 4 (Production Monitoring):** Track restart events, pipeline failures, user feedback

---

## Notes & Assumptions

### Assumed Decisions (to be Confirmed)
- **Genre count:** Fixed at 12-18 before classification (not dynamic)
- **Autoplay trigger:** Only when queue is empty AND autoplay enabled
- **Feedback scope:** Per-user (not global/aggregate ratings yet)
- **Similarity metric:** Jaccard index on genre set (shared / union)
- **Pipeline schedule:** Hourly or on-demand (trigger point TBD by Wash)
- **Recovery behavior:** Last known good state; no data recovery from partial writes
- **Offline mode (iOS):** Feedback cached locally, synced on reconnect (if not already decided)

### Test Environment Requirements
- SQLite database with test data: 50-200 songs, varied genres
- Blazor server running on RPi or desktop
- iOS app with connectivity to test server
- Kill/restart capabilities for crash simulation
- Database inspection tools (sqlite3, or SQL IDE)
- Manual timing tools (stopwatch or logging analysis)

---

**Next Step:** Implementation team reviews assumptions, confirms scope, and begins building. Once implementation is testable, Zoe runs validation against this matrix.


---


