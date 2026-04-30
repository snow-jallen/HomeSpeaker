# Project Context
- **Owner:** Jonathan Allen
- **Project:** HomeSpeaker — a home audio/music player system
- **Stack:** .NET 8 / C#, Blazor, ASP.NET Core, Bootstrap/Bootswatch CSS, Docker, SQLite, SignalR, REST APIs
- **Created:** 2026-04-29T16:02:42-06:00

## Learnings

<!-- Append new learnings below. Each entry is something lasting about the project. -->
- **2026-03-24:** Blazor Server build enforces analyzer rules (file name = first type, camelCase private methods). Keep UI service facades in Server2 and avoid gRPC client wrappers; add explicit SSR-friendly service interfaces (temperature, blood sugar, forecast).
