# Squad Decisions

## Active Decisions

### 20260323104717: User Directive — Touch-First Design
**By:** Jonathan Allen (via Copilot)  
**Status:** Active

Primary interface is a 7" Raspberry Pi touch screen. Optimize UI primarily for that. Secondary: mobile phones. Tertiary: desktop. Touch-first, large tap targets, finger-friendly controls, no hover-only interactions.

---

### Theme Selection — Darkly Dark Theme
**Date:** 2025-03-23  
**Author:** Kaylee (Frontend Dev)  
**Status:** Implemented

Switched from Sandstone (light, neutral) to **Darkly** (dark theme). Music players traditionally use dark interfaces — reduces eye strain during evening listening, feels more atmospheric, common in Spotify/Apple Music/YouTube Music. Better for OLED screens. Provides better contrast for colorful album art.

**Color Palette:**
- Primary: `#1DB954` (Spotify green) — interactive elements, active states
- Secondary: `#535bf2` (Purple) — highlights, secondary actions
- Accent: `#ff6b6b` (Coral) — warnings, delete actions
- Backgrounds: `#121212` → `#282828` (hierarchy)

**Typography:** Inter (body) + Poppins (headings) via Google Fonts.

---

### Layout: Bottom Navigation for Mobile/RPi
**Date:** 2025-03-23  
**Author:** Kaylee (Frontend Dev)  
**Status:** Implemented

Converted sidebar navigation to bottom navigation bar for screens <1024px wide. On 800×480 landscape, sidebar wastes 25% horizontal space. Bottom nav is standard mobile pattern.

**Implementation:**
- Bottom bar: 70px tall with 56px tap targets (5 items: Library, Queue, Folders, Streams, More)
- Desktop (≥1024px): Traditional sidebar layout maintained
- Desktop breakpoint: 1024px

---

### Touch Target Standards — WCAG AAA Compliance
**Date:** 2025-03-23  
**Author:** Kaylee (Frontend Dev)  
**Status:** Implemented

Enforced strict minimum sizes across all interactive elements:
- Standard buttons/links: 44×44px minimum (WCAG AAA)
- Player icon buttons: 56×56px (primary actions)
- Play/pause button: 80×80px desktop, 72×72px RPi
- Nav items: 56px min-height
- List items: 56px min-height
- Volume slider: 40px track height, 44px thumb (up from 6px/16px)

Rationale: Larger targets reduce tap errors and improve user confidence on touch devices.

---

### Touch-Specific Interactions
**Date:** 2025-03-23  
**Author:** Kaylee (Frontend Dev)  
**Status:** Implemented

Replaced hover-based interactions with active states and touch optimizations:
- All elements: `touch-action: manipulation` (prevents 300ms tap delay)
- Active states: scale(0.97) + opacity 0.85 for immediate feedback
- Removed all :hover-only features
- Added `-webkit-overflow-scrolling: touch` for momentum scrolling
- Added `overscroll-behavior: contain` to prevent body scroll during touch

Rationale: Hover states don't exist on touch screens. Active states provide instant visual feedback that a tap registered.

---

### Typography Minimum Size
**Date:** 2025-03-23  
**Author:** Kaylee (Frontend Dev)  
**Status:** Implemented

No text smaller than 14px anywhere. Base font remains 16px.

Rationale: 7-inch screen at arm's length (30-50cm) requires readable text. 14px is minimum for comfortable reading without strain.

---

### Feature Implementation: Repeat Mode
**Date:** 2025-03-23  
**Author:** Mal (Architect)  
**Status:** Implemented

Toggle to loop the current song indefinitely. Added `RepeatMode` property to `IMusicPlayer` interface. After song ends, if RepeatMode = true and queue is empty, replay last song. Both `WindowsMusicPlayer` and `LinuxSoxMusicPlayer` updated. gRPC methods: `SetRepeatMode`, `GetRepeatMode`. UI: New repeat button in `PlayControls.razor` (yellow when active).

---

### Feature Implementation: Sleep Timer
**Date:** 2025-03-23  
**Author:** Mal (Architect)  
**Status:** Implemented

Schedule automatic stop after N minutes (15, 30, 45, 60, 90, 120 min). Added `SetSleepTimer(minutes)`, `CancelSleepTimer()` to `IMusicPlayer`. Uses `CancellationTokenSource` + `Task.Delay` for clean async timer. After expiration: stops playback, clears queue. gRPC methods: `SetSleepTimer`, `CancelSleepTimer`, `GetSleepTimer`. UI: Dropdown menu in `PlayControls.razor` (moon icon, shows remaining time).

---

### Feature Implementation: Recently Played History
**Date:** 2025-03-23  
**Author:** Mal (Architect)  
**Status:** Implemented

View history of last 20 songs played. Leverages existing `Impressions` table in database. Auto-tracking added via event handler in `HomeSpeakerService`. API endpoint: `GET /api/music/recently-played?limit=20`. New page at `/recently-played`. Nav menu: Added "Recently Played" link (clock icon).

---

### Feature Implementation: Keyboard Shortcuts
**Date:** 2025-03-23  
**Author:** Mal (Architect)  
**Status:** Implemented

Control playback without clicking. Shortcuts:
- **Space** = Play/Pause toggle
- **Right Arrow** = Skip forward
- **Left Arrow** = Previous/restart
- **Up Arrow** = Volume up (+5%)
- **Down Arrow** = Volume down (-5%)
- **S** = Stop
- **R** = Toggle repeat mode

Implementation: JavaScript `keyboard.js` with global event listener (ignores typing in input fields). Blazor interop: `MainLayout.razor` receives JS callbacks via `[JSInvokable]` methods. Works globally across all pages.

---

## Governance

- All meaningful changes require team consensus
- Document architectural decisions here
- Keep history focused on work, decisions focused on direction
