# Project Context
- **Owner:** Jonathan Allen
- **Project:** HomeSpeaker — a home audio/music player system with Blazor WebAssembly frontend and .NET backend
- **Stack:** .NET 8 / C#, Blazor WebAssembly, ASP.NET Core, Bootstrap/Bootswatch CSS, Docker, SQLite
- **Created:** 2026-03-23

## Core Context

### SSR Migration & Validation (2026-03-24 — Q1 2026 Completed)
Blazor WebAssembly to Server-Side Rendering migration completed over Q1 2026. Four QA validation attempts; final approval post-rendermode fixes. Build succeeds (0 errors, 20 warnings), all pages accessible, 100+ Blazor components migrated, 11/14 pages with Interactive Server rendermode. Zero automated tests; manual smoke testing only. Status: ✅ APPROVED & LIVE.

### AI Playlists Feature Matrix (2026-05-01)
Comprehensive QA matrix defined covering 8 risk domains (restart safety, incremental pickup, multi-genre classification, similarity & autoplay, feedback loop, progress visibility, data consistency, E2E integration). 77 total test cases. Key risks identified: CRITICAL on restart safety (RPi kiosk needs transaction guarantee), CRITICAL on incremental pickup (new file detection), HIGH on progress visibility (RPi touch users need feedback), HIGH on similarity/autoplay (user exposure), MEDIUM on data consistency. All blocking issues resolved through team implementation cycle (Wash + Zoe validation) by May 2. Final status: Production-ready with telemetry oversight.

### AI Readiness Cycle (2026-05-01 → 2026-05-02)
Initial readiness assessment identified critical iOS data-contract issues (TrackCount vs trackCount, percentComplete scaling), missing autoplay UX, and weak error handling — NOT READY for trial. Team implemented fixes: Wash completed retry/recovery, JSON repair, and Azure OpenAI support; numeric JSON normalization validated. Zoe re-validated end-to-end after each fix cycle. Final state: AI features smoke-tested and approved for production. **May 2 Status: All blocking issues resolved; production-ready.**


## Learnings
<!-- Siri/Offline and release work below -->

### 2026-05-14: Siri Commands + Offline Downloads QA Assessment - REJECTED
**Status:** ❌ Rejected for requested scope
**Validated by:** Zoe

**What I verified:**
- `dotnet build D:\homespeaker\HomeSpeaker.sln` succeeds.
- `dotnet test D:\homespeaker\HomeSpeaker.sln` completes, but there are still no automated test projects in the solution, so it only proves the solution builds.
- Current Siri coverage is limited to content-picking App Shortcuts in `HomeSpeakerMobile\iOS\Intents\HomeSpeakerShortcuts.swift` and `HomeSpeakerMobile\iOS\Intents\HomeSpeakerIntents.swift` for artist/album/playlist/AI playlist/stream playback.
- The requested Siri command set is not implemented: there are no dedicated discoverable intents for **next song**, **fun music**, **hymns**, **quiet down**, or **stop**.
- Current volume control is only an absolute slider/API call (`setVolume(level)`); I found no atomic "cut current volume in half" workflow.
- Offline mobile downloads are not implemented for the library: no artist/album/track offline flags, no local download manager, no download state in `Models.swift`, and no management UI in `MusicLibraryView.swift` / `NowPlayingView.swift`.

**Blocking findings:**
- The current Siri approach still depends on free-form `MediaQueryEntity` phrases plus the app name, which does not match the user's request for short, explicit commands and remains high-risk for recognition ambiguity.
- `parseContentAndServer()` only parses `" on "` server hints, while the shipped shortcut phrases now use `" in "`, so the parser/phrase contract is already out of sync.
- Offline playback today streams from `/api/music/{songId}` via `LocalPlayer`; there is no persisted on-device media store or download lifecycle to validate.

**High-risk manual cases to run once implementation lands:**
- Siri: "next song", "play fun music", "play hymns", "quiet down", and "stop" with the app open, closed, and after device restart.
- Siri ambiguity: similar phrases with background noise, alternate wording, and no explicit app name.
- Quiet-down behavior when volume is odd, already low, already zero, or server status fetch fails.
- Offline downloads: mark/unmark song, album, and artist; duplicate downloads; partial downloads; cancel/retry; delete while playing; out-of-space; server unreachable; stale metadata after library edits.
- Download management: mixed states (queued/downloading/downloaded/failed), bulk delete, selective delete, and correct fallback between offline and server playback.

**Revision recommendation:**
- Route Siri command revision to **Kaylee** (not the current shortcut author) so the app gets explicit App Intents for the requested commands instead of generic media-query phrases.
- Route offline-download design/implementation to **Mal + Kaylee** for a concrete download-state contract and client workflow before QA re-validation.

### 2026-05-14: Siri Commands + Offline Downloads Final Review - REJECTED
**Status:** ❌ Rejected for final requested scope
**Validated by:** Zoe

**What I verified:**
- `dotnet build D:\homespeaker\HomeSpeaker.sln` succeeds.
- `dotnet test D:\homespeaker\HomeSpeaker.sln` succeeds, but there are still no real automated test projects in the solution.
- Runtime smoke on Windows succeeds for the server paths I can exercise here: `/health`, `/api/homespeaker/offline`, `/`, `/music`, and `/playlists` all returned HTTP 200.
- The iOS Siri/App Intents implementation is now explicit and app-scoped in code for the requested commands: next song, play fun music, play hymns, quiet down, and stop.
- The iPhone UI now exposes offline marking for artists/albums/tracks plus a dedicated Offline Downloads management screen under More / Library.
- `LocalPlayer` is wired to prefer a downloaded local file before falling back to `/api/music/{songId}`.

**Blocking finding:**
- The offline workflow still stores durable local state by **`songId`**, not by **`Song.Path`**. In this repo, `SongId` is assigned from the current in-memory scan order (`OnDiskDataStore` sets `song.SongId = songs.Count`), so adding/removing/rescanning library items can shift IDs. Because `OfflineDownloadsStore` names files and selections by `songId`, and `LocalPlayer` resolves offline files by `songId`, a rescan can make a saved download disappear or, worse, attach the wrong local audio file to a different song. That breaks the requested offline reliability and does not match Mal's accepted durable-key decision.

**Artifacts rejected for revision:**
- `HomeSpeakerMobile\iOS\OfflineDownloadsStore.swift` → **Kaylee**
- `HomeSpeakerMobile\iOS\LocalPlayer.swift` → **Kaylee**
- `HomeSpeakerMobile\iOS\Views\MusicLibraryView.swift` → **Kaylee**

**Acceptable limitations noted:**
- I could not run Xcode/iOS simulator validation from this Windows host, so Siri invocation and on-device offline playback remain code-reviewed rather than device-proven in this session.

### 2026-05-14: Offline Mobile Re-Review - APPROVED WITH CAVEAT
**Status:** ✅ Approved for the offline durable-key regression scope
**Validated by:** Zoe

**What I verified:**
- `dotnet build D:\homespeaker\HomeSpeaker.sln` succeeds.
- `dotnet test D:\homespeaker\HomeSpeaker.sln` succeeds, but there are still no real automated test projects in the solution.
- Durable offline manifest records, track selections, and downloaded-file lookup now key off `Song.path` in `HomeSpeakerMobile\iOS\OfflineDownloadsStore.swift`.
- Local playback now prefers a downloaded file by `Song.path` plus `connection.id` before falling back to `/api/music/{songId}` in `HomeSpeakerMobile\iOS\LocalPlayer.swift`.
- Library and management UI wiring remains present in `HomeSpeakerMobile\iOS\Views\MusicLibraryView.swift`, `HomeSpeakerMobile\iOS\Views\MoreView.swift`, `HomeSpeakerMobile\iOS\Views\OfflineDownloadsView.swift`, and `HomeSpeakerMobile\iOS\HomeSpeakerApp.swift`.

**Acceptable residual limitation:**
- Legacy migration from the old `songId`-keyed manifest is only best-effort because the old key was scan-order based. If a library changed before the first post-upgrade refresh, some pre-existing legacy selections/downloads may not map perfectly. Going forward, newly persisted offline state is using the durable path key the team asked for.
- I still cannot run Xcode/iOS simulator validation from this Windows host, so on-device playback and download lifecycle remain code-reviewed rather than device-proven here.

### 2026-05-14: Final Release Review - Offline Contract Gap
Rejected the final release pass because the new server-side offline manifest path is still not wired into the iOS client. `HomeSpeakerMobile/iOS/OfflineDownloadsStore.swift` keeps its own local manifest and still downloads from `/api/music/{songId}`, while the new persisted server contract in `HomeSpeaker.Server2/Services/OfflineDownloadService.cs` and `/api/homespeaker/offline*` is never called from `HomeSpeakerMobile/Shared/APIClient.swift`.

Host validation still passed for what Windows can prove: `dotnet build D:\homespeaker\HomeSpeaker.sln`, `dotnet test D:\homespeaker\HomeSpeaker.sln` (no real tests), runtime smoke on `/health`, `/api/homespeaker/offline`, `/`, `/music`, and `/playlists`, plus scratch-SQLite confirmation that the `OfflineDownloadTargets` table and unique index are created by migration startup. Siri explicit commands exist in code, but iOS/watch invocation remains code-reviewed only from this host.

### 2026-05-14: Post-fix Release Review - APPROVED
Validated the final Siri/offline pass after one last server-side fix. The requested blockers are now in place in code: `PlayFunMusicOnHomeSpeakerIntent` directly targets AI genre `family-singalong`, `PlayerStatus.quietDownVolume` preserves non-zero volume at a minimum of 1 for both iOS and watch flows, and `OfflineDownloadsStore.summaryLine` reports failed downloads separately instead of folding them into pending work.

The iOS offline client is now wired to the `/api/homespeaker/offline*` contract through `HomeSpeakerMobile/Shared/APIClient.swift` and `HomeSpeakerMobile/iOS/OfflineDownloadsStore.swift`, with durable `Song.path` keys carried through `OfflineSongKey`, local-file lookup, and playback fallback in `HomeSpeakerMobile/iOS/LocalPlayer.swift`. I also caught and fixed a release-blocking server bug in `HomeSpeaker.Server2/Services/OfflineDownloadService.cs`: offline media responses must resolve catalog paths to full filesystem paths before calling `Results.File`, or device downloads fail on relative library paths even though manifest creation succeeds.

Host validation for the approved state: `dotnet build D:\homespeaker\HomeSpeaker.sln -c Release`, `dotnet test D:\homespeaker\HomeSpeaker.sln -c Release --no-build` (still no real automated tests), route smoke on `/health`, `/api/homespeaker/offline`, `/`, `/music`, and `/playlists`, plus live create/download/delete exercise of `/api/homespeaker/offline/targets` and `/api/homespeaker/offline/media` against a real song. I still cannot prove Siri invocation or on-device iOS playback lifecycle from this Windows host, so those remain code-reviewed rather than simulator/device-verified here.
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

