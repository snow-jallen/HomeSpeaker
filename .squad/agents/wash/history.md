# Project Context
- **Owner:** Jonathan Allen
- **Project:** HomeSpeaker — a home audio/music player system with Blazor WebAssembly frontend and .NET backend
- **Stack:** .NET 8 / C#, Blazor WebAssembly, ASP.NET Core, Bootstrap/Bootswatch CSS, Docker, SQLite, gRPC, SignalR
- **Created:** 2026-03-23

## Core Context

### Architecture (2026-03-23)
HomeSpeaker backend: .NET 8 with ASP.NET Core, Blazor WebAssembly UI, SQLite database, gRPC services, Docker deployment on Raspberry Pi. External integrations: YouTube (YoutubeExplode), Govee API (temp/sensors), Nightscout (blood sugar), Open-Meteo (weather).

### Security Audit (2026-03-23)
Critical findings: No auth/authz layer implemented. All HTTP/gRPC/SignalR endpoints open. Security improvement: Add OAuth2/JWT before production. DOS/traversal/data validation risks identified.

### WASM-to-SSR Migration Audit (2026-03-24, 2026-04-29)
Analyzed WebAssembly to server-side rendering migration. Rejected: half-completed migration left WASM in place. Current state: Build failures in Server2 (93 errors), architecture inconsistency.

### AI Playlists Backend (2026-05-01)
Mapped AI integration points for OpenAI-backed playlisting. Use in-process service layer with Microsoft.Extensions.AI, background worker for batch analysis, SQLite persistence keyed on SongPath. No vector database.

## Learnings
<!-- Recent entries below -->

### 2026-05-14 — Mobile release fixes for Siri shortcuts and offline download status
Locked the fixed-command Siri path to exact backend targets instead of alias search where the target is already decided: `PlayFunMusicOnHomeSpeakerIntent` now calls AI genre `family-singalong` directly, and the shared `PlayerStatus.quietDownVolume` helper is the contract for halving volume without collapsing non-zero levels to zero. `Quiet Down` on both iOS intents and the watch widget must use that shared helper so 1 stays 1 and higher values still halve predictably.

Offline download summaries must count `failed` separately from `pending`; pending is only `.pending`, `.queued`, or `.downloading`. Key release paths: `HomeSpeakerMobile\iOS\Intents\HomeSpeakerIntents.swift`, `HomeSpeakerMobile\watchOS\Widget\WidgetIntents.swift`, `HomeSpeakerMobile\Shared\Models.swift`, `HomeSpeakerMobile\iOS\OfflineDownloadsStore.swift`, and `HomeSpeakerMobile\iOS\Views\OfflineDownloadsView.swift`. Host-side validation available here remains `dotnet build HomeSpeaker.sln`, `dotnet test HomeSpeaker.sln`, and `dotnet ef migrations has-pending-model-changes`; Swift/Xcode toolchains are not installed on this Windows host.

### 2026-05-06 (COMPLETED): Music page play replaces queue
Music-page Play dropdown now routes through dedicated `PlaySongsAsync` path that stops playback, clears queue, starts first song immediately, and queues remaining songs. Preserves add-to-queue append semantics through separate code path. Coordinated with Kaylee on AI playlist playback. Build: ✅ SUCCESS

### 2026-05-06 (COMPLETED): AI genre entry sanitization
Bounded JSON-node normalization pass now runs only over `songs[*].genres` when typed deserialization fails. Canonicalizes known genre keys, coerces safe numeric score/rank strings, drops malformed items. Preserves existing numeric repair and truncated-json fallback unchanged. Logs all changes for observability.

### 2026-05-06 — Music page play replaces queue through a dedicated server-side path
The music page's shared multi-song play dropdown was stopping playback and then enqueueing each selected song, which preserved any stale queued entries because `Stop()` does not clear the queue. The safe fix is to route multi-song server play through a dedicated `HomeSpeakerService.PlaySongsAsync()` path that stops playback, clears the queue, starts the first song, and only uses enqueue for the remaining tracks, while the existing plus-menu add-to-queue flow keeps append semantics.

### 2026-05-06 — AI Genre Entry Sanitization
`AiMusicAnalyzer` was still deserializing the full model payload straight into `AiBatchAnalysisResponse`, so schema-valid JSON with a bad `songs[*].genres` shape/value (stringified numbers, non-object entries, wrong `genres` container) bypassed the numeric repair pass and never qualified for truncated-json fallback. The production fix is a bounded DOM normalization pass that runs only for `.genres` parse failures, canonicalizes known genre keys, coerces safe score/rank strings, drops malformed genre entries or invalid genre containers, and logs exactly what was changed before retrying typed deserialization.

### 2026-05-03 — AI Playlist Genre Key Deduping
`AiMusicCatalogService` summary queries can return separate grouped rows for `choral`/`CHORAL` because SQLite grouping and the composite `{ SongPath, GenreKey }` key are case-sensitive. Collapsing definitions and grouped aggregates with `StringComparer.OrdinalIgnoreCase`, plus case-insensitive playlist lookups, keeps AI playlists rendering and preserves partial results when dirty genre data slips in.

### 2026-05-02 — AI Playlist Detail Payload Enrichment
Extended the existing AI playlist detail flow instead of creating a second details endpoint. `AiPlaylistDto` now carries `Tracks`, where each entry includes the song plus its selected-genre score, rank, why text, and stored marker values/confidence, while the legacy `Songs` list remains populated for older callers.

### 2026-05-02 — AI Truncated JSON Fallback
Truncated AI batch payloads that fail at paths like `$.songs[4].genres[2]` are structural end-of-data errors, so the existing numeric repair path cannot safely fix them. `HomeSpeaker.Server2` now tightens the prompt/output budget and falls back to per-song analysis only for classified truncated-JSON batch failures, which keeps one malformed batch from stranding every claimed track.

### 2026-05-02 — EstateMapper IDbContextFactory Disposal Diagnosis
Diagnosed disposed DI scope issue in EstateMapper's IDbContextFactory<EstateContext> usage. Root cause: CreateDbContext fails because constructor dependencies resolve through dead IServiceProvider. The DI container scope is disposed before the factory attempts instantiation. Diagnostic completed, implementation pending team decision.

### 2026-05-01 — AI Retry Cooldown + Explicit Request Timeout
AI music analysis now re-queues failed work items automatically after a short cooldown instead of leaving them stranded in `Failed` until manual DB cleanup. The batch default was reduced to 6, and model calls now enforce a 200-second linked cancellation timeout inside `AiMusicAnalyzer`, which applies consistently across whichever OpenAI provider is behind `IChatClient`.

### 2026-05-02 — Dual OpenAI Provider Config
Updated `HomeSpeaker.Server2` AI wiring to support either public OpenAI or Azure OpenAI from the existing `AI` options section. `AI:AzureOpenAI` now uses `Endpoint`, `ApiKey`, and `DeploymentName`, Azure is preferred when fully configured, and degraded-status messaging points at the active/missing provider instead of always blaming `AI:OpenAI:ApiKey`.

### 2026-05-02 — AI Playlists Backend Slice
Implemented AI playlist backend slice in Server2: AI options + OpenAI `IChatClient` wiring, AI entities + seeded genres, background analysis worker, similarity/autoplay, feedback capture, and `/api/ai/*` endpoints. Player status now includes nullable AI context via shared DTOs. Migration was created manually because the existing PlayControls razor build error blocks `dotnet ef`.

### 2026-05-02 — Azure OpenAI Support (Request from Jonathan Allen)
Implemented dual OpenAI provider configuration: added `AI:AzureOpenAI` section with `Endpoint`, `ApiKey`, `DeploymentName`. Runtime preference: Azure when fully configured, fallback to public OpenAI. Updated degraded-status messaging to reflect active provider. Validated by Zoe: build clean, server startup healthy, smoke tests passing on /, /music, /queue, /playlists, /ai-playlists, /ai-status. ✅ APPROVED

### 2026-05-02 — AI Timeout Message Path
The user-facing “failed on attempt 1” activity message is composed in `AiMusicCatalogService` from `AiProcessingWorkItem.LastError`, while the underlying timeout text comes from the exception captured in `AiMusicAnalysisWorker` around `AiMusicAnalyzer.AnalyzeBatchAsync()`. The app does not implement its own model-call retry policy here; with Azure configured it constructs `AzureOpenAIClient` with default options, so Azure SDK retry/timeout behavior surfaces directly, and `/api/ai/process/resume` only wakes the worker—it does not requeue already failed items.

### 2026-05-01 — AI Timeout Diagnosis (Diagnostic)
Traced Azure/OpenAI timeout message to Azure SDK retry timeout during chat request in AiMusicAnalyzer.AnalyzeBatchAsync(). Resume endpoint does not requeue failed items, allowing timeouts to persist. Root cause identified: No custom retry policy wrapping AzureOpenAIClient. Diagnostic only; no code changes made.

### 2026-05-01 — AI Retry/Timeout Fix Cycle (Wash → Zoe → Mal → Approved)

Implemented auto-requeue mechanism for failed AI music analysis work items. Initial implementation rejected by Zoe due to end-to-end timeout ineffectiveness. Mal revised provider-level timeout wiring via AzureOpenAIClientOptions and OpenAIClientOptions to properly configure SDK transport. Zoe revalidated and approved. Final state: auto-requeue enabled, batch size 6, 200s timeout enforced at both analyzer and transport layers.

### 2026-05-02 — AI JSON Numeric Repair

Traced malformed-model failures to `AiMusicAnalyzer.AnalyzeBatchAsync()` deserializing `response.Text` directly into `AiBatchAnalysisResponse`, so invalid JSON numbers like `01`, `.4`, `0.`, or `0,4` at paths such as `$.songs[5].energy` abort the whole batch. Tightened the prompt to demand valid JSON numerics and added a narrow repair pass that only normalizes known numeric fields before deserialization, with warning/error logging that preserves the failing path and response context. Zoe validated with smoke tests and numeric repair validation. ✅ APPROVED for production.

### 2026-05-02 — AI Playlist Detail Payload Enrichment (Completed)
Extended the existing AI playlist detail flow (`/api/ai/playlists/{genreKey}`) to include per-track scoring metadata. `AiPlaylistDto.Tracks` now carries `Song`, `GenreScore`, `GenreRank`, `Why` text, and `Markers[]` (key/value/confidence). Legacy `Songs` list remains populated for backward compatibility. Reused existing endpoint rather than creating a second details API, avoiding duplicate contracts. Validated by Zoe: build clean, all pages load, scoring data visible. ✅ APPROVED & COMPLETE

### 2026-05-14 — Offline mobile download manifest
Implemented offline mobile support as a small server-side manifest, not a separate sync subsystem. Persist only offline selection rules in SQLite (`OfflineDownloadTargets`) and resolve them against the live library on demand so artist/album/song marks stay durable even if runtime `SongId` values move.

The mobile contract lives in shared DTOs (`OfflineDownloadManifestDto`, `OfflineDownloadTargetDto`, `OfflineDownloadSongDto`) and the server flow lives in `HomeSpeaker.Server2\Services\OfflineDownloadService.cs` plus `/api/homespeaker/offline*` endpoints. Downloadable media is exposed through a validated `/api/homespeaker/offline/media?songPath=...` endpoint with range support, ETag, and last-modified headers, using `Song.Path` as the durable content key.
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

## Learnings
<!-- Append new learnings below. Each entry is something lasting about the project. -->

### 2025-03-23 — Security Audit
**Architecture Overview:**
- Blazor WebAssembly frontend served by ASP.NET Core backend (.NET 8)
- Backend: gRPC services (HomeSpeakerService, GreeterService) + REST APIs + SignalR hubs
- Database: SQLite with Entity Framework Core (MusicContext)
- External integrations: YouTube (YoutubeExplode), Govee API (temperature), Nightscout (blood sugar), Open-Meteo (weather)
- Deployment: Docker Compose with 3 containers (homespeaker, aspire dashboard, airplay-receiver)
- Audio: Linux SoX player + PulseAudio, supports AirPlay via shairport-sync

**Authentication/Authorization:**
- **NONE CURRENTLY IMPLEMENTED** — This is the #1 security issue
- No [Authorize] attributes anywhere
- No authentication middleware configured
- All endpoints (HTTP/gRPC/SignalR) are completely open
- User IDs are client-controlled strings (not validated against auth)

**Key Backend Files:**
- `HomeSpeaker.Server2/Program.cs` — Main entry point, all HTTP API definitions (lines 189-617)
- `HomeSpeaker.Server2/Services/HomeSpeakerService.cs` — gRPC service implementation (music control, playlists, YouTube)
- `HomeSpeaker.Server2/Hubs/AnchorHub.cs` — SignalR hub for real-time anchor updates
- `HomeSpeaker.Server2/Services/AnchorService.cs` — Anchor management (habit tracking system)
- `HomeSpeaker.Server2/Data/MusicContext.cs` — EF Core DbContext with 8 entities
- `HomeSpeaker.Server2/Services/YoutubeService.cs` — YouTube video downloading via YoutubeExplode
- `HomeSpeaker.Server2/Services/TemperatureService.cs` — Govee smart sensor integration
- `HomeSpeaker.Server2/Services/BloodSugarService.cs` — Nightscout CGM data integration
- `HomeSpeaker.Server2/Services/RadioStreamService.cs` — Internet radio stream management

**Data Models (SQLite):**
- Songs/Playlists — Music library management
- RadioStreams — Internet radio stations with favicons
- Impressions — Play history tracking
- Thumbnails — Album artwork cache
- AnchorDefinitions/UserAnchors/DailyAnchors — Habit tracking system with temporal records

**Security Patterns Found:**
- No authentication/authorization (critical gap)
- No rate limiting
- SQL injection protection via EF Core (good), except hardcoded PRAGMA statements (safe)
- Path operations use Path.Combine (good) but lack traversal validation (bad)
- File uploads have size limits (2MB) but weak content validation
- SSL bypass for internal backlight controller (192.168.1.111)
- Cache management endpoints unprotected (DoS risk)
- Health data (blood sugar, temperature) exposed without auth
- Docker runs as non-root user (good)
- Certificate file has no password protection

**External API Dependencies:**
- Govee API (Temperature:ApiKey) — smart sensor data
- Nightscout (NIGHTSCOUT_URL) — diabetes CGM data (PHI)
- Open-Meteo — weather forecasts (public API, no key)
- YouTube (via YoutubeExplode library) — video downloads
- DuckDuckGo + Wikipedia — image search for radio stream icons

**Configuration Patterns:**
- appsettings.json for defaults
- Environment variables override config (docker-compose.yml)
- .env.example template (good), real .env excluded from git (good)
- ConfigKeys.cs defines constant keys
- IConfiguration injected throughout

**Deployment Architecture:**
- Production: Docker Compose on Raspberry Pi (Ubuntu user)
- Volume mounts: /music (media + database), /certs (TLS), /sys/class/backlight (hardware)
- Ports: 80 (HTTP), 443 (HTTPS), 18888 (Aspire dashboard)
- HTTPS via Tailscale certificates (refresh-cert.sh)
- Audio via /dev/snd device passthrough + PulseAudio socket

**Key Findings:**
- Well-structured code with good separation of concerns
- Proper async/await patterns throughout
- Good use of caching (MemoryCache) for external APIs
- Entity Framework used correctly (AsNoTracking for reads)
- BUT: Security fundamentals missing — needs auth/authz layer before production use

## Cross-Team Updates (2026-03-23)
**From mal:** Implemented repeat mode, sleep timer, recently played, and keyboard shortcuts across ~15 files. Feature-complete and ready for production.
**From kaylee:** Completed full UI redesign with Darkly theme and touch optimization for RPi 7` 800x480. Bottom navigation, WCAG AAA touch targets, momentum scrolling. Interface production-ready.
**From scribe:** Orchestration logs created, squad decisions consolidated, cross-team communication established. Ready for public release deployment.

## Cross-Team Updates (2026-03-24)
**From kaylee:** Removed redundant quick-link buttons from home page. Compacted Now Playing card to prioritize health data displays (80px → 56px, font sizes reduced 1.4rem → 1.1rem title, 1rem → 0.875rem artist). Changes scoped to avoid sidebar impact. Touch targets preserved.
**From scribe:** Orchestration logs finalized, decisions merged into primary file, inbox cleared, git commit staged.

### 2025-03-23 — Deployment Workflow: Browser Auto-Refresh Fix

**Problem:** The GitHub Actions deploy workflow couldn't refresh the kiosk-mode Chromium browser after deployment. The `xdotool key F5` command failed silently due to X11 permission issues — the self-hosted runner (running as a service user) couldn't access the X display owned by the desktop session user.

**Root Cause:** X11 display `:0` requires `XAUTHORITY` environment variable pointing to the `.Xauthority` cookie file. Without it, xdotool gets "Can't open display" permission denied errors. The `continue-on-error: true` flag hid these failures.

**Solution Implemented:** Multi-strategy fallback approach in `.github/workflows/deploy.yml`:

1. **Strategy 1 (Primary):** Chrome Remote Debugging Protocol — If Chromium is running with `--remote-debugging-port=9222`, use HTTP API to trigger `location.reload()`. This bypasses X11 permissions entirely.
   
2. **Strategy 2 (Secondary):** xdotool with proper XAUTHORITY — Search for `.Xauthority` file in `/home/piuser` and `/run/user`, export it, then run `xdotool key F5`.
   
3. **Strategy 3 (Fallback):** xdotool with hardcoded path — Try `/home/piuser/.Xauthority` directly (works if runner is piuser).

**Changes Made:**
- Enhanced "Wait for services" step to actively poll `https://localhost/` with curl (12 attempts × 5s = 60s max wait)
- Replaced blind `xdotool` call with 3-strategy approach with logging
- Removed `continue-on-error: true` — failures now properly reported (exit 1 if all strategies fail)
- Added clear console output showing which strategy succeeded/failed

**One-Time Pi Setup (Optional but Recommended):**
To enable Strategy 1 (most reliable), modify the Chromium kiosk launch command to include:
```bash
chromium-browser --kiosk --remote-debugging-port=9222 <url>
```
This allows the deploy workflow to refresh the browser without any X11 permissions.

**Deployment Pattern:**
- Both `kitchen` and `upstairs` runners will try all 3 strategies
- If Strategy 1 works on one Pi but not the other, that's fine — the fallback chain handles it
- Failures are now visible in GitHub Actions logs (search for "⚠ All refresh strategies failed")

**Security Note:** Remote debugging port (9222) is only accessible via localhost — no external exposure.
