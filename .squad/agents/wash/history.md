# Project Context
- **Owner:** Jonathan Allen
- **Project:** HomeSpeaker — a home audio/music player system with Blazor WebAssembly frontend and .NET backend
- **Stack:** .NET 8 / C#, Blazor WebAssembly, ASP.NET Core, Bootstrap/Bootswatch CSS, Docker, SQLite, gRPC, SignalR
- **Created:** 2026-03-23

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

### 2026-03-24 — Blazor WebAssembly to SSR Migration Analysis

**Task:** Analyze current hosting model and plan migration from Blazor WebAssembly to server-side rendering (SSR/Interactive Server).

**Current Architecture:**
- Blazor WebAssembly hosted by ASP.NET Core via `UseBlazorFrameworkFiles()` + `MapFallbackToFile("index.html")`
- gRPC-Web protocol for client-server communication (2 services, 45+ methods)
- HomeSpeaker.WebAssembly project (~40 Razor components, 10 client services)
- SignalR hub for Anchor real-time updates (already server-side)
- 25 REST endpoints for iOS/watchOS mobile app (must preserve)
- Docker deployment on Raspberry Pi (ports 80/443, Ubuntu user)

**Key Findings:**
1. **gRPC Services to Remove:**
   - `HomeSpeakerService` — 45 methods (music, playlists, YouTube, radio, player control)
   - `GreeterService` — 1 demo method
   - iOS app uses **ZERO gRPC** — all REST endpoints (verified via APIClient.swift)
   - Client-side gRPC wrapper services can be replaced with direct backend service injection

2. **REST Endpoints to Preserve:**
   - All 25 endpoints in `/api/homespeaker/*` (used by iOS/watchOS app)
   - No changes required — backend services already exist

3. **SignalR Hub Status:**
   - `/anchorHub` already server-side (no changes needed)
   - WebAssembly client (`AnchorSyncService`) will move to Server2 with minimal changes
   - Risk: Dual SignalR connections per user (Blazor circuit + Anchor hub) — acceptable for low-concurrency Pi deployment

4. **Server Configuration Changes:**
   - Remove: `UseBlazorFrameworkFiles()`, `UseWebAssemblyDebugging()`, `MapFallbackToFile()`, `UseGrpcWeb()`, `AddGrpc()`, `MapGrpcService<>()`
   - Add: `AddRazorComponents().AddInteractiveServerComponents()`, `MapRazorComponents<App>().AddInteractiveServerRenderMode()`
   - Remove NuGet: `Microsoft.AspNetCore.Components.WebAssembly.Server`, `Grpc.AspNetCore`, `Grpc.AspNetCore.Web`
   - Remove project reference: `HomeSpeaker.WebAssembly.csproj`

5. **Component Migration:**
   - Move 40 Razor components from WebAssembly → Server2 (`Pages/`, `Components/`)
   - Update namespaces: `HomeSpeaker.WebAssembly` → `HomeSpeaker.Server2`
   - Replace gRPC service injection with direct backend service injection (e.g., `HomeSpeakerClient` → `IMusicPlayer`, `PlaylistService`, `YoutubeService`)

6. **Service Layer Refactor:**
   - **Remove:** gRPC client wrappers (`HomeSpeakerService.cs` with `HomeSpeakerClient`)
   - **Remove (maybe):** `IBrowserAudioService`, `ILocalQueueService`, `IPlaybackModeService` — if local playback mode is deprecated
   - **Preserve:** `AnchorSyncService` (SignalR), `PlayerStateService` (convert to scoped), `ImagePickerService`, `YouTubeStateService` (convert to scoped)
   - **Replace:** Temperature/BloodSugar/Forecast wrappers with direct backend service injection (already exist as singletons)

7. **Docker Changes:**
   - Remove WebAssembly project from Dockerfile COPY statements
   - No changes to docker-compose.yml (same ports, volumes, env vars)

**Risks Identified:**
- **LOW-MEDIUM:** SignalR circuit management (dual connections, reconnection logic)
- **LOW-MEDIUM:** Prerendering issues (JS interop failures during SSR — add guards or disable prerendering)
- **MEDIUM:** Performance on Raspberry Pi (more server RAM/CPU usage vs. WebAssembly)
- **LOW:** Scoped vs. singleton service registration (PlayerStateService, YouTubeStateService must be scoped)
- **NONE:** iOS app compatibility (REST endpoints unchanged)
- **NONE:** Deployment workflow (browser refresh works identically)

**Security Improvements:**
- Better secret protection (no client-side API key exposure)
- Server-side input validation (all events server-side vs. client-side)
- Easier auth implementation (if added later)

**Effort Estimate:**
- Backend: ~11 hours (remove gRPC, add Blazor Server, refactor services, Docker, testing)
- Frontend: ~10 hours (move components, replace gRPC calls, fix prerender issues)
- Architecture: ~4 hours (decision-making, code review)
- Testing: ~6 hours (iOS app, REST endpoints, performance)
- **Total:** ~31 hours (~4 days solo, 1-2 days with team)

**Rollback Plan:** Git revert + redeploy (~10 minutes, zero data loss)

**Deliverable:** Created comprehensive decision file at `.squad/decisions/inbox/wash-blazor-ssr-migration-analysis.md` with 15+ sections covering:
- Current architecture
- gRPC services to remove (with line numbers)
- REST endpoints to preserve (with iOS evidence)
- SignalR hub status
- Server hosting changes (code samples)
- Component migration path (file lists)
- Service layer refactoring (DI changes)
- Docker/deployment changes
- Runtime risks (prerendering, circuits, performance)
- Security implications
- Testing strategy (5 phases)
- Open questions for team
- Effort estimate and rollback plan

**Recommendation:** PROCEED. Low-medium risk, clear migration path, significant architectural simplification.

**Next Actions:** Obtain team approval (Mal, Kaylee, Zoe), create feature branch, execute migration phases.

**Files Referenced:**
- `HomeSpeaker.Server2/Program.cs` (lines 34-35, 142, 152, 154, 190, 193-194, 664)
- `HomeSpeaker.Server2/Services/HomeSpeakerService.cs` (gRPC implementation)
- `HomeSpeaker.WebAssembly/Services/HomeSpeakerService.cs` (gRPC client wrapper)
- `HomeSpeaker.WebAssembly/Services/AnchorSyncService.cs` (SignalR client)
- `HomeSpeaker.Server2/Endpoints/HomeSpeakerRestEndpoints.cs` (25 REST APIs)
- `HomeSpeakerMobile/Shared/APIClient.swift` (iOS REST client)
- `HomeSpeaker.Shared/homespeaker.proto` (45 gRPC methods)
- `HomeSpeaker.Server2/Hubs/AnchorHub.cs` (SignalR hub)
- `HomeSpeaker.Server2/Dockerfile` (Docker build config)
- `docker-compose.yml` (deployment config)

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
