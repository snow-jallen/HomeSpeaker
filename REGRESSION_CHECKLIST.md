# WebAssembly → SSR Migration: Validation Checklist (Zoe's Quick Reference)

**Status:** Pre-Migration Inspection Complete  
**Date:** 2025-03-24  
**Target:** Blazor SSR or Server-Interactive (TBD by dev team)

---

## What's Changing

| Aspect | Before | After |
|--------|--------|-------|
| **Frontend** | Blazor WASM (client-side .NET) | Blazor SSR or Server-Interactive (server-side .NET) |
| **API Protocol (Browser)** | gRPC-Web | HTTP REST + SignalR/SSE |
| **API Protocol (iOS)** | HTTP REST | HTTP REST (no change) ✓ |
| **Real-Time Events** | gRPC streaming | SignalR Hub or Server-Sent Events |
| **Projects** | Server2 + WebAssembly + Shared | Server2 + Shared (WASM deleted) |

---

## What Doesn't Change (Safety Points)

✓ REST API endpoints (`/api/homespeaker/*`) — iOS depends on these  
✓ Database schema and persistence logic  
✓ AirPlay receiver service  
✓ Health features (temperature, blood sugar, forecast)  
✓ Docker deployment and CI/CD structure

---

## Critical Tests (Must Pass)

### 1. **REST API Integrity** (Protects iOS)
```bash
curl -k https://localhost/api/homespeaker/songs
curl -k https://localhost/api/homespeaker/player/status
curl -k -X POST https://localhost/api/homespeaker/player/control -d '{"play":true}'
```
✓ All return 200 OK with expected JSON structure

### 2. **Player Control & Streaming**
- Play a song → audio plays within 2s
- Skip → next song plays immediately  
- Player status updates visible in UI (no page refresh needed)
- Volume slider works

**Measurement:** Record from play to audio = ?ms (should be <2000ms)

### 3. **Page Rendering** (Each page)
- [ ] Index.razor (home/now-playing)
- [ ] Music.razor (library browser)
- [ ] Queue.razor (current queue)
- [ ] Playlists.razor (playlist management)
- [ ] Streams.razor (radio)
- [ ] YouTube.razor (video integration)

Each must load, render, and be interactive without JS errors.

### 4. **WebAssembly Removal**
- [ ] `HomeSpeaker.WebAssembly` not in solution
- [ ] No WASM project references in `Server2.csproj`
- [ ] `build` succeeds
- [ ] No "UseWebAssemblyDebugging()" in Program.cs

### 5. **Touch Experience** (RPi 7" screen)
- All buttons ≥44px tall/wide
- Play/pause ≥56px
- No hover-only UI
- Scroll smooth, no jank

### 6. **iOS Client** (If device available)
- Connect to backend
- Fetch songs, play, skip, volume
- No 404/500 errors

---

## Highest-Risk Areas (Watch These Carefully)

1. **Real-time event streaming** — SignalR/SSE must push player updates to browser
   - Test: Start playing → watch DevTools Network > WS for messages
2. **Touch responsiveness** — Must test on **actual RPi hardware**, not just desktop
   - Test: Tap play button; measure response time
3. **Page load performance** — SSR may be slower than WASM on low-power RPi
   - Target: <3s page load time on RPi
4. **iOS doesn't break** — Every REST endpoint must remain unchanged
   - Test: iOS app connects and works (not just API testing)

---

## Test Environment Setup

### Local Testing
```bash
# Terminal 1: Start server
cd ~/code/homespeaker
dotnet run --project HomeSpeaker.Server2

# Terminal 2: Test REST API
curl -k https://localhost/api/homespeaker/player/status | jq .

# Browser: Chrome > chrome://inspect
# Device: Raspberry Pi (if available)
```

### Docker Testing (Matches production)
```bash
docker build -f HomeSpeaker.Server2/Dockerfile -t homespeaker:test .
docker run -it --rm -p 443:443 homespeaker:test
# Then test in browser & via curl
```

---

## Approval Gate (Before Shipping)

**I will NOT mark this "ready for production" until:**

- ✓ All pages render without JS errors
- ✓ REST API responds identically to iOS test calls
- ✓ Player playback & control works (audio plays, skips, volume works)
- ✓ Real-time updates visible (played song updates UI without refresh)
- ✓ Touch targets are adequate (44px minimum measured in DevTools)
- ✓ Docker image builds and runs
- ✓ Database persists across server restart
- ✓ No gRPC-Web errors in console (if gRPC middleware removed)

**If any of these fail, I will:**
1. Identify the specific issue
2. Suggest which agent should fix it (Kaylee for UI, Wash for backend, etc.)
3. Re-test once fixed

---

## Smoke Test Walkthrough (Day of Migration)

**Estimated time:** 45 minutes

1. **Start server** (5 min)
   - `dotnet run` completes without errors
   - `curl -k https://localhost/` returns HTML

2. **REST API validation** (10 min)
   - Curl each endpoint; verify 200 OK & correct JSON structure
   - Test: GET songs, GET player status, POST play/pause/skip

3. **Homepage** (5 min)
   - Navigate to `https://localhost/`
   - Now-playing card visible (or empty if no song playing)
   - No console errors (F12)

4. **Music library** (10 min)
   - Navigate to /music (or wherever Music page lives)
   - Songs load (at least 10 visible)
   - Click a song → plays in <2s
   - Skip → next song plays immediately

5. **Queue & Playlists** (10 min)
   - Queue page shows current playing song
   - Create a playlist (if UI exists)
   - No errors

6. **Touch test** (RPi only, 5 min)
   - Tap play button with finger
   - Responsiveness adequate (no delay)
   - Scale: playback starts within 1–2s of tap

---

## Known Unknowns (Decisions Pending)

1. **SSR or Server-Interactive?** — Not yet decided; both are valid
2. **SignalR or Server-Sent Events?** — Depends on team preference
3. **Remove gRPC entirely or keep for iOS?** — iOS code needs audit (currently only uses REST)

**Action:** Dev team decides before starting implementation.

---

**Questions? See full regression plan:** `.squad/decisions/inbox/zoe-regression-plan.md`

— Zoe, 2025-03-24
