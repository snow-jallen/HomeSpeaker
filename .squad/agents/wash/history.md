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
