# Project Context
- **Owner:** Jonathan Allen
- **Project:** HomeSpeaker — a home audio/music player system with Blazor WebAssembly frontend and .NET backend
- **Stack:** .NET 8 / C#, Blazor WebAssembly, ASP.NET Core, Bootstrap/Bootswatch CSS, Docker, SQLite
- **Created:** 2026-03-23

## Core Context

### SSR Migration Journey (Summarized from 2025-03-24 validation attempts)
Completed Blazor WebAssembly to Server-Side Rendering (SSR) migration over Q1 2026. 

**Validation Timeline:**
1. **QA Attempt #1 (2025-03-24):** Documentation phase; no code changes yet
2. **QA Attempt #2 (2025-03-24):** ✅ 95/100 score - Implementation validated; WebAssembly removed, gRPC sunset, REST API preserved, Interactive Server configured
3. **QA Attempt #3 (2025-03-24):** ✅ 97/100 score - Final validation post-Wash revision; 218 files changed, clean build, all components migrated
4. **QA Attempt #4 (2025-03-24):** ⚠️ Rejected - 3 pages missing rendermode directives (Folders, NightScout, AspireDashboard); simple one-line fix per page assigned

**Key Outcomes:**
- **Build:** Clean Release (0 errors), Dockerfile validated
- **Architecture:** `HomeSpeakerService` wraps backend; Interactive Server at app level; SignalR hub active
- **Compatibility:** All `/api/homespeaker/*` endpoints preserved for iOS client
- **Components:** 100+ Blazor components migrated; 11/14 routable pages have Interactive Server rendermode
- **Test Coverage:** Zero automated tests; manual smoke testing only
- **Status:** ✅ APPROVED & LIVE (post-rendermode fixes)

## Learnings
<!-- Recent entries below -->

### 2026-05-01: AI Playlists Readiness Assessment - NOT READY
**Status:** ⚠️ Developer-preview only
**Validated by:** Zoe

**What I verified:**
- `dotnet build HomeSpeaker.sln` succeeds.
- `dotnet test HomeSpeaker.sln` runs with no actual automated tests present.
- Existing smoke evidence only proves `/ai-playlists` and `/ai-status` load; it does not prove AI generation, feedback adaptation, or autoplay behavior.

**Blocking readiness findings:**
- iOS AI playlist decoding is wired to `TrackCount` instead of the server's camelCase `trackCount`, so real playlist payloads are likely to decode as empty.
- iOS AI status multiplies `percentComplete` by 100 even though the server already returns 0-100, so progress display is unreliable.
- Similar-song autoplay exists as an API endpoint, but I found no Blazor or iOS control that exposes it to users.
- Resume processing only nudges the worker; failed items are not re-queued, so transient AI failures remain stuck.
- Error handling is weak in user-facing flows: Blazor playlist/status pages collapse failures into empty/idle states, and iOS feedback/playlist actions mostly swallow errors.

**Release recommendation:**
- Do not treat this as trial-ready for real users until the iOS data-contract issues, retry/recovery behavior, and end-to-end validation of playlist generation / feedback / autoplay are completed.

### 2026-05-01: AI Library Enrichment E2E Smoke Test - PASSED
**Status:** ✅ All pages load successfully
**Branch:** master
**Commit:** `fc948ad`
**Validated by:** Zoe's automated browser testing

**Test Scope:**
Performed end-to-end smoke test after backend fixes were completed:
- `AiPlaybackSession` primary-key issue resolved
- Additional backend analyzer/runtime blockers fixed
- Solution build succeeds
- Server startup succeeds

**Validation Method:**
1. Built solution from repo root (`dotnet build HomeSpeaker.sln`)
2. Started server in detached mode (listens on http://0.0.0.0:5280)
3. Verified `/health` endpoint reports Healthy
4. Automated browser testing via Playwright (Chromium) for all required routes

**Test Results:**
All 6 pages loaded successfully (HTTP 200, no runtime errors):
1. ✅ `/` (Home) - HTTP 200
2. ✅ `/music` (Music) - HTTP 200
3. ✅ `/queue` (Queue) - HTTP 200
4. ✅ `/playlists` (Playlists) - HTTP 200
5. ✅ `/ai-playlists` (AI Playlists) - HTTP 200
6. ✅ `/ai-status` (AI Status) - HTTP 200

**Health Check:**
Server health endpoint confirmed:
- Status: Healthy
- Database check: 13.3ms (Healthy)
- Self check: 0.9ms (Healthy)

**Build Quality:**
- Build succeeded in 6.0s
- 20 warnings (non-blocking: nullability, unused usings, DateTime.Now ban)
- 0 errors

**What This Validates:**
- Blazor SSR pages render correctly
- New AI-related routes (`/ai-playlists`, `/ai-status`) are accessible
- Database migrations apply cleanly
- Server startup completes without crashes
- SignalR hub initializes (AnchorHub, player events)
- Background workers start (DailyAnchorWorker, AI processing heartbeat)
- Library sync completes (96 songs loaded)

**What This Does NOT Validate:**
- Interactive functionality (button clicks, form submissions)
- Real-time player updates via SignalR
- AI enrichment processing behavior
- Touch responsiveness on RPi hardware
- Playlist generation correctness
- API endpoint responses (only page rendering tested)

**Technical Notes:**
- Installed Playwright locally for testing (`@playwright/test` + Chromium)
- Server started successfully on first attempt (no port conflicts)
- All pages waited for `domcontentloaded` and 2s Blazor initialization
- No JavaScript exceptions or HTTP errors detected in page content

**Recommendation:** ACCEPT. All critical pages load successfully. Interactive behavior and AI functionality require manual validation or integration tests (future work).

### 2025-03-24: SSR Migration QA Attempt #4 (Final Interactive Server Re-Check) - REJECTED
**Status:** ❌ Failed - 3 pages missing @rendermode directive
**Branch:** copilot/ssr-server-interactive-migration
**Validated by:** Zoe's interactive Server verification

**Critical Finding:**
Three routable pages are missing `@rendermode InteractiveServer` directive, causing them to render as static SSR instead of Interactive Server:
1. `/folders` - Folders.razor (redirect page)
2. `/nightscout` - NightScout.razor (iframe wrapper with @code block)
3. `/aspire` - AspireDashboard.razor (iframe wrapper with @code block)

**Impact Analysis:**
- **Folders.razor**: Redirect-only page with `Nav.NavigateTo()` in `OnInitialized()` - needs Interactive Server for navigation to work
- **NightScout.razor**: Has `OnInitializedAsync()` and local state (`nightscoutUrl`) - needs Interactive Server for initialization
- **AspireDashboard.razor**: Has `OnInitializedAsync()` and local state (`aspireUrl`) - needs Interactive Server for initialization

**What Works (✅ Verified):**
1. ✅ **MainLayout**: Has `@rendermode InteractiveServer` (line 1)
2. ✅ **All 11 interactive pages**: Index, Queue, Music, Playlists, Streams, YouTube, RecentlyPlayed, Anchors, AnchorsEdit, Counter, FetchData all have `@rendermode InteractiveServer`
3. ✅ **Child components**: Properly inherit rendermode from parent (no directive needed)
4. ✅ **Program.cs**: `.AddInteractiveServerComponents()` and `.AddInteractiveServerRenderMode()` properly configured
5. ✅ **HomeSpeakerService**: Returns domain models (Song, PlayerStatus), no gRPC types
6. ✅ **No gRPC in Server2**: Zero references to `AddGrpc`, `MapGrpcService`, or `GrpcChannel`
7. ✅ **No gRPC in Shared**: HomeSpeaker.Shared.csproj has no gRPC packages (verified clean)
8. ✅ **WebAssembly removed**: No HomeSpeaker.WebAssembly directory or project reference
9. ✅ **REST endpoints intact**: `MapHomeSpeakerApi()` mapped at line 625 of Program.cs
10. ✅ **Build succeeds**: `dotnet build HomeSpeaker.sln` passes with 0 errors (19 warnings, non-blocking)
11. ✅ **_Imports.razor**: Imports `RenderMode` helpers via `static Microsoft.AspNetCore.Components.Web.RenderMode`

**Architecture Validation:**
- Interactive Server properly configured at application level
- 11 of 14 routable pages correctly opted into Interactive Server
- Child components (PlayControls, QueueItem, Song, etc.) correctly inherit from parent
- SignalR hub active for real-time updates
- No static SSR on main app routes (except the 3 missing pages)

**Recommendation:** REJECT. Assign to **Wash** to add `@rendermode InteractiveServer` to the 3 missing pages. Simple one-line fix per page.

### 2025-03-24: AI Playlists Feature — QA & Acceptance Matrix Definition
**Status:** ✅ Completed
**Deliverable:** `.squad/decisions/inbox/zoe-ai-playlists-qa-matrix.md`
**Test Case Count:** 77 across 8 risk domains

**Matrix Structure:**
- **Part A: Restart & Resume Safety** (12 tests) — Pipeline crash recovery, transaction rollback, graceful shutdown
- **Part B: Incremental Pickup** (12 tests) — New song detection, playlist updates, file handling edge cases
- **Part C: Multi-Genre Classification** (14 tests) — Genre assignment, 12-18 playlist creation, UI navigation
- **Part D: Similarity & Autoplay** (13 tests) — Similarity scoring, autoplay triggering, "Play Similar" mode
- **Part E: Feedback Loop** (13 tests) — Thumbs up/down capture, persistence, adaptation to ratings
- **Part F: Progress Visibility** (13 tests) — Blazor status page, iOS status page, completion notifications
- **Part G: Data Consistency** (8 tests) — Playlist-song linkage, genre stability, concurrency
- **Part H: Integration & E2E** (7 tests) — Full workflow, cross-client sync, boundary conditions

**Key Risk Findings:**
1. **CRITICAL:** Restart safety without transaction guarantee = feature unusable in kiosk (RPi restarts frequently)
2. **CRITICAL:** Incremental pickup must handle files in subdirectories, symlinks, permission errors gracefully
3. **HIGH:** Progress visibility essential on touch-first RPi kiosk (7" screen); users need confidence pipeline is working
4. **HIGH:** Multi-genre classification requires UI updates to both Blazor (NavMenu.razor, Playlists.razor) and iOS (PlaylistsView.swift, MoreView.swift)
5. **MEDIUM:** Feedback loop requires per-user rating storage; verify SQLite schema supports user_id or implicit-user context

**Assumed Decisions (flagged for team confirmation):**
- Genre count: Fixed (12-18) before classification, not dynamic
- Autoplay trigger: Queue empty AND autoplay enabled
- Feedback scope: Per-user (not aggregate yet)
- Similarity metric: Jaccard on genre set
- Pipeline schedule: Hourly or on-demand (TBD by Wash)
- Recovery: Last known good state, no partial write recovery

**Architecture Notes:**
- REST API: All endpoints use `/api/homespeaker/*` (iOS-safe)
- Blazor: Interactive Server components; Status page needs `@rendermode InteractiveServer`
- iOS: APIClient.swift handles JSON serialization; new endpoints needed: `POST /api/homespeaker/songs/{id}/feedback`, `GET /api/homespeaker/playlists/ai`, `GET /api/homespeaker/ai-status`
- Database: New tables needed: `SongGenres`, `GenrePlaylists`, `SongSimilarity`, `UserFeedback`, `PipelineStatus`

**Implementation Blockers (for team):**
1. Confirm genre model: Fixed list or dynamic tag system?
2. Confirm feedback model: Per-user or anonymous aggregate?
3. Confirm AI service integration: Azure OpenAI? Local model? Genre taxonomy source?
4. Confirm similarity algorithm: Custom scorer or library (e.g., Apache Lucene)?
5. Confirm pipeline schedule: Cron trigger? On-demand? Manual button?

**Next Steps:**
- Implementation: Wash defines database schema, pipeline trigger, and similarity algorithm
- Implementation: Kaylee adds Blazor UI (Playlists.razor, Status.razor, feedback buttons)
- Implementation: River adds iOS UI (PlaylistsView enhancements, Status screen, feedback UI)
- QA: Once implementation testable, Zoe validates against matrix (estimated 5-10 hours manual testing)


### 2026-05-01 — AI Playlists QA Strategy

**By:** Zoe  
**Status:** Ready for test implementation

Produced comprehensive QA matrix covering AI playlist generation, feedback mechanisms, edge cases, and performance benchmarks. Test strategy aligns with in-process architecture (no vector DB, OpenAI backend).

**Key outputs:**
- Full test coverage matrix (functional, edge case, performance)
- Test case definitions
- QA baseline established
- Ready for test automation

### 2026-05-01: Azure OpenAI Provider QA Validation - APPROVED
**Status:** ✅ Passed
**Validated by:** Zoe

**Validation results:**
1. `HomeSpeaker.Server2` built successfully with `dotnet build`.
2. Server started healthy with all AI configuration values forced blank via ephemeral environment variables.
3. Playwright smoke coverage passed for `/`, `/music`, `/queue`, `/playlists`, `/ai-playlists`, and `/ai-status` (all HTTP 200, no browser console or page errors).
4. `/ai-status` showed the provider-aware degraded summary: `AI provider is not configured. Set AI:OpenAI:ApiKey or AI:AzureOpenAI:Endpoint, ApiKey, and DeploymentName.`
5. Confirmed the old hardcoded OpenAI-only message was not present.
6. Additional startup check with dummy Azure OpenAI env vars (`Endpoint`, `ApiKey`, `DeploymentName`) succeeded; the app stayed healthy and `/ai-status` did not fall back to provider-missing or old OpenAI-only messaging.

**Not validated:**
- Real Azure OpenAI authentication or successful completions (dummy config only)
- Interactive playlist generation behavior beyond page-load smoke coverage

### 2026-05-01 — AI Readiness Review Finalized (Cross-team Synthesis)

**Verdict:** NOT READY for any user trial

**Team consensus (Zoe + Mal):**
- Developer-preview status only; do not attempt user trial in current state.
- iOS data-contract issues block any iOS exposure.
- Retry/recovery missing for failed analyses.
- Similar-song autoplay not exposed to users.
- Error handling weak in user-facing flows.

**Blocking issues for trial readiness:**
1. iOS playlist decoding uses `TrackCount` instead of server's camelCase `trackCount`
2. iOS status multiplies `percentComplete` by 100 (server 0-100 → display 0-10000)
3. Similar-song autoplay API exists but not exposed in Blazor/iOS UIs
4. Failed analyses not re-queued; transient failures strand tracks
5. Blazor/iOS error handling collapses to empty/idle states

**Required before trial:**
- Fix iOS data contracts
- Implement retry/recovery for failed items
- Expose and test similar-song autoplay flow
- End-to-end validation: generation → playlists → play → feedback → ranking

**Orchestration logs created:**
- `2026-05-01T155906Z-zoe.md` — QA findings and blocking issues
- `2026-05-01T155906Z-mal.md` — Mal's verdict and next steps
- Session log: `.squad/log/2026-05-01T155906Z-ai-readiness-review.md`
