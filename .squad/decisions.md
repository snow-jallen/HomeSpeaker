# Squad Decisions

## Active Decisions

---

- Genre grid: 2 columns
- Card size: ~180×180px
- Bottom nav: Visible OR sidebar depending on screen width
- Breakpoint: <1024px = bottom nav + mobile menu

**Desktop (≥1024px):**
- Genre grid: 3 columns (or 4 on ultra-wide)
- Card size: ~200×200px
- Sidebar: Always visible, 12em width
- Bottom nav: Hidden

---

## Animation & Microinteractions

### Genre Card Tap
```css
.genre-card:active {
  transform: scale(0.97);
  opacity: 0.85;
  transition: all 100ms ease-out;
}
```

### Progress Bar Update
```css
.progress-bar-ai {
  background-color: var(--hs-primary, #1DB954);
  transition: width 0.5s ease-in-out;
  height: 6px;
  border-radius: 3px;
}
```

### Feedback Button Click
```css
.feedback-btn:active {
  transform: scale(0.95);
  box-shadow: inset 0 2px 4px rgba(0, 0, 0, 0.2);
}
```

### Spinner (Loading)
```css
.spinner {
  animation: spin 1s linear infinite;
}

@keyframes spin {
  from { transform: rotate(0deg); }
  to { transform: rotate(360deg); }
}
```

---

## Font Sizing (Touch-Friendly)

| Element | Size | Usage |
|---------|------|-------|
| Genre name | 18px | Genre cards, clear and readable |
| Song count | 14px | Secondary info on cards |
| Job status | 14px | "Analyzing Rock..." text |
| Status badge | 12px | "Processing", "Complete" |
| Page title | 24px | "AI Playlists", "AI Status" |
| Progress % | 14px | "50% complete" |
| Aria labels | — | Not visible, for screen readers |

---

## Accessibility — Focus States

All interactive elements must have visible focus indicator:

```css
a:focus,
button:focus,
[role="button"]:focus {
  outline: 2px solid var(--hs-primary, #1DB954);
  outline-offset: 2px;
}
```

**Touch-friendly:** Outline offset prevents overlap with content.

---

## Notes for CSS Implementation

### New CSS Classes to Add

```css
.genre-grid {
  display: grid;
  grid-template-columns: repeat(2, 1fr);  /* RPi: 2 cols */
  gap: var(--hs-space-lg, 1rem);
  padding: var(--hs-space-md);
}

@media (min-width: 600px) {
  .genre-grid {
    grid-template-columns: repeat(3, 1fr);
  }
}

@media (min-width: 1024px) {
  .genre-grid {
    grid-template-columns: repeat(4, 1fr);
  }
}

.genre-card {
  min-height: 150px;
  min-width: 150px;
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  border-radius: var(--hs-radius, 0.5rem);
  background-color: var(--hs-bg-secondary, #282828);
  cursor: pointer;
  touch-action: manipulation;
  transition: all 150ms ease-out;
  padding: var(--hs-space-md);
}

.genre-card:active {
  transform: scale(0.97);
  opacity: 0.85;
  background-color: var(--hs-primary, #1DB954);
}

.ai-feedback-buttons {
  display: flex;
  gap: var(--hs-space-md);
  margin-top: var(--hs-space-md);
}

.feedback-btn {
  min-width: 56px;
  min-height: 56px;
  border-radius: 50%;
  border: 2px solid transparent;
  background-color: var(--bs-secondary);
  color: white;
  font-size: 24px;
  cursor: pointer;
  transition: all 100ms ease-out;
  touch-action: manipulation;
}

.feedback-btn:active {
  transform: scale(0.95);
}

.feedback-btn.liked {
  background-color: var(--hs-primary, #1DB954);
  border-color: var(--hs-primary, #1DB954);
}

.feedback-btn.disliked {
  background-color: var(--hs-accent, #ff6b6b);
  border-color: var(--hs-accent, #ff6b6b);
}

.status-job-card {
  border-radius: var(--hs-radius, 0.5rem);
  background-color: var(--hs-bg-secondary, #282828);
  padding: var(--hs-space-md);
  margin-bottom: var(--hs-space-md);
  min-height: 80px;
  display: flex;
  flex-direction: column;
  justify-content: space-between;
}

.progress-bar-ai {
  height: 6px;
  background-color: var(--hs-bg-primary, #121212);
  border-radius: 3px;
  overflow: hidden;
  margin: var(--hs-space-sm) 0;
}

.progress-bar-ai-fill {
  height: 100%;
  background-color: var(--hs-primary, #1DB954);
  transition: width 0.5s ease-in-out;
}
```

---

## Edge Cases & Fallbacks

1. **No internet during generation:** Show error state, allow retry
2. **Long generation (>60s):** Auto-cancel with error message
3. **Feedback submission fails:** Toast notification "Could not save feedback. Tap to retry?"
4. **Status page polling lags:** Show "Last updated: 2 seconds ago"
5. **Mobile viewport too narrow:** Genre cards stack to 1 column (or hide overflow with horizontal scroll)

---

This document is for design validation and implementation reference. Submit questions to Kaylee before coding.


---


**Quick Reference for Developers**

## The Ask

Add three interconnected features:
1. **AI Playlists menu option** — Navigate to genre-based AI playlist generator
2. **Thumbs up/down feedback** — Rate songs during AI playlist playback
3. **Progress/status page** — Monitor AI playlist generation in real-time

---

## UI Changes at a Glance

### Navigation Changes
- **NavMenu.razor:** Add `<NavLink href="ai-playlists" class="nav-item">` with `fa-sparkles` icon
- **Position:** After "Playlists" link, before "YouTube"
- **Mobile:** Accessible via "More" menu button on bottom nav

### New Pages (Routes)
| Route | File | Purpose |
|-------|------|---------|
| `/ai-playlists` | Pages/Music/AIPlaylists.razor | Genre selector (Rock, Pop, Jazz, etc.) |
| `/ai-status` | Pages/Music/AIStatus.razor | Real-time job progress tracker |

### New Components
| Component | Purpose | Placement |
|-----------|---------|-----------|
| AIFeedback.razor | Thumbs up/down buttons | PlayControls.razor or Index.razor (conditional) |
| GenreCard.razor | Genre tile (selectable) | AIPlaylists.razor |
| JobStatus.razor | Individual job card | AIStatus.razor (per active job) |

### Modified Components
| File | Change |
|------|--------|
| NavMenu.razor | +1 nav item (AI Playlists) |
| PlayControls.razor | +AIFeedback component (conditional, when in AI mode) |
| Index.razor | +AIFeedback component (alternative placement, conditional) |
| Music.razor | +Optional CTA to AI Playlists |
| Program.cs | +AIPlaylistService registration |

---

## Touch-First Compliance Checklist

All changes must respect `/squad/decisions.md` decisions:

- ✅ **Button/tap targets:** Minimum 44×44px (most should be 56×56px or larger)
- ✅ **Active states:** `scale(0.97) + opacity: 0.85` (immediate tactile feedback)
- ✅ **No hover-only interactions:** All states work on touch (`:active`, `:focus`, not just `:hover`)
- ✅ **Typography:** Minimum 14px for all text (except tiny secondary labels)
- ✅ **Responsive layout:** Genre grid = 2 cols on RPi (800×480), 3+ on desktop
- ✅ **Momentum scrolling:** Inherit `-webkit-overflow-scrolling: touch` from body
- ✅ **Touch action:** `touch-action: manipulation` to prevent 300ms tap delay

---

## User Flows

### Quick Path (Playlist ready in <2s)
```
Tap "AI Playlists" nav item
→ See genre grid (Rock, Pop, Jazz, etc.)
→ Tap genre card (e.g., "Rock")
→ [Spinner briefly shows] Playlist generated
→ Auto-navigate to Home or Queue
→ Songs playing, thumbs buttons visible
→ Tap 👍 or 👎 to rate song
```

### Long Path (Playlist takes >2s)
```
Tap genre card
→ [Spinner shows + auto-navigate]
→ Land on /ai-status page
→ See job card: "Rock AI Playlist - 35% complete"
→ User can wait or cancel
→ On completion: "Complete" badge + Play button
→ Tap Play → Queue updated → Auto-navigate home
```

---

## Backend Service Interface

**New Service: AIPlaylistService**

```csharp
// All methods are async Task<T>

// Core generation
GeneratePlaylistAsync(string genre) → Playlist

// Job tracking
GetActiveJobsAsync() → List<AIPlaylistJob>
GetJobAsync(string jobId) → AIPlaylistJob
CancelJobAsync(string jobId) → void

// Feedback loop
SubmitFeedbackAsync(string songId, bool liked) → void
GetFeedbackStatsAsync() → FeedbackStats  [optional]
```

**Models:**
```csharp
public record AIPlaylistJob(
    string Id,
    string Genre,
    int PercentComplete,        // 0-100
    DateTime StartedAt,
    DateTime? CompletedAt,
    string Status,              // "Pending", "Processing", "Complete", "Cancelled", "Failed"
    string? ErrorMessage,
    Playlist? GeneratedPlaylist
);
```

---

## CSS Design Tokens (Reuse Existing)

```css
/* Colors (from decisions.md) */
--hs-primary: #1DB954;          /* Spotify green */
--hs-accent: #ff6b6b;           /* Coral for negatives *)
--hs-bg-primary: #121212;
--hs-bg-secondary: #282828;

/* Spacing Scale */
--hs-space-sm: 0.5rem;
--hs-space-md: 0.75rem;
--hs-space-lg: 1rem;

/* Component sizes */
min-height: 56px;               /* Touch target minimum */
min-width: 56px;                /* Touch target minimum */
```

---

## Accessibility Requirements

**Minimum WCAG 2.1 AA compliance:**

- Color contrast: All text ≥ 4.5:1 (except <14px text, which must be ≥3:1)
- Focus indicators: Visible on all interactive elements (2px outline in primary color)
- ARIA labels: 
  - Genre cards: `aria-label="Generate Rock playlist"`
  - Thumbs buttons: `aria-label="Love this song"` and `"Skip similar"`
  - Progress bar: `aria-valuenow`, `aria-valuemin`, `aria-valuemax`
  - Job status: Live region `aria-live="polite"` for updates
- Keyboard support: Tab navigation, Enter/Space to activate
- Status messages: Always visible (don't rely on color alone)

---

## Known Risks & Mitigations

| Risk | Severity | Mitigation |
|------|----------|-----------|
| Long generation time feels broken | Medium | Show progress page, ETA, allow cancel |
| Thumbs buttons not discoverable | Low-Med | Prominent placement, highlight color, optional toast |
| Queue clearing without consent | Medium | Confirmation modal before replacing queue |
| Status page navigation confusion | Low | Auto-nav on completion, "Back to Music" button |
| Mobile menu hides AI Playlists | Low-Med | Consider promoting to main bottom nav or add home CTA |
| Accessibility gaps (screen readers) | Medium | ARIA labels + keyboard shortcuts |

---

## Testing Checklist (Before Handoff)

- [ ] Genre card tap works on touch device (RPi or phone)
- [ ] Fast generation (<2s): Auto-plays immediately
- [ ] Slow generation (>2s): Shows status page with live progress
- [ ] Thumbs buttons appear during AI playlist playback
- [ ] Thumbs feedback submits without error, shows visual feedback
- [ ] Status page cancellation works (job stops, UI updates)
- [ ] Back navigation from status page works (don't get stuck)
- [ ] All buttons meet 56×56px minimum (inspect with DevTools)
- [ ] Bottom nav still has 5 items (ensure no overflow on RPi)
- [ ] Keyboard shortcuts work (if extended for AI features)
- [ ] Contrast ratios pass (use axe DevTools or similar)
- [ ] Focus indicators visible on all interactive elements

---

## File Creation Order (Recommended)

1. **Models:** AIPlaylistJob.cs, FeedbackEntry.cs
2. **Service:** AIPlaylistService.cs (backend wrapper, not backend API itself)
3. **Components:** GenreCard.razor, JobStatus.razor, AIFeedback.razor
4. **Pages:** AIPlaylists.razor, AIStatus.razor
5. **Updates:** NavMenu.razor, PlayControls.razor, Program.cs
6. **Styling:** Add CSS classes to app.css (genre-grid, genre-card, ai-feedback-buttons, etc.)

---

## Notes for Mal (Architect)

- AIPlaylistService is a Blazor-side wrapper; the actual AI generation logic lives on the backend (gRPC or REST)
- Recommend SignalR for real-time job status updates (better UX than polling)
- Feedback storage: Add new DB table `SongFeedback` (SongId, UserId?, Liked, Timestamp)
- Consider: Genre detection from existing song metadata (avoid hard-coding 8 genres)

## Notes for Wash (Backend Dev)

- Auth/security: Who can generate AI playlists? Public? Authenticated only?
- Rate limiting: Prevent spam job submissions (e.g., 1 job per 5 seconds per user)
- Job timeout: Set max 60s generation time; auto-cancel and return error if exceeded
- Feedback loop: Is feedback used to improve model? Stored for analytics?
- DB schema: New tables for `AIPlaylistJobs` and `SongFeedback` (schema TBD)

---

## Decision Document

Full UI architecture analysis at:
`.squad/decisions/inbox/kaylee-ai-playlists-uimap.md`

This summary focuses on implementation; detailed design rationale in the full doc.


---


**By:** River (iOS Developer)  
**Date:** 2026-04-30  
**Status:** Awaiting implementation

## Summary
Analyzed iOS app structure for AI Playlists feature. Requires changes to Models, APIClient, and two Playlist-related views. No new Views needed. Risk is LOW with proper state isolation and polling cleanup.

## Changes Required

### Models.swift
- Add `AIPlaylistStatus` enum (generating, ready, failed)
- Extend `Playlist` struct: `isAIGenerated: Bool`, `aiStatus: AIPlaylistStatus?`
- Add `SubmitFeedbackRequest` struct

### APIClient.swift
- Add `createAIPlaylist(prompt:, count:) -> Playlist`
- Add `getAIPlaylistStatus(name:) -> AIPlaylistStatus`
- Add `submitFeedback(playlistName:, songId:, feedback:)`

### PlaylistsView.swift
- Display AI badge (sparkles icon) for AI playlists
- Show processing status (ProgressView if .generating, error icon if .failed)
- Implement polling with Timer (start .onAppear, stop .onDisappear)
- Adaptive backoff: 1s → 2s → 5s, stop when not .generating

### PlaylistDetailView.swift
- Show thumbs up/down buttons for songs (only in AI mode)
- Submit feedback via `api.submitFeedback()`
- Optional: show feedback summary header

### MainTabView.swift
- ContentView references this but it doesn't exist—must be created or verified

## Key Risks & Mitigations

| Risk | Mitigation |
|------|-----------|
| Polling drain | Adaptive backoff; stop when not generating |
| Stale status while backgrounded | Refresh on pull-to-refresh; accept brief staleness |
| Feedback during generation | Only show buttons if aiStatus == .ready |
| Race conditions on status update | Single @State source of truth; indexed updates |
| Submission failures silent | Error toast on failure; optional retry |

## Design Decisions

1. **Polling pattern:** Timer-based (not async sequence) for iOS 15+ compatibility. Consider server-sent events (SSE) in future.
2. **Feedback UI:** Horizontal thumbs in song rows (touch-first design, ≥44px targets).
3. **Backward compatibility:** Playlist struct extension uses optional fields; graceful decode of old playlists.
4. **Isolation:** AI feature is conditional on `isAIGenerated` flag; zero impact on existing playlist flows.

## Open Questions for Backend (Wash)

1. AI creation endpoint request/response format?
2. Possible `AIPlaylistStatus` values and any metadata?
3. Feedback endpoint format?
4. Does feedback immediately reorder songs or only log for training?
5. Error codes for failed generation?

## Implementation Order

1. Models.swift (foundation)
2. APIClient.swift (enables testing)
3. PlaylistsView.swift (status display + polling)
4. PlaylistDetailView.swift (feedback UI)
5. MainTabView.swift (fix missing view)

**Full analysis:** See `iOS_AI_PLAYLISTS_ANALYSIS.md`


---

**Date:** 2025-03-24  
**Author:** Zoe (QA Engineer)  
**Status:** Ready for Implementation Review  

---

## Executive Summary
This matrix defines **77 test cases** across **8 risk domains** for the AI Playlists feature. The feature requires resumable pipeline processing, incremental new-track pickup, multi-genre classification, playlist generation, and adaptive feedback behavior across both Blazor and iOS clients.

**Risk areas prioritized by likelihood of failure:**
1. **Restart safety** (pipeline recovery)
2. **Incremental pickup** (new track detection)
3. **State consistency** (genre markers, playlist sync)
4. **Progress visibility** (UI updates lag)
5. **Feedback loop** (thumbs up/down → autoplay adaptation)

---

## Part A: Restart & Resume Safety

### A1: Database Transaction Integrity
**Risk:** Pipeline crashes mid-song classification; database left in partial state.

| ID | Test Case | Acceptance Criteria | Pass? |
|---|---|---|---|
| **A1.1** | Start classification on 10 songs, kill process at song 5 | Restart: Completed songs marked `classified=true`, unprocessed songs remain `classified=false`. No duplicates. | — |
| **A1.2** | Playlist generation starts, kill process mid-insert | Restart: Partial playlists cleaned up (rollback or marked incomplete). Next run doesn't re-insert duplicates. | — |
| **A1.3** | Genre assignment in transaction fails on song N | Song N skipped, pipeline continues. Song N retried on next restart. No data corruption. | — |
| **A1.4** | SQLite database locked during batch update | Pipeline detects lock, retries (not crash). Logs retry attempt. | — |
| **A1.5** | Shutdown during similarity-link insert | Restart: Partial similarity edges cleaned up. No orphan pointers. Playlist generation works on clean graph. | — |

### A2: Pipeline State Persistence
**Risk:** Pipeline forgets progress; restarts from zero instead of resuming.

| ID | Test Case | Acceptance Criteria | Pass? |
|---|---|---|---|
| **A2.1** | Pipeline batch size 100. Process 60 songs, restart. | Next run: Skips first 60, processes songs 61+. Total run time ≈60% of initial. | — |
| **A2.2** | Pipeline crashes after writing `ProcessedAt` timestamp to 40/100 songs | Restart: Reads timestamp, skips those 40, starts at 41. | — |
| **A2.3** | Pipeline state table corrupted | Recovery: Reset state table, mark all songs unclassified, restart from song 1. Logs warning. | — |
| **A2.4** | Two pipeline instances start simultaneously (race condition) | Instance 1 acquires lock, instance 2 waits/exits. No duplicate classifications. | — |
| **A2.5** | Server restarts 3× in succession during classification | Restarts 2 & 3: Correctly resume from previous checkpoint. No exponential slowdown. | — |

### A3: Graceful Shutdown
**Risk:** Long-running batch job doesn't save progress before exit.

| ID | Test Case | Acceptance Criteria | Pass? |
|---|---|---|---|
| **A3.1** | SIGTERM sent to process during song 47/100 classification | Pipeline: Completes current song, saves state, shuts down cleanly. Restart resumes at song 48. | — |
| **A3.2** | Kill -9 (SIGKILL) during batch insert | Restart: Detects partial insert, rolls back to known good state. | — |
| **A3.3** | Kubernetes pod evicted mid-pipeline | Pod restart: Reads last good checkpoint, resumes. No data loss. | — |

---

## Part B: Incremental Pickup of New Tracks

### B1: New Song Detection
**Risk:** New songs added to library after initial classification are never processed.

| ID | Test Case | Acceptance Criteria | Pass? |
|---|---|---|---|
| **B1.1** | Classify 50 songs, add 10 new songs to folder, run pipeline again | All 10 new songs marked `classified=false`, pipeline processes them. Old songs skipped. | — |
| **B1.2** | New song metadata (title, artist) changed after classification | Pipeline re-checks file modified timestamp. If changed, re-classify. Or: Skip if timestamp unchanged. (Behavior should be documented.) | — |
| **B1.3** | Song file deleted after classification | Pipeline detects missing file, marks `deleted=true`, removes from playlists. No playlist references broken files. | — |
| **B1.4** | Batch job paused at song 50, 100 new files added, resume | Pipeline: Continues from song 51 (original batch). Next scheduled run processes new 100. | — |
| **B1.5** | Folder monitoring: New file detected 0.5s after pipeline ends | Next pipeline run (hourly? scheduled?) picks it up. Grace period documented. | — |

### B2: Incremental Playlist Updates
**Risk:** New classified songs aren't added to existing genre playlists.

| ID | Test Case | Acceptance Criteria | Pass? |
|---|---|---|---|
| **B2.1** | Genre "Rock" has 15 songs. 5 new "Rock" songs classified. | Playlist "Rock" updated to 20 songs. UI shows new count. | — |
| **B2.2** | Genre playlist capped at 50 songs. New songs added but cap hit. | Playlists: Either extend to 60 (if no hard limit) or track "overflow" and rotate oldest. Behavior documented. | — |
| **B2.3** | Song re-classified into different genre. | Old genre playlist: Song removed (if it was the only genre). New genre playlist: Song added. | — |

### B3: Edge Cases — File Handling
**Risk:** Symlinks, invalid files, permission errors break incremental pickup.

| ID | Test Case | Acceptance Criteria | Pass? |
|---|---|---|---|
| **B3.1** | Song is a symlink to another file | Pipeline: Classifies symlink once. If target changes, symlink behavior is defined (skip or re-process). | — |
| **B3.2** | File is unreadable (permission denied) | Log error, skip file, continue. Next run retries. | — |
| **B3.3** | Directory structure changes; song moved to subfolder | Pipeline: Detects new path, updates DB reference. Or: Re-imports as new song (behavior documented). | — |

---

## Part C: Multi-Genre Classification

### C1: Genre Assignment
**Risk:** A song assigned to only 1 genre despite fitting multiple.

| ID | Test Case | Acceptance Criteria | Pass? |
|---|---|---|---|
| **C1.1** | Song (e.g., "Blinding Lights" by The Weeknd) classified | DB record shows `genres = ["synth-pop", "electronic", "dance-pop"]`. All 3 genres linked. | — |
| **C1.2** | Song has no clear genre fit | Assigned to "Uncategorized" or "Other". Behavior consistent. Not left null. | — |
| **C1.3** | Genre list has 18 items. Song fits only 2. | Song linked to those 2 genres only. Null/empty genres not counted. | — |
| **C1.4** | Same song classified twice (edge case) | Second classification: Overwrites first. No duplicate genre links. Transaction clean. | — |

### C2: Genre Playlist Creation (12-18 Genres)
**Risk:** Not all 12-18 genres get playlists, or duplicate playlists created.

| ID | Test Case | Acceptance Criteria | Pass? |
|---|---|---|---|
| **C2.1** | Classify 200 songs with AI model; 15 unique genres emerge | Playlists: Exactly 15 created (1 per genre). No duplicates. | — |
| **C2.2** | Manual genre tags + AI classification conflict | Precedence: Defined (e.g., AI wins, or manual tags preserved). Consistent. | — |
| **C2.3** | Genre "Jazz" has 1 song (below minimum threshold?) | Behavior: Still create playlist if no minimum is enforced. Or: Merge into "Other". Documented. | — |
| **C2.4** | Playlists persist across server restarts | GET /playlists: Returns all 15 genre playlists with correct song counts. | — |

### C3: Multi-Genre UI & Navigation
**Risk:** Genre playlists not surfaced in Blazor or iOS UI.

| ID | Test Case | Acceptance Criteria | Pass? |
|---|---|---|---|
| **C3.1** | Blazor: Playlists page shows "AI Genre Playlists" section | All 15 genre playlists listed with song count and play button. | — |
| **C3.2** | iOS: PlaylistsView includes AI playlists | All AI-generated playlists shown. Play/rename/delete actions available. | — |
| **C3.3** | Click "Rock" playlist in Blazor | Navigates to playlist detail, lists songs, allows play. | — |
| **C3.4** | Click "Electronic" playlist on iOS | Navigates to playlist detail, shows songs, allows queue/play. | — |

---

## Part D: Similarity-Based Autoplay & Song Recommendations

### D1: Similarity Calculation & Scoring
**Risk:** Similarity markers are wrong or unused; "similar songs" are actually dissimilar.

| ID | Test Case | Acceptance Criteria | Pass? |
|---|---|---|---|
| **D1.1** | Song A classified: [rock, alternative, indie]. Song B classified: [rock, indie]. | Similarity score ≥ 0.66 (shared 2/3 genres). Configurable threshold. | — |
| **D1.2** | Calculate similarity for 5,000 songs (all pairs) | Completes in <5 min on typical hardware. Results persisted. | — |
| **D1.3** | Similarity graph updated after new songs classified | New song: Similarity links created to all existing songs. Existing songs get new links back. | — |
| **D1.4** | Similarity A→B = 0.8; B→C = 0.9; A→C = 0.5 | No assumption of transitivity. Stored correctly. | — |

### D2: Autoplay & Up-Next Queue
**Risk:** Autoplay picks dissimilar songs, or doesn't trigger when it should.

| ID | Test Case | Acceptance Criteria | Pass? |
|---|---|---|---|
| **D2.1** | Song ends, queue empty, autoplay enabled | Next song: Top-1 similar song added to queue, plays. | — |
| **D2.2** | Song ends, queue has 3 songs, autoplay enabled | Autoplay: Doesn't trigger (queue not empty). Next song in queue plays. | — |
| **D2.3** | Autoplay disabled | Song ends, queue empty: Stops playback. No auto-add. | — |
| **D2.4** | Song with 0 similar matches (new/unique) | Autoplay: Adds random unplayed song from library. Or: Stops. Documented. | — |

### D3: "Play Something Similar" Mode
**Risk:** User taps "Play Similar", nothing happens or wrong songs play.

| ID | Test Case | Acceptance Criteria | Pass? |
|---|---|---|---|
| **D3.1** | Blazor: Playing song X. Click "Play Similar" button | Queue cleared. Top-5 similar songs added. First plays. | — |
| **D3.2** | iOS: Playing song X. Tap "More" → "Play Similar" | Queue cleared. Top-5 similar songs enqueued. Playback continues/restarts. | — |
| **D3.3** | Play Similar on song with <2 similar matches | Add available similar songs. If <1, add random or stop (documented). | — |

---

## Part E: Thumbs Up/Down Feedback & Adaptation

### E1: Feedback Capture
**Risk:** Feedback button not visible or clicks not recorded.

| ID | Test Case | Acceptance Criteria | Pass? |
|---|---|---|---|
| **E1.1** | Blazor: Playing song. Click thumbs-up button | Icon highlights. Event logged to DB. `feedback=1`. | — |
| **E1.2** | Blazor: Same song. Click thumbs-down button | Thumbs-up clears. Thumbs-down highlights. Event logged. `feedback=-1`. | — |
| **E1.3** | iOS: Playing song. Tap 👍 icon | Icon highlights. API call: POST /api/homespeaker/songs/{id}/feedback with `value=1`. | — |
| **E1.4** | Feedback on a song already rated | Second rating: Overwrites first (no duplicate entries). Count increments. | — |
| **E1.5** | Feedback sent while offline (iOS) | Saved locally, synced on reconnect. No duplicate submissions. | — |

### E2: Feedback Persistence
**Risk:** Feedback lost on restart or not visible next session.

| ID | Test Case | Acceptance Criteria | Pass? |
|---|---|---|---|
| **E2.1** | Rate song X as 👍. Restart server. | Server: Feedback persisted. GET /song/{X} returns `userFeedback=1`. | — |
| **E2.2** | Rate 50 songs over 2 days. View feedback stats. | All 50 ratings shown. Counts correct (e.g., 35 👍, 15 👎). | — |
| **E2.3** | Feedback export/backup | Can backup rating history. Portable format (CSV/JSON). | — |

### E3: Feedback-Driven Autoplay Adaptation
**Risk:** Thumbs-down songs still appear in "similar" recommendations; no learning occurs.

| ID | Test Case | Acceptance Criteria | Pass? |
|---|---|---|---|
| **E3.1** | Song X rated 👎. Autoplay triggers. | Similar songs to X: Excluded from recommendation. Lower-ranked similar songs prioritized. | — |
| **E3.2** | Rate 10 songs 👍, 5 songs 👎. View "Play Similar." | Recommendations: Biased toward 👍 genres. 👎 genres de-prioritized. | — |
| **E3.3** | Song X rated 👎, but user manually enqueues it later | Manual queue: Always overrides feedback rules. No block. | — |
| **E3.4** | Feedback reversal: 👎 → clear → 👍 | Recommendation model: Retrains (if real-time) or updates on next classification run. | — |

### E4: Feedback UI State
**Risk:** UI shows wrong feedback state; user thinks they rated when they didn't.

| ID | Test Case | Acceptance Criteria | Pass? |
|---|---|---|---|
| **E4.1** | Blazor: Play song X (rated 👍 previously). | Button shows 👍 highlighted. Clear visual state. | — |
| **E4.2** | iOS: Play song Y (no prior rating). | Buttons (👍 👎) both unhighlighted. Tappable. | — |
| **E4.3** | Real-time sync: Rate song on Blazor, switch to iOS. | iOS: Immediately shows correct rating state. No lag. | — |

---

## Part F: Progress & Status Visibility

### F1: Blazor Progress Page
**Risk:** No visible progress page; users don't know if pipeline is running or stuck.

| ID | Test Case | Acceptance Criteria | Pass? |
|---|---|---|---|
| **F1.1** | Blazor: Menu link "AI Processing Status" (or similar) | Page shows: Total songs, classified count, % complete, ETA. Refreshes every 2-5s. | — |
| **F1.2** | Pipeline running. Progress page shows 45/200 songs classified, 22% | ETA calculated. E.g., "~3 minutes remaining" (if 2 min/50 songs). | — |
| **F1.3** | User navigates away and back. Progress updates. | Page re-fetches status. Shows current progress. No stale data. | — |
| **F1.4** | Classification paused/stopped. Status shows "Paused" or "Idle" | Controls available: Resume / Cancel. Logging visible. | — |
| **F1.5** | Playlist generation active (separate phase). Progress shows "Generating playlists: 12/15" | Each phase (classification, playlist gen) tracked separately. | — |

### F2: iOS Progress Page
**Risk:** iOS doesn't show processing status; user assumes app is broken.

| ID | Test Case | Acceptance Criteria | Pass? |
|---|---|---|---|
| **F2.1** | iOS: "More" tab or settings. Link to "AI Processing Status" | Shows: Classified / Total, % done, phase (classifying / generating playlists). | — |
| **F2.2** | Pull-to-refresh on status page | Status updates instantly. Reflects latest server state. | — |
| **F2.3** | Status page offline (server unreachable) | Graceful error message. Last known status shown with timestamp (if cached). | — |

### F3: Status Accuracy & Latency
**Risk:** Progress percentage frozen; doesn't reflect actual pipeline work.

| ID | Test Case | Acceptance Criteria | Pass? |
|---|---|---|---|
| **F3.1** | Pipeline processing songs 45-65/200 at 2 sec/song. Status endpoint polled every 2s. | Counts increment by 1-2 every 2s. No jumps or freezes. | — |
| **F3.2** | Heavy system load. Progress page remains responsive (no 5+ sec lag). | UI updates within 3s of actual pipeline progress. | — |

### F4: Notification/Alert on Completion
**Risk:** Pipeline finishes silently; user unaware that AI playlists are ready.

| ID | Test Case | Acceptance Criteria | Pass? |
|---|---|---|---|
| **F4.1** | Pipeline finishes classifying all songs & generating playlists. | Blazor: Toast notification "AI Playlists ready!" with link to view playlists. | — |
| **F4.2** | iOS: Pipeline completes. | Notification (if enabled) or status page badge (e.g., red "1" if in background). | — |
| **F4.3** | Multiple classification runs. Notification only on final completion. | Don't spam on every resumption; only on full feature completion. | — |

---

## Part G: Data Consistency & Edge Cases

### G1: Playlist-Song Linkage
**Risk:** Playlists reference deleted songs; songs in multiple genres break when reassigned.

| ID | Test Case | Acceptance Criteria | Pass? |
|---|---|---|---|
| **G1.1** | Song deleted from library. Playlists containing it updated. | Playlist: Song removed from all genre playlists. Counts decremented. | — |
| **G1.2** | Song re-classified from [Rock, Indie] → [Indie, Electronic]. | Playlists: Removed from Rock, added to Electronic. Indie unchanged. Links clean. | — |
| **G1.3** | Playlist-song join table has orphan entries (data corruption). | Recovery: DELETE FROM playlist_songs WHERE song_id NOT IN (SELECT id FROM songs). | — |

### G2: Genre Tag Stability
**Risk:** Genres change mid-pipeline; inconsistency across playlists.

| ID | Test Case | Acceptance Criteria | Pass? |
|---|---|---|---|
| **G2.1** | Genre list fixed to 15 items before classification. Classification uses that list. | No new genres created mid-run. Consistent genre set across all songs. | — |
| **G2.2** | Admin adds 16th genre mid-pipeline. | Pipeline: Either ignores (uses original 15) or includes new genre. Behavior documented. | — |

### G3: Concurrency & Locks
**Risk:** Multiple clients (Blazor + iOS) both try to rate song; race condition.

| ID | Test Case | Acceptance Criteria | Pass? |
|---|---|---|---|
| **G3.1** | Blazor client rates song 👍, iOS rates 👎 simultaneously | Database: Last write wins (timestamp). One rating recorded. Or: Conflict resolved (documented). | — |
| **G3.2** | Two browsers open, both rate same song | No duplicate entries. Single rating record. | — |

---

## Part H: Integration & End-to-End Scenarios

### H1: Full Workflow
**Risk:** Feature works in isolation but breaks when combined with existing features.

| ID | Test Case | Acceptance Criteria | Pass? |
|---|---|---|---|
| **H1.1** | 1. Start with 50 unclassified songs. 2. Run pipeline. 3. Verify 15 genre playlists created. 4. Play genre playlist. 5. Rate songs 👍👎. 6. Restart server. 7. Verify ratings persisted, playlists intact. | All steps succeed. Data consistent throughout. | — |
| **H1.2** | Playing from AI playlist. Song ends, autoplay enabled. Next similar song plays from genre (not random). | Autoplay respects genre similarity. Queue managed. | — |
| **H1.3** | Radio stream playing. Switch to AI playlist. | Playback switches cleanly. No race condition. | — |

### H2: Cross-Client Consistency
**Risk:** Blazor and iOS show different playlist counts or ratings.

| ID | Test Case | Acceptance Criteria | Pass? |
|---|---|---|---|
| **H2.1** | Blazor: 150 songs classified. iOS: Fetch playlists. | iOS shows same playlists, same song counts. Data in sync. | — |
| **H2.2** | Blazor: Rate song 👍. Switch to iOS. | iOS: Shows correct rating state immediately. No lag. | — |

### H3: Boundary Conditions
**Risk:** Feature breaks with extreme data sizes.

| ID | Test Case | Acceptance Criteria | Pass? |
|---|---|---|---|
| **H3.1** | 10,000-song library. Run full classification & playlist generation. | Completes in reasonable time (<30 min). Memory usage stays <500MB. | — |
| **H3.2** | One song rated by 100+ users (simulated). | Feedback aggregation works. No slowdown. | — |
| **H3.3** | Playlist with 200 songs. Autoplay requests similar songs. | Calculation <500ms. Doesn't block playback. | — |

---

## Risk Summary & Test Execution Strategy

### High-Risk Domains (Test First)
1. **Restart safety (A1-A3)** — 12 tests — *Criticality:* CRITICAL
   - Without this, feature is unusable in production
   - Simulate crashes, power loss, process kills
2. **Incremental pickup (B1-B3)** — 12 tests — *Criticality:* CRITICAL
   - User adds 100 songs; AI must find & classify them
3. **Progress visibility (F1-F4)** — 13 tests — *Criticality:* HIGH
   - Kiosk mode (RPi) must show status; users need confidence feature is working

### Medium-Risk Domains
4. **Multi-genre classification (C1-C3)** — 14 tests — *Criticality:* HIGH
5. **Similarity & autoplay (D1-D3)** — 13 tests — *Criticality:* HIGH
6. **Feedback loop (E1-E4)** — 13 tests — *Criticality:* MEDIUM

### Lower-Risk Domains
7. **Data consistency (G1-G3)** — 8 tests — *Criticality:* MEDIUM
8. **E2E integration (H1-H3)** — 7 tests — *Criticality:* MEDIUM

### Execution Approach
- **Phase 1 (Implementation):** Wash/Kaylee build feature
- **Phase 2 (QA Validation):** Zoe runs high-risk domain tests manually (no automation framework yet)
- **Phase 3 (Regression):** If tests pass, mark feature "Ready for Release"
- **Phase 4 (Production Monitoring):** Track restart events, pipeline failures, user feedback

---

---

## 2026-05-01: AI Playlists Readiness Verdict (Mal)

### Status
Trial-ready (limited private trial on server/Blazor path only)

### Decision

Treat the current AI playlists/music-intelligence work as **trial-ready on the server/Blazor path**, not as a finished feature.

### Key Points

- Do **not** call the iOS AI surfaces production-ready yet; the current DTO contract and progress display logic are not trustworthy enough.
- Before broad rollout, close the session-lifecycle, stale-library cleanup, and similarity-refresh gaps so AI context and recommendations stay aligned with actual playback and library state.
- If this goes to Azure now, keep it a limited/private trial and use the web/server experience as the primary validation surface.

---

## 2026-05-01: AI Playlists Readiness Decision (Zoe)

**Date:** 2026-05-01  
**Author:** Zoe  
**Status:** Proposed  
**Affects:** AI playlists, AI status, iOS UI, rollout

### Decision

Treat the AI playlists feature as **developer preview only**. Do **not** call it ready for a real user trial yet.

### What is Actually Validated

- Solution build succeeds.
- Prior smoke testing shows the Blazor routes load without immediate runtime errors.
- Provider-misconfiguration messaging on /ai-status was smoke-validated.

### What is Still Unproven or Broken

1. **iOS playlist decoding looks broken with real data**
   - AIPlaylistsView loads AiPlaylistSummaryDto.
   - Swift model decodes TrackCount instead of the server's camelCase 	rackCount.
   - Result: non-empty playlist payloads are likely to fail decode and fall back to an empty state.

2. **iOS status progress is unreliable**
   - Server status returns PercentComplete as 0-100.
   - AIStatusView multiplies it by 100 again for display and feeds the raw 0-100 value into ProgressView.
   - Result: progress can be overstated by 100x and the progress bar range is wrong.

3. **Similar-song autoplay is not user-proven**
   - API endpoints exist for similar songs and autoplay from current track.
   - I found no Blazor or iOS entry point exposing that flow to a real user.
   - I found no evidence of end-to-end validation for queue-empty handoff or similar-song sequencing.

4. **Pipeline recovery is not trial-safe yet**
   - Resume only signals the worker.
   - Failed items are counted and surfaced, but I found no retry/requeue path for ordinary failed analyses.
   - Transient provider/network failures can leave tracks stranded in failed state.

5. **User-facing error handling is weak**
   - Blazor AI playlists falls back to "no playlists available yet" on load failures.
   - Blazor AI status falls back to a default idle-looking model on load failures.
   - iOS playlist loads, play actions, and thumbs feedback mostly swallow errors.

### QA Recommendation

Before any limited real-user trial:
- Fix the iOS playlist/status contract issues
- Expose and test the similar-song autoplay flow
- Prove retry/recovery behavior for failed analysis work items
- Run end-to-end validation for: analysis → playlists appear → play playlist → thumbs feedback recorded → future ranking changes

### Rollout Call

Current recommendation: **NOT YET READY** for any user trial.

---
## Notes & Assumptions

### Assumed Decisions (to be Confirmed)
- **Genre count:** Fixed at 12-18 before classification (not dynamic)
- **Autoplay trigger:** Only when queue is empty AND autoplay enabled
- **Feedback scope:** Per-user (not global/aggregate ratings yet)
- **Similarity metric:** Jaccard index on genre set (shared / union)
- **Pipeline schedule:** Hourly or on-demand (trigger point TBD by Wash)
- **Recovery behavior:** Last known good state; no data recovery from partial writes
- **Offline mode (iOS):** Feedback cached locally, synced on reconnect (if not already decided)

### Test Environment Requirements
- SQLite database with test data: 50-200 songs, varied genres
- Blazor server running on RPi or desktop
- iOS app with connectivity to test server
- Kill/restart capabilities for crash simulation
- Database inspection tools (sqlite3, or SQL IDE)
- Manual timing tools (stopwatch or logging analysis)

---

**Next Step:** Implementation team reviews assumptions, confirms scope, and begins building. Once implementation is testable, Zoe runs validation against this matrix.


---


---

## AI Retry/Timeout Fix (2026-05-01)

### Decision 1: Wash — AI Retry Timeout Placement

**By:** Wash (Backend)  
**Date:** 2026-05-01  
**Status:** Approved (after revision by Mal)  
**Affects:** HomeSpeaker.Server2 AI music analysis worker

**What:**
- Enforce model-request timeout in AiMusicAnalyzer with linked cancellation token set to 200 seconds
- Automatically re-queue failed work items after 5-minute cooldown in worker loop

**Why:**
The analyzer-level timeout applies uniformly through IChatClient for both Azure OpenAI and public OpenAI without depending on provider-specific client plumbing. Cooldown-based requeue avoids infinite hot-loop retries while keeping failures visible.

**Rationale:** Initial iteration lacked end-to-end timeout effectiveness (see rejection below). Revised by Mal to include provider-level timeout wiring.

---

### Decision 2: Zoe — Rejection: AI Timeout Not Effective End-to-End

**By:** Zoe (Tester)  
**Date:** 2026-05-01  
**Status:** Validated & Addressed by Mal  
**Affects:** HomeSpeaker.Server2 AI timeout + retry validation

**Decision:** Reject Wash's backend change set as not fully validated for release.

**Why:**
- Batch-size reduction (6) and automatic failed-item requeue are validated ✓
- **Problem:** Live runtime/status output still shows Azure OpenAI requests timing out at  :01:40 (~100s)
- **Root cause:** Program.cs creates AzureOpenAIClient without configuring SDK network timeout
- **Evidence:**
  - AiMusicOptions + ppsettings.json set batch size 6 and timeout 200
  - AiMusicAnalysisWorker re-queues correctly; DB state shows Attempts = 2 on retries
  - /api/ai/status error messages recommend ClientPipelineOptions.NetworkTimeout

**Next Step:** Assign **Mal** to revise provider timeout wiring, then rerun validation.

---

### Decision 3: Mal — AI Provider Timeout Must Be Set at Transport

**By:** Mal (Lead)  
**Date:** 2026-05-01  
**Status:** Implemented & Approved  
**Affects:** HomeSpeaker.Server2 AI provider wiring

**What:**
For AI analysis requests, the effective timeout must be applied in the actual provider client stack, not only in AiMusicAnalyzer.

**Implementation:**
- Keep existing analyzer cancellation timeout as outer request budget
- Configure System.ClientModel client options with explicit NetworkTimeout
- Provide transport backed by HttpClient with timeout set above the analyzer budget

**Why:**
Wash's rejected version only changed outer call budget. Live failures still occurred at ~100s because SDK transport used default HttpClient.Timeout, preventing intended ~200s policy from taking effect.

**Scope:** Apply pattern to both:
- Azure OpenAI via AzureOpenAIClientOptions
- Public OpenAI via OpenAIClientOptions

Do not add extra wrapper abstractions that don't own underlying HTTP request.

**Validation:** Zoe revalidated post-revision and approved. Smoke tests passing, batch size 6 retained, auto-requeue retained.

---

---

### 2026-05-02T23:20:09Z: AI Playlist Preview Pattern
**By:** Kaylee (Frontend Dev)
**Date:** 2026-05-02
**Status:** Proposed
**Affects:** Blazor UI, AI playlist browsing

## Decision

AI playlist cards should act as preview-first entry points: tapping the card opens a details page, while playback remains a visible secondary action on both the card and the details page.

## Why

- Users asked to inspect what an AI playlist contains before pressing play.
- Full-card navigation is more obvious and touch-friendly than hiding preview behind a small affordance.
- The details table can safely render only the real scoring fields exposed by the service shape, including dynamic marker columns when present.

## UI Contract

- Gallery route: `/ai-playlists`
- Details route: `/ai-playlists/{genreKey}`
- Table shows static score fields first, then any playlist-specific marker columns supplied by the model.

---

### 2026-05-02T23:20:09Z: AI Truncated JSON Fallback
**By:** Wash (Backend Dev)
**Date:** 2026-05-02
**Status:** Implemented
**Affects:** HomeSpeaker.Server2 AI analysis

## Decision

Treat AI JSON parse failures that end with parser EOF messages (for example near `$.songs[4].genres[2]`) as truncated batch responses. Do not add broad structural JSON repair for these cases; instead tighten the output contract and retry the claimed songs individually inside the same worker pass.

## Why

The existing numeric repair only fixes narrow malformed number tokens and is not safe for incomplete arrays or objects. Broadly guessing missing structure would hide model defects and risk storing incorrect genre/marker data, while per-song fallback contains the blast radius and keeps the queue moving.

## Implementation Notes

- `AiMusicAnalyzer` now requests exactly one ordered result per input song, caps verbose text fields, and scales `MaxOutputTokens` with batch size.
- Batch JSON parse failures are classified as `TruncatedJson` vs `InvalidJson`, preserving the failing JSON path and parser detail.
- `AiMusicAnalysisWorker` retries truncated batch failures one song at a time within the same claimed batch, so successful singles can complete even when the original batch payload was cut off.

---

### 2026-05-02T23:20:09Z: AI Playlist Detail Payload Enrichment
**By:** Wash (Backend Dev)
**Date:** 2026-05-02
**Status:** Implemented
**Affects:** AI playlist detail API, Blazor server models

## Decision

Reuse the existing AI playlist detail flow (`/api/ai/playlists/{genreKey}` and `HomeSpeakerService.GetAiPlaylistAsync`) and enrich its payload rather than introducing a duplicate details-specific endpoint.

## Details

- `AiPlaylistDto` now includes `Tracks`
- each track carries:
  - `Song`
  - `GenreScore`
  - `GenreRank`
  - `Why`
  - `Markers[]` with key/value/confidence
- legacy `Songs` remains populated for backward compatibility

## Rationale

This keeps Kaylee on the existing fetch path for the detail page, avoids two near-identical playlist contracts drifting apart, and preserves current list/play consumers that only know about `Songs`.

---

### 2026-05-02T23:23:09Z: AI Playlist Card Link Navigation
**By:** Kaylee (Frontend Dev)  
**Status:** Implemented  
**Affects:** AiPlaylists.razor, card navigation UX

## Decision

AI playlist cards on /ai-playlists use real full-card anchor links that navigate to /ai-playlists/{genreKey}. The dedicated Play button remains as a separate action layered above the card link and triggers playback instead of navigation.

## Rationale

- Real links preserve expected navigation even before Blazor event handlers hydrate
- Safer for touch-first card UIs on 7-inch Raspberry Pi display
- Keeps play action separate, preventing accidental navigation when user intends playback
- Aligns with full-page details pattern established in previous AI playlist iteration

## Implementation

- Card click/tap: navigates to detail page
- Play button click/tap: triggers playback without navigation
- Styling: consolidated to AiPlaylists.razor.css only
- Build: validated ✅
---

### 2026-05-02T20:11:24Z: AI Playlists In-Progress Visibility
**By:** Kaylee (Frontend Dev)  
**Status:** Implemented  
**Affects:** AiPlaylists.razor gallery, AiMusicCatalogService

## Decision
Keep the `/ai-playlists` gallery visible during active AI processing by treating genre cards as stable playlist shells, then layering current counts/progress messaging on top instead of swapping to an empty state.

## Why
- Users should be able to browse into playlist details immediately, even when some playlists still have zero scored tracks.
- "No AI playlists available yet" reads like failure or absence, when the real state is partial/in-progress enrichment.
- Non-blocking refresh feedback keeps the page feeling alive without hiding already-loaded cards every time counts update.

## Impact
- The gallery now shows partial playlist data and links to status for live progress.
- Per-card counts can read as "so far" while processing is active.
- Detail and play flows stay unchanged.
---

### 2026-05-03T20:18:30Z: Case-insensitive AI genre aggregation
**By:** Wash (AI Engineer)  
**Status:** Implemented  
**Affects:** AiMusicCatalogService, AI Playlists Backend

## Decision
Treat AI genre keys as case-insensitive at the service boundary:
- Collapse active genre definitions by key using StringComparer.OrdinalIgnoreCase
- Fold grouped summary counts/last-updated values case-insensitively before ToDictionary
- Resolve playlist requests case-insensitively and query scores with SQLite NOCASE
- Keep partial-results behavior by deduping per-song rows and duplicate library paths instead of failing

## Why
This fixes the immediate crash without requiring a risky data cleanup or schema migration, and it matches how the UI/API already present genre keys as logical identifiers rather than case-sensitive IDs.

## Root Cause
AI playlist summaries crashed in AiMusicCatalogService.GetGenreSummariesAsync() when grouped score rows produced keys that only differed by case (for example choral and CHORAL). SQLite grouping and the current { SongPath, GenreKey } primary key treat those as distinct rows, but the service later materialized them into case-insensitive dictionaries.

---

### 2026-05-06T00:37:09Z: AI playlist detail row-level play actions
**By:** Kaylee (Frontend Dev)  
**Status:** Implemented  
**Affects:** AiPlaylistDetails page, AI playlist playback flow

## Decision
AI playlist detail row-level play actions should reuse the AI genre playback session instead of falling back to generic single-song playback.

## Why
- Keeps AI feedback/session context intact for thumbs-up/down and related AI player state.
- Lets a tapped song start immediately without throwing away the rest of the playlist.
- Preserves a consistent queue experience between "play all" and "play this track."

## Implementation Note
Start the same genre session, rotate the ranked song order so the selected track is first, then enqueue the remaining playlist tracks after it.

---

### 2026-05-06T00:37:09Z: Music-page play replaces queue through dedicated path
**By:** Wash (Backend/AI Engineer)  
**Status:** Implemented  
**Affects:** HomeSpeakerService.PlaySongsAsync, music page play dropdown

## Decision
Music-page server-side multi-song play should use a replace-queue path, not repeated enqueue calls.

## Why
The shared play dropdown is used for artist/album song collections. Reusing EnqueueSongAsync after StopPlayingAsync left old queued tracks behind because stopping playback does not clear IMusicPlayer.SongQueue.

## Implementation
HomeSpeakerService now owns a dedicated PlaySongsAsync flow that stops playback, clears the active queue, starts the first selected song immediately, and queues only the remaining selected songs. Existing add-to-queue behavior still routes through enqueue-only code paths.

---

### 2026-05-06T00:37:09Z: AI genre entry sanitization with bounded normalization
**By:** Wash (Backend/AI Engineer)  
**Status:** Implemented  
**Affects:** AiMusicAnalyzer, AI batch processing pipeline

## Context
AiMusicAnalyzer already had a narrow numeric repair pass and a truncated-batch fallback. Repeated failures like AI returned invalid JSON near $.songs[4].genres[2] were still escaping because the payload was syntactically valid JSON, but the genres item shape/value did not match AiGenreScoreResult.

## Decision
- Keep strict typed deserialization as the primary path.
- If deserialization fails on a .genres path, run a bounded JSON-node normalization pass only over songs[*].genres.
- Canonicalize known genre keys, coerce safe numeric score/rank strings into numbers, clear non-string optional why, and drop malformed genre items or non-array genres containers.
- Preserve existing numeric repair and truncated-json per-song fallback behavior unchanged.

## Why
- This contains the blast radius to the field that is actually failing instead of weakening the whole contract.
- One malformed genre item should not strand the rest of the batch, but unknown or structurally bad genre data should still be visible in logs rather than silently accepted.



# Inbox Merge — 2026-05-14T21-32-28Z

## From: book-postfix-integration-review

# Book verdict: approve after migration cleanup

- **Verdict:** Approve for release.
- **What I found:** The cutover was almost coherent, but legacy on-device offline downloads still depended on `MusicLibraryView` loading the catalog before old song-ID records could be migrated to path-based keys. That left a dead-path risk for users who updated and went straight to Offline/More without opening Library.
- **What I changed:** `HomeSpeakerMobile/iOS/OfflineDownloadsStore.swift` now resolves any legacy song-ID based downloads, queued items, failures, or active work during normal app refresh by fetching the catalog before migration when needed.
- **Validation run on this host:** `dotnet build D:\homespeaker\HomeSpeaker.sln -c Release` succeeded before and after the cleanup. Native iOS build validation is not available on this Windows host, so the Swift side was reviewed statically.
- **Release read:** Server-backed `/api/homespeaker/offline*` is now the live path, legacy local selection data is reduced to one-way migration state, and the remaining migration gap is closed.


---

## From: kaylee-offline-keying-fix

# Kaylee: Offline mobile keying fix

- Durable offline selections and downloaded-file records in `HomeSpeakerMobile/iOS/OfflineDownloadsStore.swift` now key by `Song.path` instead of `songId`.
- Local file names are now deterministic path-derived tokens per server, so local playback lookup in `HomeSpeakerMobile/iOS/LocalPlayer.swift` can prefer downloaded files by durable path.
- I added legacy manifest migration support so older `songId`-keyed track selections/download records upgrade to path-based keys after the library refreshes, instead of silently breaking existing offline saves.


---

## From: mal-final-release-review

## Mal Final Release Review — 2026-05-14

**Verdict:** REJECT

**What I validated**
- `dotnet build HomeSpeaker.sln` ✅
- `dotnet test HomeSpeaker.sln` ✅, but there are still no real automated test projects
- `dotnet ef migrations has-pending-model-changes --project HomeSpeaker.Server2\HomeSpeaker.Server2.csproj --startup-project HomeSpeaker.Server2\HomeSpeaker.Server2.csproj` ✅ no pending model drift

**Why this is not ready**
1. **`Play Fun Music` is wired wrong.**  
   `HomeSpeakerMobile\iOS\Intents\HomeSpeakerIntents.swift` does fuzzy alias matching for `"fun music"` / `"fun"` against playlist display names and genre keys. The agreed mapping was direct to AI genre `family-singalong`. As written, the command can fail unless there happens to be a literal “fun music” playlist.

2. **`Quiet Down` violates the contract on both surfaces.**  
   `HomeSpeakerMobile\iOS\Intents\HomeSpeakerIntents.swift` and `HomeSpeakerMobile\watchOS\Widget\WidgetIntents.swift` both use `max(0, status.volume / 2)`. That drops volume `1` to `0`. The call was: halve it, but clamp non-zero volume to at least `1`.

3. **Offline summary lies after failures.**  
   `HomeSpeakerMobile\iOS\OfflineDownloadsStore.swift` counts every non-downloaded item as pending, including `.failed`. That means More/Offline can say “pending” forever when the real state is “failed”. That is sloppy UX, not ship-ready UX.

**Rejected artifacts / revision owner**
- `HomeSpeakerMobile\iOS\Intents\HomeSpeakerIntents.swift` → **Wash**
- `HomeSpeakerMobile\watchOS\Widget\WidgetIntents.swift` → **Wash**
- `HomeSpeakerMobile\iOS\OfflineDownloadsStore.swift` → **Wash**

**Notes**
- I did **not** find a migration blocker after re-checking EF state.
- I still cannot device-test Siri or iOS playback from this Windows host, so the mobile path remains code-reviewed, not device-proven.


---

## From: mal-offline-unification

# Offline unification decision

## Decision
The iOS client will use the existing server offline contract as the source of truth: `GET /api/homespeaker/offline` for manifest state, `POST /api/homespeaker/offline/targets` to mark artist/album/song content, `DELETE /api/homespeaker/offline/targets/{id}` to unmark it, and `/api/homespeaker/offline/media` for media bytes.

## Why
The previous mobile flow was a second manifest system living only on-device. That was scope creep and it drifted from the backend contract we already shipped. The fix is to keep server-side selection rules on the server and keep only per-device file presence, queue state, and failure state on the phone.

## Implementation notes
- Durable local file tracking stays keyed by `Song.Path`, not `SongId`.
- Legacy local selection state is migrated forward on first sync by posting equivalent server targets, then clearing the stale local rule.
- Local playback still prefers an on-device file first; when it has to stream, it should use the offline media path keyed by `songPath` instead of the old `songId` media route.
- Failed downloads are a separate local state and must not be counted as pending.


---

## From: mal-siri-offline-plan

# Siri + Offline Downloads Plan

**By:** Mal  
**Status:** Approved for implementation

## What I reviewed

- Siri/App Intents currently lean on free-form `MediaQueryEntity` phrases and fuzzy matching in `HomeSpeakerMobile\iOS\Intents\HomeSpeakerIntents.swift` and `HomeSpeakerMobile\iOS\Intents\HomeSpeakerShortcuts.swift`.
- Intent helpers read/write `UserDefaults.standard`, while widget code already uses the shared app-group store. That is the wrong shape for reliable cross-process Siri state.
- Mobile library browsing already has enough shape to group artists/albums client-side from `Song` rows in `MusicLibraryView.swift`; we do not need new artist/album APIs for offline selection.
- Server already exposes enough for v1 downloads: `GET /api/homespeaker/songs`, album art, and ranged media streaming at `GET /api/music/{songId}`.

## Decisions

### 1) Siri scope gets smaller, not smarter

Stop trying to make Siri understand general artist/album/playlist commands. That path is too ambiguous and the current free-text/fuzzy-match design makes it worse.

For Siri, support only these five explicit commands:

1. Next song
2. Play fun music
3. Play hymns
4. Quiet down
5. Stop

Everything else stays in the app UI.

### 2) Siri intents should be dedicated and parameterless

Implement five dedicated intents instead of parameterized media queries:

- `NextSongIntent`
- `PlayFunMusicIntent`
- `PlayHymnsIntent`
- `QuietDownIntent`
- `StopPlaybackIntent`

These intents should:

- use the currently selected server only
- run without opening the app (`openAppWhenRun = false`)
- reuse the same control code path for widget + Siri where possible
- use brand-first phrases so Siri hears a distinct command, not a generic media verb

Recommended phrases:

- “Next song on HomeSpeaker”
- “Play fun music on HomeSpeaker”
- “Play hymns on HomeSpeaker”
- “Quiet down HomeSpeaker”
- “Stop HomeSpeaker”

### 3) Fix shared state before blaming Siri again

The intent layer currently reads `hs_connections` and `hs_selectedId` from `UserDefaults.standard`. That is not a dependable cross-process contract for intents/widgets.

Implementation direction:

- add the iOS app + Siri/App Intents target to the same app group already used by watch/widget (`group.com.homespeaker`)
- move connection persistence behind one shared helper/store
- stop reading/writing connection data directly from multiple places

If this is not fixed, Siri reliability will stay suspect even with better phrases.

### 4) “Play fun music” maps to an existing AI genre, not a new taxonomy

Do not invent a tagging system for this.

Map:

- **play fun music** → AI genre key **`family-singalong`**
- **play hymns** → AI genre key **`hymns`**

Why:

- `family-singalong` already exists and is the closest broad “fun” bucket.
- `hymns` already exists as a first-class AI genre.
- These are stable, explicit keys. No fuzzy genre lookup needed.

If an AI playlist is unavailable or empty, fail with a plain spoken error. Do not add fallback heuristics in v1.

### 5) “Quiet down” is a relative volume intent

`QuietDownIntent` should:

1. fetch current player status
2. read current volume
3. set volume to half the current value

Rule:

- if current volume is greater than 0, clamp to at least 1
- if current volume is 0, leave it at 0

That matches the user’s request without inventing a new volume profile system.

## Offline downloads architecture

### 6) Offline is a device feature, so keep state on the device

Do **not** add server-side user download tables for v1. This is iPhone-specific behavior.

Use a client-owned download store that persists:

- offline selection rules
- expanded per-track download state
- local file metadata
- download progress / failure state

### 7) Use selection rules, not copied artist/album objects

The minimal model is:

- **artist rule** keyed by artist name
- **album rule** keyed by artist name + album name
- **song rule** keyed by song path

Why this shape:

- mobile already gets flat `Song` rows and groups them locally
- album name alone is not unique enough
- `SongId` is not durable enough for offline storage

### 8) The durable identity is `Song.Path`, not `SongId`

Anything persisted for offline must key off `Song.Path`.

`SongId` is runtime/library-order data and can move around between scans. Use it only as the current server download handle after a fresh library sync.

### 9) Expand rules client-side from the existing song catalog

The app already downloads the library via `GET /api/homespeaker/songs`.

Use that catalog to:

- expand artist/album/song rules into a concrete song set
- determine what is missing locally
- determine what is stale/orphaned
- map persisted `songPath` back to the current `songId` when a download is needed

No new artist/album catalog endpoints are required for v1.

### 10) Reuse the existing media endpoint for downloads

For v1, download audio from the existing endpoint:

- `GET /api/music/{songId}`

That endpoint already supports range requests and content length. Good enough.

Store downloaded files in:

- app `Application Support`
- dedicated subfolder like `HomeSpeakerDownloads`

Do **not** store offline audio in cache directories.

### 11) Local playback should prefer disk, then fall back to stream

When destination is “This iPhone”:

- if a local file exists for `songPath`, play the local file
- otherwise stream from the server as today

Do this inside the local playback path. No extra abstraction circus.

### 12) Download management needs one real screen

Add a **Downloads** screen under **More** with four sections:

1. **Storage summary** — downloaded track count + bytes used
2. **Offline selections** — artists/albums/songs marked for offline use
3. **Active / failed downloads** — progress, retry, error state
4. **Downloaded tracks** — remove local copy

Library actions should be simple:

- artist context menu: Keep Offline / Remove Offline
- album context menu: Keep Offline / Remove Offline
- song context menu: Keep Offline / Remove Offline

Removing a rule should remove local files only when no remaining rule still covers that track.

## What stays out of scope

- no cross-device download sync
- no server-side user profiles
- no zip exports
- no transcoding pipeline
- no “smart” fallback taxonomy for Siri

That is scope creep. Ship the simple version first.


---

## From: river-siri-offline

# River — Siri and Offline Download Notes

## Decision
- Siri voice controls should use fixed, app-scoped phrases for the exact actions Jonathan asked for: next song, play fun music, play hymns, quiet down, and stop.
- Keep the older free-form media intents undiscoverable so Siri is steered toward the explicit commands instead of ambiguous generic matching.
- Offline mobile downloads do **not** require a new server endpoint right now; the iOS app can reuse the existing `GET /api/music/{songId}` audio route and manage its own local manifest plus file storage.

## Why
- App-scoped phrases are more reliable for Siri than broad "play X" patterns that compete with system music intents.
- Reusing the existing song media route keeps the mobile work self-contained and avoids blocking on server changes.
- A local manifest gives the app a native download-management surface without changing the C# server while other backend work is in flight.

## Files
- `HomeSpeakerMobile/iOS/Intents/HomeSpeakerIntents.swift`
- `HomeSpeakerMobile/iOS/Intents/HomeSpeakerShortcuts.swift`
- `HomeSpeakerMobile/iOS/OfflineDownloadsStore.swift`
- `HomeSpeakerMobile/iOS/Views/OfflineDownloadsView.swift`
- `HomeSpeakerMobile/iOS/Views/MusicLibraryView.swift`


---

## From: wash-offline-downloads

# Wash — Offline downloads decision

## Decision
Use a manifest-style offline API instead of building a new sync/download subsystem on the server.

## Details
- Persist only offline selection rules in SQLite: artist, album, and song targets.
- Use `Song.Path` as the durable identity for offline media, because in-memory `SongId` values are assigned during library scans and are not stable enough for long-lived mobile download state.
- Resolve targets to concrete songs on demand from the current library snapshot.
- Expose one manifest endpoint for targets + resolved songs, one endpoint to add a target, one to remove a target, and one validated media endpoint for downloading playable bytes.
- The media endpoint only serves files that currently exist in the library and returns range/ETag/last-modified metadata for resumable mobile downloads.

## Why
This keeps the server-side change small, matches the existing path-based playlist/AI patterns, and leaves device-local download progress/state in the mobile app where it belongs.


---

## From: wash-release-fixes

# Wash — release fixes

- **Date:** 2026-05-14
- **Scope:** `HomeSpeakerMobile` Siri shortcut fixes and offline download summary/status fixes

## Decision

For release, fixed-command Siri actions must use exact decided targets, and offline download summaries must distinguish failed work from pending work.

## Applied changes

1. `Play Fun Music` now calls AI genre key `family-singalong` directly in `HomeSpeakerMobile\iOS\Intents\HomeSpeakerIntents.swift`.
2. `Quiet Down` now uses a shared `PlayerStatus.quietDownVolume` rule in `HomeSpeakerMobile\Shared\Models.swift`, consumed by both iOS intents and the watch widget, so non-zero volume never collapses to zero unexpectedly.
3. `OfflineDownloadsStore` now counts only `.pending`, `.queued`, and `.downloading` as pending, exposes a separate failed count, and summarizes failures separately. `OfflineDownloadsView` surfaces the failed count when present.

## Validation

- `dotnet build HomeSpeaker.sln` ✅
- `dotnet test HomeSpeaker.sln` ✅
- `dotnet ef migrations has-pending-model-changes --project HomeSpeaker.Server2\HomeSpeaker.Server2.csproj --startup-project HomeSpeaker.Server2\HomeSpeaker.Server2.csproj` ✅
- Swift/Xcode validation could not run on this Windows host because `swift` and `xcodebuild` are not installed.


---

## From: zoe-final-release-review

# Zoe — Final Release Review

- **Verdict:** ❌ Reject
- **Reviewer:** Zoe
- **Date:** 2026-05-14

## What I rechecked
- `dotnet build D:\homespeaker\HomeSpeaker.sln` passed.
- `dotnet test D:\homespeaker\HomeSpeaker.sln` passed, but there are still no meaningful automated test projects in the solution.
- Windows runtime smoke passed for `/health`, `/api/homespeaker/offline`, `/`, `/music`, and `/playlists`.
- Startup migration behavior looked healthy on a scratch SQLite database: the app came up cleanly and created `OfflineDownloadTargets` plus its unique index.
- The requested Siri commands are present in code as explicit app intents and shortcuts: next song, fun music, hymns, quiet down, and stop.

## Blocking issue
- The offline implementation is still split into two separate systems that are not wired together.
- Server side:
  - `HomeSpeaker.Server2\Services\OfflineDownloadService.cs`
  - `HomeSpeaker.Server2\Endpoints\HomeSpeakerRestEndpoints.cs`
  - `HomeSpeaker.Server2\Data\MusicContext.cs`
  - `HomeSpeaker.Server2\Migrations\20260514202804_AddOfflineDownloadTargets.cs`
  - These persist offline targets in SQLite and expose `/api/homespeaker/offline*`.
- iOS side:
  - `HomeSpeakerMobile\iOS\OfflineDownloadsStore.swift`
  - `HomeSpeakerMobile\Shared\APIClient.swift`
  - This still keeps a separate local `manifest.json` and downloads media directly from `/api/music/{songId}`.
- I found no mobile calls to:
  - `GET /api/homespeaker/offline`
  - `POST /api/homespeaker/offline/targets`
  - `DELETE /api/homespeaker/offline/targets/{targetId}`
  - `GET /api/homespeaker/offline/media`
- Result: the new migration-backed offline target store is effectively dead code from the shipping mobile flow. That is the kind of partial rollout that turns into embarrassing release confusion later.

## Release stance
Do **not** commit/push this as final.

## Revision owner
Route the revision to **Mal**.

Reason: this is now a contract/integration correction across server persistence, shared API surface, and mobile behavior. It should not go back to **Wash** (he touched the rejected server-side offline contract) or to the earlier locked-out path; it needs a clean integration pass by a different owner.

## Residual limitation
- I still could not run Xcode, iOS simulator, or watchOS device validation from this Windows host, so Siri/watch execution is code-reviewed only here.


---

## From: zoe-offline-rereview

# Zoe — Offline mobile re-review verdict

## Verdict
✅ **Approved** for the requested re-review scope.

## What I re-verified
- `dotnet build D:\homespeaker\HomeSpeaker.sln` passed on this host.
- `dotnet test D:\homespeaker\HomeSpeaker.sln` passed on this host, but there are still no meaningful automated test projects in the solution.
- The original blocker is fixed in the revised mobile artifact:
  - `HomeSpeakerMobile\iOS\OfflineDownloadsStore.swift` now stores track selections and download records by `songPath`.
  - Downloaded-file lookup now resolves by `songPath` + `connectionId`.
  - `HomeSpeakerMobile\iOS\LocalPlayer.swift` now checks for a local file by `Song.path` before falling back to `/api/music/{songId}`.
- Library/offline management UI wiring remains connected through:
  - `HomeSpeakerMobile\iOS\Views\MusicLibraryView.swift`
  - `HomeSpeakerMobile\iOS\Views\MoreView.swift`
  - `HomeSpeakerMobile\iOS\Views\OfflineDownloadsView.swift`
  - `HomeSpeakerMobile\iOS\HomeSpeakerApp.swift`

## Acceptable residual limitation
- Legacy migration from the old `songId`-based manifest is only **best-effort**. Because the old key was scan-order based, a library that changed before first refresh after upgrade may not let every pre-existing saved item migrate perfectly.
- I could not run Xcode/iOS simulator checks from this Windows host, so this approval is based on code review plus available host checks, not on-device execution.

## Release stance
For the specific blocker I raised last time, I am satisfied: durable offline state and local lookup are now keyed by `Song.Path` rather than transient `songId`.


---

## From: zoe-postfix-release-review

# Zoe Post-fix Release Review

**Date:** 2026-05-14  
**Reviewer:** Zoe  
**Verdict:** ✅ APPROVED FOR RELEASE

## What I verified
- Siri fixed-command wiring is aligned with the requested behavior in code:
  - `Play Fun Music` calls AI genre `family-singalong`
  - `Quiet Down` uses the shared clamp helper so non-zero volume never drops below 1
  - watch/widget quiet-down uses the same clamp path
- Offline summary reporting now separates failed downloads from pending downloads.
- The iOS offline flow is now using the `/api/homespeaker/offline*` contract rather than the older local-only rule flow.
- Durable offline identity is based on `Song.path`, carried through manifest state, local-file lookup, and local playback fallback.

## Final issue I found and fixed during review
Offline manifest creation worked, but actual media download was still broken on this host because `OfflineDownloadService` passed catalog-relative song paths into `Results.File`. That caused live `/api/homespeaker/offline/media` requests to fail with file-not-found even after a target was created successfully. I fixed the service to resolve catalog paths to full filesystem paths before serving the file, then re-ran the runtime check.

## Host validation run
- `dotnet build D:\homespeaker\HomeSpeaker.sln -c Release`
- `dotnet test D:\homespeaker\HomeSpeaker.sln -c Release --no-build`
  - no real automated test projects are present yet, so this still only proves solution health/buildability
- Runtime smoke passed:
  - `GET /health`
  - `GET /api/homespeaker/offline`
  - `GET /`
  - `GET /music`
  - `GET /playlists`
- Live offline contract exercise passed:
  - created a song offline target
  - confirmed it appeared in the manifest
  - downloaded the generated media URL successfully
  - removed the target and confirmed the manifest returned to empty

## Residual limitation
I cannot run Xcode, the iOS simulator, Siri invocation, or watch execution from this Windows host. Those parts are code-reviewed only in this pass.

## Release call
Approve this working tree for release based on the validated server/runtime behavior available on this host.


---

## From: zoe-siri-offline-final-review

# Zoe Siri + Offline Final Review

- **Verdict:** ❌ Reject for final requested scope
- **Reviewer:** Zoe
- **Date:** 2026-05-14

## What passed
- Server validation on this host passed: `dotnet build` and `dotnet test` both succeeded, `/health` returned Healthy, `/api/homespeaker/offline` returned 200, and `/`, `/music`, `/playlists` all returned 200.
- Siri/App Intents are now explicit and app-scoped in code for the five requested commands:
  - next song
  - play fun music
  - play hymns
  - quiet down
  - stop
- The iPhone app now has offline marking affordances for artists, albums, and tracks, plus a dedicated Offline Downloads management screen.
- Local playback is coded to prefer a downloaded file before falling back to the server stream.

## Blocking issue
- The offline implementation still keys durable local state by `songId` instead of `Song.Path`.
- In this repo, `SongId` is not durable: `HomeSpeaker.Server2\Data\OnDiskDataStore.cs` assigns IDs from the current scan order (`song.SongId = songs.Count`).
- `HomeSpeakerMobile\iOS\OfflineDownloadsStore.swift` persists selections/downloads and file names by `songId`, and `HomeSpeakerMobile\iOS\LocalPlayer.swift` resolves downloaded files by `songId`.
- Result: after a library rescan or file-set change, saved downloads can map to the wrong song or appear missing. That is not reliable enough for approval and does not match the accepted decision to key durable download state by `Song.Path`.

## Rejected artifacts and reassignment
- `HomeSpeakerMobile\iOS\OfflineDownloadsStore.swift` → **Kaylee** (River lockout)
- `HomeSpeakerMobile\iOS\LocalPlayer.swift` → **Kaylee** (River lockout)
- `HomeSpeakerMobile\iOS\Views\MusicLibraryView.swift` → **Kaylee** (River lockout)

## QA note
- I could not run Xcode or iOS simulator/device validation from this Windows host, so Siri/device playback remains code-reviewed only here.


---

## From: zoe-siri-offline-qa

---
date: 2026-05-14
author: Zoe
status: Rejected
affects: iOS Siri intents, mobile offline downloads, QA coverage
---

# Siri Commands + Offline Downloads QA Decision

## Decision

Reject the current state for the requested Siri/offline scope.

## Why

- The existing Siri/App Intents implementation only supports content lookup phrases for artist, album, playlist, AI playlist, and stream playback.
- The requested explicit commands are missing: **skip to the next song**, **play fun music**, **play hymns**, **quiet down**, and **stop**.
- Shortcut phrases were changed from `"on"` to `"in"`, but `parseContentAndServer()` still only extracts server hints from `" on "`, so phrase parsing is internally inconsistent.
- There is no offline download workflow to validate yet: no downloadable-library state, no local file persistence model, no download manager UI, and no manage-downloads surface.

## QA Notes

### Automated coverage reality
- `dotnet build D:\homespeaker\HomeSpeaker.sln` passes.
- `dotnet test D:\homespeaker\HomeSpeaker.sln` passes but there are still no automated tests in the solution.
- I found no practical automated test path for the Swift client in this Windows environment, so this scope currently needs manual validation once implementation exists.

### Manual cases to require on revision
1. Siri recognition for the five exact requested commands with app open, backgrounded, and closed.
2. Siri recognition without requiring the app name in the spoken phrase.
3. Quiet-down behavior halves current volume, rounds predictably, clamps at 0, and survives transient status/API failures.
4. "Fun music" and "hymns" resolve to deterministic content sources and fail with a user-meaningful error if that source is unavailable.
5. Offline workflow supports mark/unmark at track, album, and artist levels.
6. Download manager shows queued/downloading/downloaded/failed states and supports retry/delete.
7. Offline playback still works after app relaunch and when the server is unavailable.
8. Removing a download updates parent album/artist state correctly.

## Reviewer lockout / next owner

- **Kaylee** should own the Siri revision.
- **Mal + Kaylee** should own the offline download contract and UI revision.

Do not send this revision back through the same shortcut approach unchanged.


---



