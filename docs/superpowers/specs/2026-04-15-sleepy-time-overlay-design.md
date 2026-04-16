# Sleepy-Time Dim Overlay

**Date:** 2026-04-15  
**Status:** Approved (Option B selected)

## Summary

After 22:00, a near-black full-screen overlay covers the UI to dim the display. Tapping or clicking anywhere dismisses it. If the user has no interaction for 1 minute, the overlay reappears. The overlay deactivates at 06:30.

---

## Active Window

- **On:** 22:00 (10 PM)
- **Off:** 06:30 AM

Outside this window the overlay never appears and all timers are stopped.

---

## Overlay Visual

- Fixed full-screen `<div>` covering the entire viewport
- `position: fixed; inset: 0; z-index: 9999`
- `background: rgba(0, 0, 0, 0.97)` — nearly opaque black
- No text or content — pure dark curtain
- `cursor: pointer` to hint that touching dismisses it
- A single `@onclick` handler calls dismiss

---

## Components

### `MainLayout.razor` (modified)

Add overlay `<div>` and all C# state/logic.

**State:**
- `bool _overlayVisible` — controls whether the overlay renders
- `bool _watching` — whether JS idle detection is currently armed
- `Timer _windowTimer` — fires every 60 seconds; checks whether current local time is in 22:00–06:30
- `Timer _idleTimer` — one-shot, 60 seconds; fires to show the overlay
- `DotNetObjectReference<MainLayout> _dotnetRef` — passed to JS for callbacks

**Flow:**
1. `OnInitializedAsync`: create `_dotnetRef`, start `_windowTimer` (fire immediately + every 60s)
2. Window timer tick:
   - If in active window and not yet watching → call `sleepyTime.startWatching(_dotnetRef)`, set `_watching = true`, start `_idleTimer`
   - If outside active window and watching → call `sleepyTime.stopWatching()`, set `_watching = false`, cancel `_idleTimer`, hide overlay
3. `[JSInvokable] OnUserActivity()`: hide overlay, restart `_idleTimer` (cancel + re-schedule 60s)
4. Idle timer fires: set `_overlayVisible = true`, call `InvokeAsync(StateHasChanged)`
5. Overlay `@onclick`: calls `OnUserActivity()`
6. `Dispose()`: cancel both timers, if watching call `sleepyTime.stopWatching()`, dispose `_dotnetRef`

**Time-window helper (pure C#):**
```csharp
private static bool IsInSleepWindow()
{
    var now = DateTime.Now.TimeOfDay;
    var start = new TimeSpan(22, 0, 0);
    var end   = new TimeSpan(6, 30, 0);
    // window wraps midnight: active if >= 22:00 OR < 06:30
    return now >= start || now < end;
}
```

### `wwwroot/js/sleepyTime.js` (new file)

Small ES module for document-level idle detection.

```js
let _dotnet = null;

export function startWatching(dotnetRef) {
    _dotnet = dotnetRef;
    document.addEventListener('pointerdown', resetIdle);
    document.addEventListener('keydown', resetIdle);
}

export function stopWatching() {
    document.removeEventListener('pointerdown', resetIdle);
    document.removeEventListener('keydown', resetIdle);
    _dotnet = null;
}

function resetIdle() {
    _dotnet?.invokeMethodAsync('OnUserActivity');
}
```

---

## JS Interop

`sleepyTime.js` is loaded via `<script>` tag in `index.html` (existing pattern for `keyboard.js` and `audioPlayer.js`). JS functions are called via `IJSRuntime.InvokeVoidAsync`.

---

## Edge Cases

- **Timer callbacks run on a thread pool thread:** All state mutations inside timer callbacks must be wrapped in `InvokeAsync(() => { ... StateHasChanged(); })` to marshal back to the Blazor sync context.
- **Component disposed while timer is running:** `Dispose()` cancels timers before they can fire again; `_dotnetRef` is disposed so any in-flight JS callback is a no-op.
- **Page loaded during active window:** Window timer fires immediately on init, so the overlay arms without waiting 60 seconds.
