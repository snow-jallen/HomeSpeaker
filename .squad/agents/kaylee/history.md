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

## Cross-Team Updates (2026-03-23)
**From wash:** Security audit complete. Critical findings: no auth/authz implemented, health data endpoints exposed, cache DoS risks, path traversal vulnerabilities. Recommends immediate auth layer implementation.
**From mal:** Repeat mode, sleep timer, recently played, and keyboard shortcuts implemented. ~15 files modified across Server2 and WebAssembly. Feature-complete and ready for deployment.
**From scribe:** Squad documentation finalized. Orchestration logs, session records, and cross-team communication established. All artifacts committed.
