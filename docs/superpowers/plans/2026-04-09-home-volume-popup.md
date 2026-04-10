# Home Volume Popup Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the static volume badge on the home screen "now playing" card with a tappable icon that opens a vertical volume slider popup; tapping outside dismisses it.

**Architecture:** All changes are in a single file (`Index.razor`). The volume badge `<span>` becomes a `<button>` that toggles a Blazor `@if` block containing a dismiss overlay + popup card with a vertical `<input type="range">`. The `overflow: hidden` on `.now-playing-card` would clip the popup, so it is removed from the card and instead applied only to the decorative pseudo-element container via a wrapper div.

**Tech Stack:** Blazor WebAssembly, CSS, gRPC via `HomeSpeakerService.SetVolumeAsync(int)`

---

### Task 1: Add volume state and wire up RefreshStatusAsync

**Files:**
- Modify: `HomeSpeaker.WebAssembly/Pages/Index.razor` (@code section)

- [ ] **Step 1: Add state fields**

In the `@code` block, add these two fields after `private Timer? refreshTimer;`:

```csharp
private bool showVolumePopup;
private int volumeLevel;
```

- [ ] **Step 2: Sync volumeLevel from status in RefreshStatusAsync**

In `RefreshStatusAsync`, after `PlayerState.UpdateStatus(status);` add:

```csharp
volumeLevel = status?.Volume ?? volumeLevel;
```

So the method body becomes:

```csharp
private async Task RefreshStatusAsync()
{
    try
    {
        var status = await svc.GetStatusAsync();
        bool repeatMode = await svc.GetRepeatModeAsync();
        PlayerState.UpdateStatus(status);
        PlayerState.UpdateRepeatMode(repeatMode);
        volumeLevel = status?.Volume ?? volumeLevel;
        StateHasChanged();
    }
    catch (Exception ex)
    {
        logger.LogWarning(ex, "Failed to refresh player status");
    }
}
```

- [ ] **Step 3: Add helper methods**

Add these three methods to the `@code` block (after `OnStateChanged`):

```csharp
private void ToggleVolumePopup() => showVolumePopup = !showVolumePopup;
private void CloseVolumePopup() => showVolumePopup = false;
private async Task OnVolumeChanged(ChangeEventArgs e)
{
    if (int.TryParse(e.Value?.ToString(), out var v))
    {
        volumeLevel = v;
        await svc.SetVolumeAsync(volumeLevel);
    }
}
```

- [ ] **Step 4: Verify it builds**

```bash
cd HomeSpeaker.WebAssembly
dotnet build
```
Expected: no errors.

- [ ] **Step 5: Commit**

```bash
git add HomeSpeaker.WebAssembly/Pages/Index.razor
git commit -m "feat: add volume popup state and refresh sync"
```

---

### Task 2: Replace volume badge with popup trigger + popup markup

**Files:**
- Modify: `HomeSpeaker.WebAssembly/Pages/Index.razor` (template section)

- [ ] **Step 1: Replace the volume badge span with a button + popup**

Find this block (lines ~33–38):

```razor
<div class="progress-row mt-2">
    <div class="progress-bar-track">
        <div class="progress-bar-fill" style="width: @(PlayerState.Status.PercentComplete.ToString("F1", System.Globalization.CultureInfo.InvariantCulture))%"></div>
    </div>
    <span class="volume-badge"><i class="fas fa-volume-up me-1"></i>@PlayerState.Status.Volume</span>
</div>
```

Replace with:

```razor
<div class="progress-row mt-2">
    <div class="progress-bar-track">
        <div class="progress-bar-fill" style="width: @(PlayerState.Status.PercentComplete.ToString("F1", System.Globalization.CultureInfo.InvariantCulture))%"></div>
    </div>
    <div class="volume-popup-anchor">
        <button class="volume-icon-btn" @onclick="ToggleVolumePopup" title="Adjust volume">
            <i class="fas fa-volume-up"></i>
        </button>
        @if (showVolumePopup)
        {
            <div class="volume-dismiss-overlay" @onclick="CloseVolumePopup"></div>
            <div class="volume-popup-card">
                <span class="volume-popup-label">Vol</span>
                <span class="volume-popup-number">@volumeLevel</span>
                <input type="range"
                       class="volume-slider-vertical"
                       min="0" max="100"
                       value="@volumeLevel"
                       @oninput="@(e => { if (int.TryParse(e.Value?.ToString(), out var v)) volumeLevel = v; })"
                       @onchange="OnVolumeChanged" />
            </div>
        }
    </div>
</div>
```

- [ ] **Step 2: Verify it builds**

```bash
dotnet build HomeSpeaker.WebAssembly
```
Expected: no errors.

- [ ] **Step 3: Commit**

```bash
git add HomeSpeaker.WebAssembly/Pages/Index.razor
git commit -m "feat: add volume popup trigger and markup"
```

---

### Task 3: Add CSS for popup

**Files:**
- Modify: `HomeSpeaker.WebAssembly/Pages/Index.razor` (`<style>` section)

- [ ] **Step 1: Fix overflow clipping on the card**

The `.now-playing-card` rule currently has `overflow: hidden`. Change it to `overflow: visible` so the popup isn't clipped. The decorative `::before`/`::after` glows are radial gradients that visually fade out and won't cause visible bleed:

Find:
```css
.now-playing-card {
    background: linear-gradient(160deg, #1C1C2E 0%, #13131F 60%, #0E0E1B 100%);
    border: 1px solid rgba(201, 152, 90, 0.2);
    border-radius: 20px;
    padding: var(--hs-space-md) var(--hs-space-md) var(--hs-space-sm);
    box-shadow:
        0 12px 48px rgba(0, 0, 0, 0.7),
        0 0 80px rgba(201, 152, 90, 0.06),
        inset 0 1px 0 rgba(255, 255, 255, 0.06);
    position: relative;
    overflow: hidden;
}
```

Change `overflow: hidden;` to `overflow: visible;`.

- [ ] **Step 2: Remove the old .volume-badge rule and add new popup CSS**

Find the existing `.volume-badge` rule:
```css
.volume-badge {
    font-family: 'DM Mono', monospace;
    font-size: 0.62rem;
    color: var(--hs-text-muted);
    white-space: nowrap;
    letter-spacing: 0.03em;
}
```

Replace it with:

```css
.volume-popup-anchor {
    position: relative;
    flex-shrink: 0;
}

.volume-icon-btn {
    background: none;
    border: none;
    padding: 0 2px;
    cursor: pointer;
    color: var(--hs-text-muted);
    font-size: 0.75rem;
    line-height: 1;
    transition: color 0.15s ease;
}

.volume-icon-btn:hover,
.volume-icon-btn:focus {
    color: var(--hs-primary);
    outline: none;
}

.volume-dismiss-overlay {
    position: fixed;
    inset: 0;
    z-index: 99;
    background: transparent;
}

.volume-popup-card {
    position: absolute;
    bottom: calc(100% + 8px);
    right: 0;
    z-index: 100;
    background: linear-gradient(180deg, #252535 0%, #1A1A28 100%);
    border: 1px solid rgba(201, 152, 90, 0.3);
    border-radius: 12px;
    padding: 12px 14px;
    display: flex;
    flex-direction: column;
    align-items: center;
    gap: 6px;
    box-shadow: 0 12px 32px rgba(0, 0, 0, 0.75);
    min-width: 54px;
}

.volume-popup-label {
    font-family: 'Syne', sans-serif;
    font-size: 0.6rem;
    text-transform: uppercase;
    letter-spacing: 0.1em;
    color: var(--hs-primary);
}

.volume-popup-number {
    font-family: 'DM Mono', monospace;
    font-size: 0.75rem;
    font-weight: 700;
    color: var(--hs-text-primary);
    min-width: 24px;
    text-align: center;
}

.volume-slider-vertical {
    -webkit-appearance: slider-vertical;
    writing-mode: vertical-lr;
    direction: rtl;
    width: 6px;
    height: 80px;
    cursor: pointer;
    accent-color: var(--hs-primary);
}
```

- [ ] **Step 3: Verify it builds and visually check**

```bash
dotnet build HomeSpeaker.WebAssembly
```
Expected: no errors. Run the app and verify:
- 🔊 icon appears in place of the old volume badge
- Tapping it shows the popup with "Vol", a number, and a vertical slider
- Dragging the slider updates the number live
- Tapping outside closes the popup
- Releasing the slider sends the volume to the server (check browser network tab or server logs)

- [ ] **Step 4: Commit**

```bash
git add HomeSpeaker.WebAssembly/Pages/Index.razor
git commit -m "feat: volume popup styles — vertical slider on home now-playing card"
```
