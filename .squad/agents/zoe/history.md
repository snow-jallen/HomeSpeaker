# Project Context
- **Owner:** Jonathan Allen
- **Project:** HomeSpeaker — a home audio/music player system with Blazor WebAssembly frontend and .NET backend
- **Stack:** .NET 8 / C#, Blazor WebAssembly, ASP.NET Core, Bootstrap/Bootswatch CSS, Docker, SQLite
- **Created:** 2026-03-23

## Core Context

### SSR Migration & Validation (2026-03-24 — Q1 2026 Completed)
Blazor WebAssembly to Server-Side Rendering migration completed over Q1 2026. Four QA validation attempts; final approval post-rendermode fixes. Build succeeds (0 errors, 20 warnings), all pages accessible, 100+ Blazor components migrated, 11/14 pages with Interactive Server rendermode. Zero automated tests; manual smoke testing only. Status: ✅ APPROVED & LIVE.

### AI Playlists Feature Matrix (2026-05-01)
Comprehensive QA matrix defined covering 8 risk domains (restart safety, incremental pickup, multi-genre classification, similarity & autoplay, feedback loop, progress visibility, data consistency, E2E integration). 77 total test cases. Key risks identified: CRITICAL on restart safety (RPi kiosk needs transaction guarantee), CRITICAL on incremental pickup (new file detection), HIGH on progress visibility (RPi touch users need feedback), HIGH on similarity/autoplay (user exposure), MEDIUM on data consistency. Awaiting implementation completion before QA validation.

### Q1 2026 AI Playlists Planning (2025-03-24)
Produced comprehensive QA matrix covering AI playlist generation, feedback mechanisms, edge cases, and performance benchmarks. Test strategy aligns with in-process architecture (no vector DB, OpenAI backend). Full test coverage matrix and case definitions prepared for implementation phase.

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

End-to-end smoke test passed after backend fixes. Build succeeded, server startup healthy. Playwright automated testing confirmed all 6 pages load successfully (/, /music, /queue, /playlists, /ai-playlists, /ai-status all HTTP 200). Health endpoint confirmed Healthy. Build quality: 0 errors, 20 warnings (non-blocking).


### 2026-05-01 — AI Playlists QA Strategy

Produced comprehensive QA matrix covering AI playlist generation, feedback mechanisms, edge cases, and performance benchmarks. Test strategy aligns with in-process architecture (no vector DB, OpenAI backend). Full test coverage matrix and case definitions prepared for implementation phase.

### 2026-05-01: Azure OpenAI Provider QA Validation - APPROVED

Validated HomeSpeaker.Server2 built successfully, server started healthy with all AI config blank, and Playwright smoke coverage passed for /, /music, /queue, /playlists, /ai-playlists, /ai-status (HTTP 200, no errors). `/ai-status` showed provider-aware degraded summary. Additional validation with dummy Azure OpenAI env vars succeeded.

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

### 2026-05-02 — AI JSON Numeric Repair Validation

Validated Wash's AI malformed JSON numeric repair implementation. Tested: build succeeded, server startup healthy, browser smoke tests on pages (/, /music, /queue, /playlists, /ai-playlists, /ai-status). Harness-style validation confirmed malformed numeric values (e.g., 00.42) are properly repaired while non-target/truncated JSON still fails as intended. ✅ APPROVED for production.

**Orchestration logs created:**
- `2026-05-01T155906Z-zoe.md` — QA findings and blocking issues
- `2026-05-01T155906Z-mal.md` — Mal's verdict and next steps
- Session log: `.squad/log/2026-05-01T155906Z-ai-readiness-review.md`

### 2026-05-01: AI Retry + Batch/Timeout Validation - REJECTED
**Status:** ❌ Rejected
**Validated by:** Zoe

**What I verified:**
- `dotnet build D:\homespeaker\HomeSpeaker.sln` succeeds.
- Server starts and `/health` reports Healthy with current configuration.
- Playwright smoke coverage passed for `/`, `/music`, `/queue`, `/playlists`, `/ai-playlists`, and `/ai-status` (all HTTP 200).
- Code defaults and config now read as batch size `6` and model timeout `200` seconds.
- Runtime/database evidence shows failed work items are being re-queued automatically: previously failed items were picked back up as `Processing` with `Attempts = 2`, so manual DB edits are no longer required just to retry.

**Blocking finding:**
- Azure OpenAI requests are still timing out at **0:01:40 (~100s)** in live runtime/status output. `AiMusicAnalyzer` wraps the call in a 200s cancellation token, but `Program.cs` still constructs `AzureOpenAIClient` without setting the Azure SDK network timeout, so the provider-level 100s timeout wins first. That means the requested explicit doubled timeout is not actually proven end-to-end and appears ineffective for the active provider.

**What I did NOT prove:**
- I did not wait through a full fresh 200s request cycle after code changes; instead I used current runtime/status evidence plus code inspection. The retry path itself is proven, but the doubled timeout behavior is not.

**Revision recommendation:**
- Reject and assign **Mal** for revision/redirect so the Azure provider timeout is wired at the client level and then re-validated end-to-end.

### 2026-05-01: Timeout Wiring Re-Validation - APPROVED WITH CAVEAT
**Status:** ✅ Approved for requested regression scope
**Validated by:** Zoe

**What I verified:**
- `dotnet build HomeSpeaker.sln` succeeds.
- No dedicated test projects are present, so there was no meaningful automated test suite to run.
- Server starts on the HTTP profile and `/health` reports Healthy after startup and after smoke testing.
- Browser smoke passes for `/`, `/music`, `/queue`, `/playlists`, `/ai-playlists`, and `/ai-status`.
- Batch size is still 6 in configuration and the worker still claims `Take(batchSize)` work items.
- Failed-item auto-requeue remains in place in code, and live `/api/ai/status` activity showed retries plus `Lease expired; re-queued.` behavior.
- Mal's timeout revision now wires the configured AI timeout into provider client options (`NetworkTimeout` and `HttpClient.Timeout`) in addition to the analyzer-level cancellation token.

**Timeout-specific caveat:**
- I did not get a clean live repro of a ~200 second timeout because the current provider/runtime behavior in this environment failed faster with 404 and invalid-JSON responses instead of hanging long enough to exercise the timeout path.
- Static validation shows the old provider-level ~100 second cap should no longer win: `ModelRequestTimeoutSeconds` is 200, `AiMusicAnalyzer` cancels at that window, and `Program.cs` now applies a transport timeout of request timeout + 15 seconds to both Azure OpenAI and OpenAI client creation.

**Recommendation:**
- Accept Mal's timeout wiring fix for this revision, but treat end-to-end timeout duration as code-validated rather than fully live-reproduced in this environment.

### 2026-05-01 — AI Retry/Timeout Fix Validation (Reject & Revalidate)

Performed initial validation of Wash's AI retry timeout implementation. Identified critical issue: end-to-end timeout ineffective because Azure SDK using default HttpClient.Timeout. Documented root cause and assigned Mal for provider-level revision. Revalidated post-revision with smoke tests across routes, auto-requeue state transitions, and timeout wiring. Approved final implementation.

### 2026-05-02: AI JSON Parse Fix Validation - APPROVED
**Status:** ✅ Approved
**Validated by:** Zoe

**What I verified:**
- `dotnet build D:\homespeaker\HomeSpeaker.Server2\HomeSpeaker.Server2.csproj` succeeds.
- Server starts on the HTTP launch profile and `/health` stays Healthy before and after smoke coverage.
- Browser automation smoke passed for `/`, `/music`, `/queue`, `/playlists`, `/ai-playlists`, and `/ai-status`.
- `AiMusicAnalyzer` now applies a narrow numeric-token repair only for the allowlisted numeric fields (`energy`, score/value/confidence-style fields, etc.) before re-deserializing the batch payload.
- A targeted harness against the built assembly proved a reported-shape case (`$.songs[5].energy` with `00.42`) is repaired to `0.42`, after which the full 6-song batch deserializes successfully.
- The same harness proved the repair is not broad/unsafe: a malformed non-target field and a truncated JSON payload both remained unrepaired and still throw JSON parse errors.

**What I did NOT prove live:**
- I did not capture a fresh runtime log entry showing the repair happening against the active model provider. The live `/api/ai/status` data still contains historical parse failures, so repair behavior in production traffic is code+harness validated here, not directly observed from the provider during this session.

