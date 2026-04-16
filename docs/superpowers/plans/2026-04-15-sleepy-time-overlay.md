# Sleepy-Time Dim Overlay Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** After 22:00, cover the entire UI with a near-black overlay that dismisses on touch/click and reappears after 1 minute of inactivity; overlay deactivates at 06:30.

**Architecture:** A small vanilla-JS module (`sleepyTime.js`) attaches document-level pointer/key listeners and calls back into .NET via `DotNetObjectReference`. `MainLayout.razor` owns all state: two `System.Threading.Timer` instances (window check every 60s, idle one-shot 60s) and a boolean that toggles the overlay `<div>`.

**Tech Stack:** Blazor WebAssembly (.NET 10), `System.Threading.Timer`, `IJSRuntime`, vanilla JS (IIFE global pattern matching existing `keyboard.js`)

---

## File Map

| File | Action | Responsibility |
|------|--------|----------------|
| `HomeSpeaker.WebAssembly/wwwroot/js/sleepyTime.js` | Create | Document-level idle detection; calls `OnUserActivity` on .NET ref |
| `HomeSpeaker.WebAssembly/wwwroot/index.html` | Modify | Add `<script src="sleepyTime.js">` after existing script tags |
| `HomeSpeaker.WebAssembly/Components/Layout/MainLayout.razor` | Modify | Add overlay `<div>`, CSS, and all C# timer/state logic |

---

## Task 1: Create `sleepyTime.js`

**Files:**
- Create: `HomeSpeaker.WebAssembly/wwwroot/js/sleepyTime.js`

- [ ] **Step 1: Create the JS file**

```js
// sleepyTime.js — idle detection for the sleepy-time overlay
// Uses IIFE pattern (matching keyboard.js) so it attaches to window.
window.sleepyTime = (function () {
    let _dotnet = null;

    function resetIdle() {
        if (_dotnet) {
            _dotnet.invokeMethodAsync('OnUserActivity');
        }
    }

    return {
        startWatching: function (dotnetRef) {
            if (_dotnet) return; // already watching
            _dotnet = dotnetRef;
            document.addEventListener('pointerdown', resetIdle);
            document.addEventListener('keydown', resetIdle);
        },

        stopWatching: function () {
            document.removeEventListener('pointerdown', resetIdle);
            document.removeEventListener('keydown', resetIdle);
            _dotnet = null;
        }
    };
})();
```

> **Why IIFE / not ES module:** `index.html` loads `keyboard.js` as a plain `<script>` tag (no `type="module"`). This file follows the same pattern so `window.sleepyTime` is available globally, consistent with how `window.homeSpeakerKeyboard` works.

> **Why named function `resetIdle` (not arrow/anonymous):** `addEventListener` and `removeEventListener` must receive the exact same function reference. A named function in the closure satisfies this without storing a bound copy.

- [ ] **Step 2: Verify the file was created**

```
ls HomeSpeaker.WebAssembly/wwwroot/js/
```

Expected: `audioPlayer.js  sleepyTime.js`

- [ ] **Step 3: Build to confirm no issues so far**

```
dotnet build HomeSpeaker.WebAssembly/HomeSpeaker.WebAssembly.csproj
```

Expected: `Build succeeded.  0 Error(s)`

---

## Task 2: Load `sleepyTime.js` in `index.html`

**Files:**
- Modify: `HomeSpeaker.WebAssembly/wwwroot/index.html`

- [ ] **Step 1: Open `index.html` and find the existing script block**

The relevant section (around line 32–35) looks like:
```html
    <script src="_framework/blazor.webassembly.js"></script>
    <script src="_content/MudBlazor/MudBlazor.min.js"></script>
    <script src="https://cdn.jsdelivr.net/npm/bootstrap@5.3.0/dist/js/bootstrap.bundle.min.js"></script>
    <script src="keyboard.js"></script>
```

- [ ] **Step 2: Add `sleepyTime.js` script tag immediately after `keyboard.js`**

Find this exact line:
```html
    <script src="keyboard.js"></script>
```

Replace with:
```html
    <script src="keyboard.js"></script>
    <script src="js/sleepyTime.js"></script>
```

> **Note the path:** `keyboard.js` is at the wwwroot root; `sleepyTime.js` is in `wwwroot/js/`, so the src is `js/sleepyTime.js`.

- [ ] **Step 3: Build to confirm**

```
dotnet build HomeSpeaker.WebAssembly/HomeSpeaker.WebAssembly.csproj
```

Expected: `Build succeeded.  0 Error(s)`

---

## Task 3: Add overlay HTML, CSS, and C# logic to `MainLayout.razor`

**Files:**
- Modify: `HomeSpeaker.WebAssembly/Components/Layout/MainLayout.razor`

This task modifies three sections of the file: the HTML template (add overlay div), the `<style>` block (add overlay CSS), and the `@code` block (add state + timers + JS calls + dispose). Do all three edits, then build once at the end.

---

### 3a — Add overlay `<div>` to the HTML template

- [ ] **Step 1: Add the overlay div**

Find the closing `</div>` of the mobile menu `@if` block (around line 60–61):
```razor
    }
</div>
```

Replace with:
```razor
    }

    @if (_overlayVisible)
    {
        <div class="sleepy-overlay" @onclick="OnOverlayClicked"></div>
    }
</div>
```

> The overlay is the last child of `.page-container` so it stacks above all other content (z-index in CSS handles the rest). `@onclick` calls a synchronous wrapper so Blazor doesn't need an `async` event handler here.

---

### 3b — Add overlay CSS to the `<style>` block

- [ ] **Step 2: Add the CSS rule**

Find the very end of the `<style>` block, just before `</style>`:
```css
    }
</style>
```

Replace with:
```css
    }

    /* Sleepy-time overlay */
    .sleepy-overlay {
        position: fixed;
        inset: 0;
        background: rgba(0, 0, 0, 0.97);
        z-index: 9999;
        cursor: pointer;
    }
</style>
```

---

### 3c — Add C# state, timers, and `[JSInvokable]` method

- [ ] **Step 3: Add private fields for overlay state**

Find the existing private field near the bottom of the `@code` block:
```csharp
    private bool showMobileMenu = false;
```

Replace with:
```csharp
    private bool showMobileMenu = false;

    // Sleepy-time overlay
    private bool _overlayVisible = false;
    private bool _watching = false;
    private Timer? _windowTimer;
    private Timer? _idleTimer;
```

---

- [ ] **Step 4: Add `IsInSleepWindow` helper and `OnOverlayClicked`**

Find the existing `ToggleMobileMenu` method:
```csharp
    private void ToggleMobileMenu()
    {
        showMobileMenu = !showMobileMenu;
    }
```

Replace with:
```csharp
    private void ToggleMobileMenu()
    {
        showMobileMenu = !showMobileMenu;
    }

    private static bool IsInSleepWindow()
    {
        var now = DateTime.Now.TimeOfDay;
        var start = new TimeSpan(22, 0, 0);
        var end   = new TimeSpan(6, 30, 0);
        // Window wraps midnight: active if >= 22:00 OR < 06:30
        return now >= start || now < end;
    }

    private void OnOverlayClicked()
    {
        _ = OnUserActivity();
    }
```

---

- [ ] **Step 5: Add `[JSInvokable] OnUserActivity` and `StartIdleTimer` helper**

Find the existing `[JSInvokable]` block — specifically just before the `OnVolumeDown` method:

```csharp
    [JSInvokable]
    public async Task OnVolumeDown()
```

Insert the following **before** that method (i.e., add these two new members between `OnVolumeUp` and `OnVolumeDown`):

```csharp
    [JSInvokable]
    public async Task OnUserActivity()
    {
        _overlayVisible = false;

        // Restart the idle timer: cancel existing, schedule new one-shot
        _idleTimer?.Change(Timeout.Infinite, Timeout.Infinite);
        _idleTimer?.Change(TimeSpan.FromMinutes(1), Timeout.InfiniteTimeSpan);

        await InvokeAsync(StateHasChanged);
    }

    private void StartIdleTimer()
    {
        if (_idleTimer == null)
        {
            _idleTimer = new Timer(async _ =>
            {
                await InvokeAsync(() =>
                {
                    _overlayVisible = true;
                    StateHasChanged();
                });
            }, null, TimeSpan.FromMinutes(1), Timeout.InfiniteTimeSpan);
        }
        else
        {
            // Reset to fire again in 1 minute
            _idleTimer.Change(TimeSpan.FromMinutes(1), Timeout.InfiniteTimeSpan);
        }
    }
```

---

- [ ] **Step 6: Start the window timer in `OnAfterRenderAsync`**

Find the existing `OnAfterRenderAsync` method and its closing brace:
```csharp
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error initializing keyboard shortcuts");
            }
        }
    }
```

Replace with:
```csharp
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error initializing keyboard shortcuts");
            }

            // Start the sleepy-time window checker (fires immediately, then every 60s)
            _windowTimer = new Timer(async _ =>
            {
                if (IsInSleepWindow())
                {
                    if (!_watching)
                    {
                        _watching = true;
                        await JSRuntime.InvokeVoidAsync("sleepyTime.startWatching", dotNetHelper);
                        StartIdleTimer();
                    }
                }
                else
                {
                    if (_watching)
                    {
                        _watching = false;
                        _idleTimer?.Change(Timeout.Infinite, Timeout.Infinite);
                        await JSRuntime.InvokeVoidAsync("sleepyTime.stopWatching");
                        await InvokeAsync(() =>
                        {
                            _overlayVisible = false;
                            StateHasChanged();
                        });
                    }
                }
            }, null, TimeSpan.Zero, TimeSpan.FromMinutes(1));
        }
    }
```

---

- [ ] **Step 7: Update `Dispose()` to clean up timers**

Find the existing `Dispose` method:
```csharp
    public void Dispose()
    {
        dotNetHelper?.Dispose();
    }
```

Replace with:
```csharp
    public void Dispose()
    {
        _windowTimer?.Dispose();
        _idleTimer?.Dispose();
        if (_watching)
        {
            _ = JSRuntime.InvokeVoidAsync("sleepyTime.stopWatching");
        }
        dotNetHelper?.Dispose();
    }
```

---

- [ ] **Step 8: Build to confirm all changes compile**

```
dotnet build HomeSpeaker.WebAssembly/HomeSpeaker.WebAssembly.csproj
```

Expected: `Build succeeded.  0 Error(s)`

---

## Self-Review Checklist (for implementer)

After the build passes, verify each spec requirement is met:

| Requirement | Where |
|---|---|
| Active window 22:00–06:30 | `IsInSleepWindow()` in Task 3 |
| Full-screen near-black overlay | `.sleepy-overlay` CSS in Task 3b |
| Touch/click dismisses overlay | `@onclick="OnOverlayClicked"` → `OnUserActivity()` |
| 1-minute idle timer resets on any interaction | `document` `pointerdown`/`keydown` → `sleepyTime` → `OnUserActivity()` |
| Overlay reappears after 1 minute idle | `StartIdleTimer` + idle timer callback sets `_overlayVisible = true` |
| Deactivates at 06:30 | Window timer's `else` branch calls `stopWatching`, hides overlay |
| Dispose cleans up | `Dispose()` cancels both timers, calls `stopWatching` |
