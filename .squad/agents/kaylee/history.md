# Project Context
- **Owner:** Jonathan Allen
- **Project:** HomeSpeaker — a home audio/music player system with Blazor WebAssembly frontend and .NET backend
- **Stack:** .NET 8 / C#, Blazor WebAssembly, ASP.NET Core, Bootstrap/Bootswatch CSS, Docker, SQLite
- **Created:** 2026-03-23

## Core Context

### UI Redesign (2025-03-23)
Switched from Sandstone to **Darkly** dark theme. Color palette: Spotify green (#1DB954), purple (#535bf2), coral (#ff6b6b). Typography: Inter (body) + Poppins (headings). Background hierarchy: #121212 → #282828.

### Touch Optimization (2025-03-23)
7-inch Raspberry Pi touch screen (800×480 landscape) primary device. Bottom navigation bar (70px, 56px tap targets). Touch targets: 44×44px minimum, 56×56px player, 80×80px play button. Momentum scrolling, no hover states, active state feedback.

### Responsive Design (2025-03-23)
Bottom nav for <1024px screens, sidebar for ≥1024px. Minimum text: 14px. 56px song list items. Mobile-first layout with landscape RPi optimizations (compact spacing, efficient vertical usage).

### WASM-to-SSR UI Migration (2026-03-24, 2026-04-29)
Mapped Blazor component migration paths. Risk: Dual rendering modes, JS interop failures during SSR prerendering. Components ready to migrate from WebAssembly to Server2.

### AI Playlists UI (2026-05-01)
Mapped Blazor UI structure for AI playlists: New pages in Pages/Music/, components in Components/Music/AiPlaylists/. Touch-optimized buttons, playlist browsing, feedback UI. Integrated with Darkly theme and existing navigation.

## Learnings
<!-- Recent entries below -->

### 2026-05-02: PlayControls Dispose Fix
Fixed build error CS0535 in PlayControls.razor caused by malformed C# code block structure. An extra closing brace between CheckAiContextSync() and RefreshSleepTimer() methods prevented the Dispose() method from being recognized by the compiler. Removed the orphaned brace on line 128. The component already correctly implemented IDisposable with proper cleanup of the refresh timer and PlayerState.StateChanged event subscription. AI feedback UX (thumbs up/down buttons) remains intact and functional.

### 2026-05-01: AI Playlists Blazor UI Implementation
