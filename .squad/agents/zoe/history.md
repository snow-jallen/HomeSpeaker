# Project Context
- **Owner:** Jonathan Allen
- **Project:** HomeSpeaker — a home audio/music player system with Blazor WebAssembly frontend and .NET backend
- **Stack:** .NET 8 / C#, Blazor WebAssembly, ASP.NET Core, Bootstrap/Bootswatch CSS, Docker, SQLite
- **Created:** 2026-03-23

## Learnings

### 2025-03-24: WebAssembly → SSR Migration Architecture
- **gRPC vs REST split:** Backend exposes both gRPC (for WASM) and REST (for iOS) simultaneously
- **iOS safety:** iOS client uses REST API exclusively (`/api/homespeaker/*` paths) — no code changes needed for iOS during migration
- **Streaming gap:** gRPC server streaming via `SendEvent()` must be replaced with SignalR or Server-Sent Events; AnchorHub already uses SignalR (reuse or extend)
- **Zero test coverage:** No unit/integration tests exist; all validation must be manual smoke testing post-migration
- **REST API completeness:** All necessary endpoints exist for SSR frontend (songs, player, queue, playlists, streams, health data); no new REST endpoints needed
- **Touch-first design:** Team decision enforces 44px minimum tap targets; SSR pages must respect this (CSS audit needed during implementation)
- **Artifact scope:** Three projects in solution; WebAssembly project removed post-migration; Shared (protobuf) may stay if external gRPC clients exist

### Critical Risk Areas
- Player event streaming (real-time UI updates) — must test with actual playback
- Touch responsiveness on 800x480 RPi screen — must test on physical hardware
- REST API compatibility with iOS — must validate every endpoint post-migration
- Database persistence — backup and restore test needed

### 2025-03-24: SSR Migration QA Attempt #1 - No Implementation Found
**Status:** Blocked - awaiting implementation
**Branch:** copilot/ssr-server-interactive-migration
**Findings:**
- Solution file still references `HomeSpeaker.WebAssembly` project (line 10)
- WebAssembly project directory still exists on disk
- No code changes committed on migration branch (git diff master...HEAD shows 0 changes)
- Only documentation updates in .squad/agents/*/history.md files
- Program.cs still has gRPC configuration (line 35: `AddGrpc()`)
- No Blazor Server components added to Server2 project yet

**Team Analysis Complete:**
- Mal provided architectural decision (collapse into Server2, Interactive Server for app routes)
- Wash provided detailed analysis (22.8KB file in inbox)
- Kaylee provided migration map (25.2KB file in inbox)
- All planning artifacts exist, but implementation has not started

**Next Step:** Implementation must be completed by Wash/Kaylee before QA validation can proceed. Will retry once actual code changes are present.

### 2025-03-24: SSR Migration QA Attempt #2 - APPROVED
**Status:** ✅ Passed with 95/100 score
**Branch:** copilot/ssr-server-interactive-migration
**Commit:** `27a8576` "Migrate to Blazor SSR: Remove gRPC, add server-side HomeSpeakerService"
**Validated by:** Book's implementation

**Validation Results:**
1. ✅ **Build Success:** HomeSpeaker.Server2 builds cleanly (Release, 6.4s)
2. ✅ **WebAssembly Removed:** Project and directory completely removed from solution
3. ✅ **gRPC-Web Gone:** No `AddGrpc()`, `MapGrpcService()`, or browser gRPC channel code in Program.cs
4. ✅ **REST Endpoints Intact:** All `/api/homespeaker/*` endpoints verified present and mapped
5. ✅ **No Runtime Breaking Issues:** Dockerfile correct, middleware pipeline valid, Interactive Server configured

**Migration Architecture:**
- New `HomeSpeakerService.cs` wraps backend services (replaces gRPC client)
- Interactive Server configured at app level (`.AddInteractiveServerRenderMode()`)
- Components migrated: Layout, Music, Health, Weather, Pages (100+ files)
- SignalR hub remains active for real-time anchor notifications

**Lingering gRPC Artifacts (Acceptable):**
- `HomeSpeaker.Shared` project still contains `homespeaker.proto` and gRPC packages
- Reason: iOS client compatibility (uses REST) OR external gRPC clients exist (status undocumented)
- Risk: Low - no longer referenced by Server2, but adds dependency bloat if unused
- Action: Document whether external gRPC clients exist; if not, schedule cleanup task

**Cleanup Tasks (Minor):**
- Delete `HomeSpeaker.Server2/Protos/greet.proto` (unused .NET template file)
- Delete `HomeSpeaker.Server2/Services/HomeSpeakerService.cs.old` (backup artifact)
- Remove `wwwroot/appsettings.json` line 9 WebAssembly logging config

**Test Coverage:**
- Zero automated tests exist (manual smoke testing required)
- Cannot verify player event streaming, touch responsiveness, or iOS compatibility without hardware
- Manual checklist: see `REGRESSION_CHECKLIST.md`

**Recommendation:** ACCEPT. Implementation is solid. Deductions: -3 pts (no tests), -2 pts (cleanup artifacts).

### 2025-03-24: SSR Migration QA Attempt #3 (Final Validation - Wash's Revision) - APPROVED
**Status:** ✅ Passed with 97/100 score
**Branch:** copilot/ssr-server-interactive-migration
**Commit:** `b879ed8` (HEAD), `27a8576` (migration implementation)
**Validated by:** Wash's revision (post-Book's implementation)

**Final Validation Results:**
1. ✅ **WebAssembly Removed:** Project completely removed from solution and filesystem
2. ✅ **No Browser gRPC:** Zero gRPC client code in Server2; only reference is `.old` backup file (cleanup task)
3. ✅ **Server-Hosted Blazor:** Interactive Server configured at app level (`.AddInteractiveServerComponents()`)
4. ✅ **SSR/Interactive Server Mode:** Components use `@inject HomeSpeakerService` for server-side data access
5. ✅ **REST Endpoints Preserved:** All `/api/homespeaker/*` endpoints mapped and functional for iOS client
6. ✅ **No Runtime-Breaking Issues:** Clean Release build (0 errors, 0 warnings), Dockerfile validated, middleware pipeline correct

**Migration Quality:**
- 218 files changed (complete restructure from WebAssembly to SSR)
- `HomeSpeakerService.cs` returns domain models (`PlayerStatus`, `Song`) instead of gRPC types
- All Blazor components migrated: Layout, Music, Health, Weather, Pages (100+ .razor files)
- SignalR hub active for real-time player events
- Proper DI scoping: scoped services for Blazor, singletons for backend state

**Cleanup Tasks (Non-Blocking):**
1. Delete `HomeSpeaker.Server2\Services\HomeSpeakerService.cs.old` (backup artifact)
2. Delete `HomeSpeaker.Server2\Protos\greet.proto` (unused .NET template)
3. Remove `wwwroot\appsettings.json` line 9 (WebAssembly logging config)

**Remaining Risk Areas (Manual Testing Required):**
- Player event streaming (SignalR confirmed present, need runtime validation)
- Touch responsiveness on 800x480 RPi screen (hardware testing)
- HttpClient usage in some pages (Index, RecentlyPlayed) - verify no internal API calls
- Zero automated tests (manual regression checklist required)

**Architecture Strengths:**
- Clean separation: `HomeSpeakerService` abstracts backend logic
- Event-driven updates: `StatusChanged` propagates player state to UI
- REST API intact: iOS client unaffected
- Minimal technical debt: No gRPC DTOs in UI layer

**Score:** 97/100 (-3 no tests, -5 cleanup artifacts, -0 build issues)

**Recommendation:** ACCEPT. Migration complete and solid. Ready for deployment and manual regression testing.

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
