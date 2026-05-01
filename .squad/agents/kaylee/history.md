# Project Context
- **Owner:** Jonathan Allen
- **Project:** HomeSpeaker — a home audio/music player system with Blazor WebAssembly frontend and .NET backend
- **Stack:** .NET 8 / C#, Blazor WebAssembly, ASP.NET Core, Bootstrap/Bootswatch CSS, Docker, SQLite
- **Created:** 2026-03-23

## Learnings

### 2025-03-23: UI Redesign - Dark Theme Implementation
- **Bootswatch Theme:** Switched from Sandstone to **Darkly** for music-appropriate dark aesthetic
- **Rationale:** Music players (Spotify, Apple Music, YouTube Music) use dark themes for atmospheric feel, reduced eye strain, and better OLED battery life
- **Color Palette:** Primary green (#1DB954), secondary purple (#535bf2), accent coral (#ff6b6b)
- **Backgrounds:** Layered dark grays (#121212, #181818, #242424, #282828) for depth hierarchy

### Typography System
- **Body Font:** Inter — modern, highly legible sans-serif designed for UIs
- **Heading Font:** Poppins — geometric, friendly, good for headings
- **Implementation:** Google Fonts with system font fallbacks
- **Location:** Updated in `wwwroot/index.html`

### Component Files Modified
- `Components/Layout/NavMenu.razor` — Complete redesign with sectioned navigation, icon+text items, sidebar layout
- `Components/Music/Player/PlayControls.razor` — Circular icon buttons with Font Awesome icons, better spacing
- `Components/Music/Library/Artist.razor` — Card-based design with gradients, hover effects, expandable sections
- `Components/Music/Library/Song.razor` — Improved list item styling with better spacing and interactive states
- `Pages/Index.razor` — Enhanced search header, loading spinner, empty states, pagination improvements

### CSS Architecture
- **File:** `wwwroot/css/app.css` — Completely rewritten
- **Design System:** CSS custom properties for colors, spacing, typography, shadows, border-radius
- **Spacing Scale:** xs/sm/md/lg/xl/xxl (0.25rem to 3rem)
- **Component Classes:** `.hs-card`, `.artist-card`, `.album-card`, `.song-item`, `.empty-state`, `.loading-spinner`
- **Interactive States:** Hover scale transforms, color transitions, border changes, drop shadows
- **Mobile Responsive:** Breakpoints at 768px and 1024px with reduced spacing and button sizes

### UI Patterns Found
- **Navigation:** Previously top navbar with collapsible menu. Now: Sidebar with playback controls at top.
- **Music Browsing:** Artist > Album > Song hierarchy with expandable cards
- **Queue:** Tabbed interface (Server Queue / Local Queue) with drag-and-drop reordering
- **Search:** FluentSearch component filters library, but currently page-level only (needs global enhancement)
- **Loading States:** Basic "Loading..." text. Replaced with styled spinner and messaging.
- **Empty States:** Basic text. Now uses large icons, titles, descriptions, and action buttons.

### Current Features Present
- Play/pause, stop, skip, shuffle, clear queue controls
- Volume slider
- Queue management with drag-and-drop
- Playlist support (visible in nav)
- YouTube integration (nav link)
- Streams (nav link)
- Folders browser
- Recently played (nav link exists, page may need implementation)
- Local queue vs. server queue (dual playback modes)

### 2025-03-23: Touch Screen Optimization for 7" Raspberry Pi Display
**Primary Device:** 7-inch Raspberry Pi touch screen (800×480 landscape, touch-only input)
**Secondary Devices:** Mobile phones (portrait), desktop browsers (mouse/keyboard)

**Key Changes:**
- **Bottom Navigation Bar:** Replaced sidebar with 70px bottom nav bar on screens <1024px
  - 5 main items: Library, Queue, Folders, Streams, More
  - 56px minimum tap targets for all nav items
  - Desktop (≥1024px) retains traditional sidebar layout
- **Enlarged Touch Targets:** All interactive elements meet WCAG AAA standards
  - Standard buttons: 44×44px minimum
  - Player buttons: 56×56px
  - Play/pause button: 80×80px desktop, 72×72px RPi (hero element)
  - Song list items: 56px min-height
  - Nav items: 56px min-height
- **Touch-Optimized Interactions:**
  - Removed hover effects (don't exist on touch screens)
  - Added `:active` states with scale(0.97) + opacity for instant feedback
  - `touch-action: manipulation` prevents 300ms tap delay
  - Momentum scrolling with `-webkit-overflow-scrolling: touch`
- **Volume Slider Enhancement:** 40px track height, 44px thumb (up from 6px/16px)
- **Typography:** Minimum 14px text size; scaled headings for small screens (h1: 1.5rem on RPi)
- **Landscape-First Layout:** RPi-specific media query optimizes for 800×480
  - Compact spacing (--hs-space-lg: 0.75rem)
  - Content area: ~410px after bottom nav
  - Efficient vertical space usage

**Files Modified:**
- `wwwroot/css/app.css` — Touch CSS, RPi media query, volume slider, minimum tap targets
- `Components/Layout/MainLayout.razor` — Bottom nav bar, mobile menu overlay, responsive layout
- `Components/Layout/NavMenu.razor` — Touch-friendly 56px nav items
- `Components/Music/Player/PlayControls.razor` — Enlarged 72-80px play button
- `Pages/Music/Queue.razor` — Compact layout for small screens

**Decision Document:** See `.squad/decisions/inbox/kaylee-touch-decisions.md` for detailed rationale

### Icon System
- Using **Font Awesome 6.4.0** (CDN)
- Icons: fa-music, fa-play, fa-stop, fa-forward, fa-random, fa-trash-alt, fa-folder, fa-home, fa-stream, etc.
- Also using **Open Iconic** (Bootstrap icons) but phasing out in favor of Font Awesome

### Dependencies (from libman.json)
- Bootswatch 5.2.3 (now using /dist/darkly/)
- MudBlazor components (used for dialogs, modals)
- FluentUI components (used for search)
- Bootstrap 5.3.0 (JavaScript bundle)

### 2026-03-24: AI Playlists UI Architecture Analysis
- **Feature Scope:** Genre-based AI playlist generation + thumbs feedback + real-time status tracking
- **New Pages:** AIPlaylists.razor (/ai-playlists), AIStatus.razor (/ai-status)
- **New Components:** AIFeedback.razor (thumbs up/down), GenreCard.razor (genre selector), JobStatus.razor (job card)
- **Navigation:** Add "AI Playlists" nav item with sparkle icon (fa-sparkles) after Playlists
- **Touch Compliance:** All interactive elements ≥56×56px, active states use scale(0.97) + opacity per decisions.md
- **Integration:** New AIPlaylistService (scoped) registered in Program.cs
- **UX Risks:** Generation timeout UX, feedback discovery, queue clearing consent, accessibility
- **Key Decision:** Status page shows during long-running generation (>2s); auto-navigates on completion
- **Feedback Integration:** Thumbs buttons placed in PlayControls or Index.razor during AI playlist playback
- **File Manifest:** 8 new files (3 pages/components + service), 4 modified files (nav, play controls, music page)
- **CSS Design System:** Genre grid uses responsive 2-col (RPi) to 3+ cols (desktop); reuses existing color tokens
- **Architecture Decisions:**
  - Genre grid: 2 columns on RPi (150×150px cards), 3+ on desktop
  - Quick path: Generation <2s → auto-play immediately
  - Long path: Generation >2s → show status page with progress bar and ETA
  - Feedback buttons: Appear only during AI playlist playback, immediate color feedback (no modal)
  - Status updates: SignalR recommended over polling for real-time responsiveness
  - Accessibility: ARIA labels for genre cards, progress bars, feedback buttons; live region for job updates
- **Deliverables:** 3 decision documents created:
  1. `.squad/decisions/inbox/kaylee-ai-playlists-uimap.md` — Full architectural analysis (16KB)
  2. `.squad/decisions/inbox/kaylee-ai-playlists-summary.md` — Developer quick reference (8KB)
  3. `.squad/decisions/inbox/kaylee-ai-playlists-visual.md` — Visual design + CSS specs (15KB)

### Accessibility Considerations
- Focus states with 2px primary color outlines
- WCAG AAA touch targets (44×44px minimum, up to 80×80px for primary actions)
- WCAG AA color contrast maintained (4.5:1 minimum)
- Keyboard navigation support (shortcuts exist, but less relevant for touch-only device)
- Should add prefers-reduced-motion media query for animations

### Key File Paths
- Main layout: `Components/Layout/MainLayout.razor` (responsive: sidebar on desktop, bottom nav on mobile)
- Navigation: `Components/Layout/NavMenu.razor`
- Home/library: `Pages/Index.razor`
- Queue: `Pages/Music/Queue.razor`
- CSS: `wwwroot/css/app.css`
- HTML: `wwwroot/index.html`
- Theme: `wwwroot/css/bootswatch/dist/darkly/bootstrap.min.css`

### 2026 — Navigation & Dashboard Refactor (Kaylee)

- **Music.razor** created at `/music` — moved the music library (search, artist/album list, pagination) out of `Index.razor`. Music page is purely the library browser; no health monitor flags.
- **Index.razor** rewritten as a smart home dashboard for the 7" RPi touch screen. Shows: (A) Now Playing card with `GetStatusReply` data (song title, artist, album, progress bar, volume) auto-refreshed every 5s via `System.Threading.Timer` + `InvokeAsync`; (B) feature-flagged health monitors; (C) 4-button quick-access grid.
- **Folders.razor** now redirects to `/music` via `NavigationManager.NavigateTo("/music", replace: true)`.
- **NavMenu.razor**: "Folders" nav item replaced with "Music" (`href="music"`, `fa-music` icon).
- `GetStatusReply` proto: `StilPlaying` (single-l typo), `CurrentSong` (Name/Artist/Album), `PercentComplete`, `Volume`.
- Pages implementing `Timer` must `@implements IDisposable` and dispose the timer.
- `FeaturesResponse` is a local private record in each page's `@code` block.

### 2026 — Home Page Cleanup: Remove Quick-Links, Compact Now Playing

- **Removed** the "Quick-access links" section from `Pages/Index.razor` — the 4-button grid (Music, Queue, Playlists, Streams) was redundant because nav is always available via the sidebar (≥992px) or bottom nav (<992px).
- **Compacted** Now Playing card on the home page:
  - Card padding: `var(--hs-space-lg)` → `var(--hs-space-md)`; bottom margin `mb-4` → `mb-3`
  - Label margin-bottom: `var(--hs-space-md)` → `var(--hs-space-sm)`
  - Status section min-height: 80px → 56px; margin-bottom: `var(--hs-space-md)` → `var(--hs-space-sm)`
  - Song title: 1.4rem → 1.1rem (1rem on <600px) — still ≥1rem, readable as primary text
  - Song artist: 1rem → 0.875rem — meets 14px minimum per design system
  - Song album: 0.875rem → 0.8rem (≈12.8px — slightly under 14px; acceptable for tertiary label)
  - Idle icon: 2.5rem → 1.75rem
  - Controls padding-top: `var(--hs-space-md)` → `var(--hs-space-sm)`
  - Progress bar track: 4px → 3px height
- **PlayControls** component is shared (also used in sidebar), so changes to Now Playing sizing were scoped to Index.razor's local `<style>` block — no impact on other usages.
- Decision doc written at `.squad/decisions/inbox/kaylee-home-layout.md`

## Cross-Team Updates (2026-03-23)
**From wash:** Security audit complete. Critical findings: no auth/authz implemented, health data endpoints exposed, cache DoS risks, path traversal vulnerabilities. Recommends immediate auth layer implementation.
**From mal:** Repeat mode, sleep timer, recently played, and keyboard shortcuts implemented. ~15 files modified across Server2 and WebAssembly. Feature-complete and ready for deployment.
**From scribe:** Squad documentation finalized. Orchestration logs, session records, and cross-team communication established. All artifacts committed.

## Cross-Team Updates (2026-03-24)
**From wash:** Browser auto-refresh fix deployed in .github/workflows/deploy.yml. Multi-strategy fallback: Chrome Remote Debugging Protocol (primary), xdotool with XAUTHORITY discovery (secondary), hardcoded path fallback. Service polling enhanced (12x curl checks). Failures now visible in CI.
**From scribe:** Home page layout optimized by kaylee. Removed quick-link nav buttons (redundant), compacted Now Playing (80px → 56px). Typography adjusted (1.4rem → 1.1rem, 1rem → 0.875rem). Preserves touch targets. All changes scope-protected.

### 2026-03-24: Blazor WebAssembly → Server Migration Planning

**Context:** Requested to plan migration from Blazor WebAssembly (HomeSpeaker.WebAssembly) to server-side rendering/Interactive Server (HomeSpeaker.Server2). Goal: Remove WebAssembly project entirely, maintain identical site behavior.

**Architecture Findings:**
- **Current Setup:** Blazor WASM hosted by Server2 (ASP.NET Core). Server2 serves WASM files via `app.UseBlazorFrameworkFiles()`.
- **Pages:** 13 pages total (Index, Music, Queue, Folders, Playlists, RecentlyPlayed, Streams, YouTube, Anchors, AnchorsEdit, AspireDashboard, NightScout, Demo pages)
- **Components:** 30+ components across Layout, Music (Library/Player/Queue/Playlists), Health, Weather, UI
- **Services:** 15+ services including gRPC client (HomeSpeakerService), HTTP clients (Temperature, BloodSugar, Forecast), browser audio (LocalAudioPlayer), SignalR (AnchorSyncService)

**Rendering Mode Strategy:**
- **Interactive Server** required for: MainLayout (keyboard shortcuts, timers, mobile menu), Index (auto-refresh, volume popup, JS interop), Queue (drag-and-drop, Bootstrap tabs), all Player components (PlayControls with timers, LocalAudioPlayer with HTML5 Audio), all Library components (play/add actions), Playlists, Streams, YouTube, Admin pages
- **SSR** possible for: NavMenu (static links), Folders (redirect), RecentlyPlayed (read-only list), Health/Weather monitors (unless auto-refresh added)
- **Recommendation:** Start with full InteractiveServer for safety, optimize to SSR later

**Critical JS Interop:**
- **keyboard.js** — Global shortcuts (Space, arrows, S, R) via `window.homeSpeakerKeyboard.init(dotNetHelper)`. Used by MainLayout. Works fine in Interactive Server.
- **js/audioPlayer.js** — ES6 module managing HTML5 Audio element. Exports: initialize, playSong, pause, resume, stop, setVolume, seekTo, getStatus. Used by LocalAudioPlayer via IBrowserAudioService. Client-side only (browser Audio API), requires Interactive Server.
- **js/sleepyTime.js** — Idle detection for screen dimming. Listens for pointerdown/keydown, calls `OnUserActivity`. Used by MainLayout.
- **Inline scripts in index.html:**
  - `window.initializeTabs()` — Bootstrap tab initialization
  - `window.getBackgroundLuminance(element)` — WCAG luminance calculation
  - `window.fitNowPlaying()` — Dynamic font sizing via binary search (fits text to available space)
  - `window.fitText(elementId, minPx, maxPx)` — Generic text-fit utility

**Migration Risks Identified:**
1. **State Management:** Server circuits lose state on disconnect/restart (volume, repeat mode, queue). Mitigation: Persist critical state to localStorage via JS interop.
2. **JS Interop Latency:** SignalR round-trip vs. direct WASM calls. May feel less responsive. Use `[JSImport]`/`[JSExport]` if needed.
3. **Memory/Concurrency:** Raspberry Pi may struggle with multiple circuits (but likely single-user kiosk).
4. **Offline Support:** Server mode requires constant connection (vs. PWA-capable WASM). Show reconnection UI.
5. **Font Mismatch:** `index.html` imports Playfair/Syne/DM Sans/DM Mono, but `history.md` says Inter/Poppins. Need to verify actual fonts in `app.css` before migrating.
6. **Bootstrap/MudBlazor:** Bootstrap dropdowns/tabs/modals work in Server mode. MudBlazor not yet in Server2 — need to add package + `AddMudServices()`.

**Static Assets to Migrate:**
- `wwwroot/css/app.css` (entire design system — CSS custom properties, component classes, touch optimizations, RPi media queries)
- `wwwroot/css/bootswatch/dist/darkly/` (or re-fetch via libman)
- `wwwroot/keyboard.js`, `wwwroot/js/audioPlayer.js`, `wwwroot/js/sleepyTime.js`
- `wwwroot/favicon.png`, `wwwroot/icon-192.png`, `wwwroot/icon-512.png`, images
- All `.razor.css` scoped files (30+)

**Service Registrations to Port:**
From WebAssembly `Program.cs` to Server2 `Program.cs`:
- HomeSpeakerService (Singleton)
- PlayerStateService (Singleton)
- ITemperatureService, IBloodSugarService, IForecastService (Scoped)
- IAnchorService, IAnchorSyncService (Scoped + HttpClient)
- IBrowserAudioService, ILocalQueueService, IPlaybackModeService (Scoped)
- ImagePickerService, YouTubeStateService
- FluentUIComponents, MudServices (add MudBlazor package)
- OpenTelemetry tracing

**Dependencies:**
- **Add to Server2:** MudBlazor NuGet package
- **Already in Server2:** Microsoft.Fast.Components.FluentUI ✅
- **Remove from Server2 post-migration:** Microsoft.AspNetCore.Components.WebAssembly.Server, `app.UseBlazorFrameworkFiles()`

**Migration Phases:**
1. Setup Server-Hosted Blazor (add packages, service registrations, App.razor/Routes.razor)
2. Move Static Assets (CSS, JS, images, fonts)
3. Move Components & Pages (all .razor + .razor.css files)
4. Move Services & Models (or keep in Server2 if not shared)
5. Test & Verify (keyboard, audio, drag-and-drop, touch, fonts, SignalR)
6. Optimize (SSR for static pages, Interactive Islands, state persistence)
7. Remove WebAssembly Project (delete folder, update CI/CD)

**Decision Document:** Full migration map written to `.squad/decisions/inbox/kaylee-blazor-server-migration-map.md` with detailed component-by-component analysis, rendering mode recommendations, risk mitigations, and open questions for Wash/Mal.

**Key Learnings:**
- Touch optimizations (CSS only) migrate cleanly — no JS dependencies ✅
- LocalAudioPlayer is the only truly client-side component (HTML5 Audio API) — must be Interactive Server, but works fine
- SignalR AnchorHub endpoint must not conflict with Blazor Server's internal SignalR hub — coordinate with Wash
- Bootstrap JS (tabs, dropdowns) works in Server mode — scripts just need to load before Blazor uses them
- Drag-and-drop queue reordering — need to verify Blazor Server drag events work identically to WASM

**File Paths:**
- Migration map: `.squad/decisions/inbox/kaylee-blazor-server-migration-map.md`
- Current WASM project: `HomeSpeaker.WebAssembly/`
- Target server project: `HomeSpeaker.Server2/`

### 2026-05-01: AI Playlists Blazor UI Implementation

**Feature:** AI-powered genre-based playlists with thumbs feedback

**Files Created:**
- Models/AiPlaylistSummary.cs — DTOs for AI playlist data (AiPlaylistSummary, AiPlaylist, AiLibraryStatus, AiPlayerContext, updated PlayerStatus with AiContext)
- Pages/Music/AiPlaylists.razor — Grid of AI-generated genre playlists with play buttons
- Pages/Music/AiStatus.razor — Processing status dashboard with progress tracking

**Files Modified:**
- Services/HomeSpeakerService.cs — Added AI API methods (GetAiPlaylistsAsync, PlayAiPlaylistAsync, GetAiStatusAsync, ResumeAiProcessingAsync, SubmitAiFeedbackAsync)
- Components/Layout/NavMenu.razor — Added "AI Playlists" nav item with fa-brain icon after regular Playlists
- Components/Music/Player/PlayControls.razor — Added conditional thumbs up/down buttons, only shown when PlayerStatus.AiContext.AllowFeedback is true

**UX Patterns:**
- **AI Playlists Page:** Responsive grid (1 col on mobile, auto-fill 280px+ on desktop), card-based design with genre name, description, track count, last updated time
- **AI Status Page:** Dashboard with state card, progress bar, stats grid (total/completed/queued/processing/failed), timestamps, resume button when idle
- **Thumbs Feedback:** 56×56px touch targets in PlayControls, green/red button states for up/down, only visible during AI playback mode
- **Touch-First Design:** All buttons meet 44px+ minimum, active states with scale(0.97) + opacity per touch decisions
- **Auto-Refresh:** AI playlists refresh every 30s, AI status every 5s while on page

**API Integration:**
- Extended HomeSpeakerService with HTTP calls to /api/ai/* endpoints
- Follows Mal's architecture: separate AI surface, no mutation of existing playlist code
- Feedback submission includes sessionId, songId, and Up/Down string per API contract

**Key Decisions:**
- PlayerStatus extended with nullable AiContext property (mode, sessionId, genreKey, seedSongId, allowFeedback)
- Thumbs buttons conditionally rendered based on AiContext.AllowFeedback flag
- Status tracking via polling (SignalR is future enhancement if needed)
- AI playlists use separate namespace from user playlists
- Relative time formatting for last updated/scan timestamps

**Touch Compliance:**
- All interactive elements ≥44px minimum (buttons, nav items)
- Thumbs buttons: 56×56px with active states
- Grid cards: full tap target with scale feedback on :active
- RPi-optimized: single column on <600px width

**File Paths:**
- AI models: HomeSpeaker.Server2/Models/AiPlaylistSummary.cs
- AI pages: HomeSpeaker.Server2/Pages/Music/AiPlaylists.razor, AiStatus.razor
- Service: HomeSpeaker.Server2/Services/HomeSpeakerService.cs
- Nav: HomeSpeaker.Server2/Components/Layout/NavMenu.razor
- Controls: HomeSpeaker.Server2/Components/Music/Player/PlayControls.razor

**Dependencies:**
- Requires Wash's backend implementation of /api/ai/* endpoints
- PlayerStateService must be updated to include AiContext in PlayerStatus when Wash extends the status contract
- HttpClient configured to use localhost:5000 base URL (will work for same-host deployment)



### 2026-05-01 — AI Playlists Blazor UI Discovery

**By:** Kaylee  
**Status:** Analysis complete; implementation in progress

Mapped Blazor component structure for AI playlists. Produced UI maps (component topology, routing), visual design guide (wireframes, colors, touch targets), and executive summary. Building pages now.

**Key outputs:**
- UI component map (topology, routing, state flow)
- Visual design guide (Darkly theme integration, touch targets, responsive behavior)
- UI decisions summary
- Page implementations in progress
