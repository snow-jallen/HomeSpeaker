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

## Consolidated Learnings (2026-05-14: Siri/Offline QA Cycle)

**Siri Commands QA Outcomes:** Initial scope rejection due to incomplete explicit intents (next song, fun music, hymns, quiet down, stop) and offline implementation gaps. Revision routes to Kaylee for App Intents and durable-key contracts. Re-review iterations focused on `Song.path` durable keys (not scan-order `songId`), final approval with legacy migration caveat. Key path fixes: `HomeSpeakerMobile/iOS/OfflineDownloadsStore.swift`, `LocalPlayer.swift` (path-based lookup), `/api/homespeaker/offline*` integration.

**Final Release Validation:** Contract gap resolved (client wired to server offline endpoints). Blockers fixed: `PlayFunMusicOnHomeSpeaker` directly targets `family-singalong`, `quietDownVolume` clamps non-zero to min 1, failed downloads counted separately from pending. Server fix: offline media responses must resolve catalog paths to full filesystem before `Results.File`.

## Learnings
<!-- Siri/Offline and release work below -->

### 2026-05-15: Push notification rebuild validation
Rebuilt server compatibility for the iOS push flow without bringing back the old module abstraction. The backend now exposes `api/homespeaker/push/installations/{installationId}` plus `.../modules/{moduleKey}` while storing subscriptions on the existing `PushNotificationDevice` booleans (`TemperatureAlertsEnabled`, `BloodSugarAlertsEnabled`) and persisting new bundle/token metadata in SQLite.

Validation I could automate here: `dotnet test D:\homespeaker\HomeSpeaker.sln` passes with new coverage for installation upsert/status, module toggle persistence, and repeat-suppressed temperature alert delivery using a fake `IPushNotificationSender`. I still cannot prove APNs delivery on a real Apple device from this Windows host, so device-side permission prompts and receipt remain simulator/device follow-up.
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

### 2026-05-15: Push notification rebuild validation
Rebuilt server compatibility for the iOS push flow without bringing back the old module abstraction. The backend now exposes `api/homespeaker/push/installations/{installationId}` plus `.../modules/{moduleKey}` while storing subscriptions on the existing `PushNotificationDevice` booleans (`TemperatureAlertsEnabled`, `BloodSugarAlertsEnabled`) and persisting new bundle/token metadata in SQLite.

Validation I could automate here: `dotnet test D:\homespeaker\HomeSpeaker.sln` passes with new coverage for installation upsert/status, module toggle persistence, and repeat-suppressed temperature alert delivery using a fake `IPushNotificationSender`. I still cannot prove APNs delivery on a real Apple device from this Windows host, so device-side permission prompts and receipt remain simulator/device follow-up.

### 2026-05-15: Push-only regression pass
Validated the trimmed push scope after the module abstraction was removed. Current automated coverage now proves the installation resource stores direct `temperatureAlertsEnabled` / `bloodSugarAlertsEnabled` flags, unregistering clears the active registration state, and the existing temperature/blood-sugar alert services still drive delivery with repeat suppression and eligibility gating.

I did **not** reject the current implementation in this pass: server, shared DTOs, mobile client, and deployment docs are aligned on registration-plus-delivery rather than module concepts. Remaining limitation is unchanged from prior push review: APNs permission prompts and on-device receipt still need Apple hardware/simulator follow-up from a non-Windows host.

## Push Module Cleanup — Complete (2026-05-15T21:05:56Z)

**Status:** ✅ COMPLETE

**Team execution summary:**
- Wash: Removed `/modules/...` shape from backend push API → flattened to `temperatureAlertsEnabled` and `bloodSugarAlertsEnabled` direct flags on installation resource
- River: Simplified iOS push to registration-only, removed module toggles and module-specific wording
- Zoe: Validated no module-coupled push behavior remains; registration lifecycle, unregister, opt-in gating, and deduped delivery all verified

**Final outcome:** Push is now registration-plus-delivery only. Validation confirmed installation lifecycle (`PUT/GET`), unregister behavior, opt-in gating, and repeat-suppressed alert delivery from existing temperature/blood-sugar services. No module-shaped subresource validation required. Tests passed; no rejection.

---

