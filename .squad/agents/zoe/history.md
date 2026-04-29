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
