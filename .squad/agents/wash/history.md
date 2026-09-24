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

## Consolidated Learnings (2026-05-01 → 2026-05-14)

**AI Playlists & Timeout Management:** Implemented auto-requeue for failed work items with 6-item batch default and 200s timeout enforced at both analyzer and transport. Dual OpenAI provider config (Azure preferred when fully configured). AI JSON numeric repair pass normalizes invalid numbers before deserialization. Genre key deduping uses `StringComparer.OrdinalIgnoreCase`. Detail payload enriched with per-track scoring.

**Music Page & Playback:** Music page play dropdown routes through `PlaySongsAsync`, clearing queue before enqueueing. Separate add-to-queue path preserves append semantics. Genre entry sanitization bounded to `.genres` failures only.

**Offline Downloads & Siri Release:** Server-side manifest (`OfflineDownloadService`) with `/api/homespeaker/offline*` endpoints. Client wired to use `Song.Path` durable keys via `OfflineSongStore`. Fixed Siri commands (`PlayFunMusicOnHomeSpeaker` targets `family-singalong` directly) and volume halving logic (`PlayerStatus.quietDownVolume` clamps non-zero to minimum 1). Release blocker fix in `OfflineDownloadService.cs` resolved path resolution before `Results.File`.

## Learnings
<!-- Recent entries below -->

### 2026-05-15: Flatten push installation status to plain alert flags
Removed the `/api/homespeaker/push/installations/{installationId}/modules/{moduleKey}` shape from the backend contract and kept `/api/homespeaker/push/installations/{installationId}` as the single mobile-facing push resource. The installation request/status DTOs now carry `TemperatureAlertsEnabled` and `BloodSugarAlertsEnabled` directly, which keeps APNs registration and per-alert preferences in one idempotent upsert without reintroducing a module abstraction.

Validation here passed with `dotnet build HomeSpeaker.sln` and `dotnet test HomeSpeaker.Server2.Tests\HomeSpeaker.Server2.Tests.csproj`. I still cannot verify real APNs delivery on Apple hardware from this Windows host, so device receipt remains follow-up outside this environment.

### 2026-05-15: Push notification rebuild validation
Rebuilt server compatibility for the iOS push flow without bringing back the old module abstraction. The backend now exposes `api/homespeaker/push/installations/{installationId}` plus `.../modules/{moduleKey}` while storing subscriptions on the existing `PushNotificationDevice` booleans (`TemperatureAlertsEnabled`, `BloodSugarAlertsEnabled`) and persisting new bundle/token metadata in SQLite.

Validation I could automate here: `dotnet test D:\homespeaker\HomeSpeaker.sln` passes with new coverage for installation upsert/status, module toggle persistence, and repeat-suppressed temperature alert delivery using a fake `IPushNotificationSender`. I still cannot prove APNs delivery on a real Apple device from this Windows host, so device-side permission prompts and receipt remain simulator/device follow-up.

### 2026-05-15: Push notifications rebuilt on direct health flows
Rebuilt push notifications without resurrecting the split monitoring module. The stable shape is: keep temperature and blood sugar in their existing services, persist APNs device registrations in `PushNotificationDevices`, persist dedupe state in `PushNotificationAlertStates`, and trigger notifications only from the fresh-fetch paths so cached polls do not fan out duplicate pushes.

The mobile-facing API lives under `/api/homespeaker/push/installations/{installationId}` with module subscription toggles, while APNs delivery details stay server-side in `PushNotificationService` and `ApplePushNotificationSender`. Validation here passed with `dotnet build HomeSpeaker.sln`, `dotnet test HomeSpeaker.Server2.Tests\HomeSpeaker.Server2.Tests.csproj`, and `dotnet ef migrations has-pending-model-changes`.

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

## Push Module Cleanup — Complete (2026-05-15T21:05:56Z)

**Status:** ✅ COMPLETE

**Team execution summary:**
- Wash: Removed `/modules/...` shape from backend push API → flattened to `temperatureAlertsEnabled` and `bloodSugarAlertsEnabled` direct flags on installation resource
- River: Simplified iOS push to registration-only, removed module toggles and module-specific wording
- Zoe: Validated no module-coupled push behavior remains; registration lifecycle, unregister, opt-in gating, and deduped delivery all verified

**Final outcome:** Leftover module-related push terminology removed across backend, iOS, and QA validation. Installation endpoint is now a single idempotent resource with direct boolean alert flags. Existing temperature and blood sugar services continue to deliver alerts through current paths without module indirection.

---
