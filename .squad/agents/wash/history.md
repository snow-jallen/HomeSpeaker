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
