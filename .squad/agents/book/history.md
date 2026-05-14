# Project Context
- **Owner:** Jonathan Allen
- **Project:** HomeSpeaker — a home audio/music player system
- **Stack:** .NET 8 / C#, Blazor, ASP.NET Core, Bootstrap/Bootswatch CSS, Docker, SQLite, SignalR, REST APIs
- **Created:** 2026-04-29T16:02:42-06:00

## Learnings

<!-- Append new learnings below. Each entry is something lasting about the project. -->
- **2026-03-24:** Blazor Server build enforces analyzer rules (file name = first type, camelCase private methods). Keep UI service facades in Server2 and avoid gRPC client wrappers; add explicit SSR-friendly service interfaces (temperature, blood sugar, forecast).
- **2026-05-14:** Offline mobile cutover is centered on `HomeSpeaker.Server2.Services.OfflineDownloadService` plus `/api/homespeaker/offline*`, while `HomeSpeakerMobile/iOS/OfflineDownloadsStore.swift` keeps only device file state and legacy manifest migration. Legacy offline downloads may require fetching the library during app-level refresh, not just from `MusicLibraryView`, so song-ID caches are resolved even if users never open the library screen.
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

