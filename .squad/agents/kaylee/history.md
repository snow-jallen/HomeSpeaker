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

### 2026-05-02: AI Playlist Card Navigation Follow-up (COMPLETED)
Swapped the `/ai-playlists` cards from Blazor click handlers to real full-card links so detail navigation works immediately and reliably on touch devices. Kept the play button layered above the card link so tapping Play still starts playback instead of navigating away. Cards navigate to `/ai-playlists/{genreKey}` on click. HomeSpeaker.Server2 build: ✅ SUCCESS

### 2026-05-02: EstateMapper PeopleNavMenu Disposal Diagnosis
Diagnosed critical lifecycle bug in PeopleNavMenu component during EstateMapper disposal request. Root cause: stale NavigationManager callback outlives component and triggers refresh after disposal. Impact: post-disposal state updates attempt to modify disposed component. Diagnostic completed, implementation pending team decision.

### 2026-05-02: PlayControls Dispose Fix
Fixed build error CS0535 in PlayControls.razor caused by malformed C# code block structure. An extra closing brace between CheckAiContextSync() and RefreshSleepTimer() methods prevented the Dispose() method from being recognized by the compiler. Removed the orphaned brace on line 128. The component already correctly implemented IDisposable with proper cleanup of the refresh timer and PlayerState.StateChanged event subscription. AI feedback UX (thumbs up/down buttons) remains intact and functional.

### 2026-05-01: AI Playlists Blazor UI Implementation

### 2026-05-02: AI Playlist Details Preview
Added a dedicated AI playlist details route in Server2 so users can inspect a playlist before playback. The gallery cards now behave like touch-friendly entry points with an explicit “View details” action, and the details table renders the real exposed scoring data: genre rank, genre score, optional “why” text, plus any marker columns Wash’s service shape provides for that playlist.

### 2026-05-02: AI Playlist Details UX (Completed)
Implemented /ai-playlists/{genreKey} details page showing full playlist metadata, included tracks, scoring columns (rank, score, why), and dynamic marker columns. Gallery cards act as preview-first entry points; play actions remain visible on both card and details page. Touch-optimized per Darkly theme. Validated by Zoe: all pages load, scoring data visible, play actions functional. ✅ APPROVED & COMPLETE
