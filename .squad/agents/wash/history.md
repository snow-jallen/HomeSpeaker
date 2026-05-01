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

### 2026-05-02 — Dual OpenAI Provider Config
Updated `HomeSpeaker.Server2` AI wiring to support either public OpenAI or Azure OpenAI from the existing `AI` options section. `AI:AzureOpenAI` now uses `Endpoint`, `ApiKey`, and `DeploymentName`, Azure is preferred when fully configured, and degraded-status messaging points at the active/missing provider instead of always blaming `AI:OpenAI:ApiKey`.

### 2026-05-02 — AI Playlists Backend Slice
Implemented AI playlist backend slice in Server2: AI options + OpenAI `IChatClient` wiring, AI entities + seeded genres, background analysis worker, similarity/autoplay, feedback capture, and `/api/ai/*` endpoints. Player status now includes nullable AI context via shared DTOs. Migration was created manually because the existing PlayControls razor build error blocks `dotnet ef`.

### 2026-05-02 — Azure OpenAI Support (Request from Jonathan Allen)
Implemented dual OpenAI provider configuration: added `AI:AzureOpenAI` section with `Endpoint`, `ApiKey`, `DeploymentName`. Runtime preference: Azure when fully configured, fallback to public OpenAI. Updated degraded-status messaging to reflect active provider. Validated by Zoe: build clean, server startup healthy, smoke tests passing on /, /music, /queue, /playlists, /ai-playlists, /ai-status. ✅ APPROVED
