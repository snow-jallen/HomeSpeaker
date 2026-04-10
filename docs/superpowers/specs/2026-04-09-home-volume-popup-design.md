# Home Screen Volume Popup — Design Spec

**Date:** 2026-04-09  
**File:** `HomeSpeaker.WebAssembly/Pages/Index.razor`

## Summary

Replace the static volume badge on the home screen "now playing" card with a tappable icon that opens a vertical volume slider popup. Tapping anywhere outside dismisses it.

## Behavior

- The existing `🔊 60%` volume badge becomes a clickable button showing just the icon.
- Tapping it toggles a popup card anchored above the icon.
- The popup shows: "Vol" label, live numeric readout (updates while dragging), vertical range slider.
- Tapping anywhere outside the popup closes it.
- On slider release (`@onchange`), calls `svc.SetVolumeAsync(volumeLevel)`.
- `volumeLevel` is initialized from `PlayerState.Status.Volume` on each status refresh.

## Implementation (all changes in `Index.razor`)

### New state fields
```csharp
private bool showVolumePopup;
private int volumeLevel;
```

### Template changes
- `.now-playing-card` currently has `overflow: hidden` (needed for the decorative `::before`/`::after` glows). An `absolute`-positioned popup inside would be clipped. Fix: add `overflow: visible` to `.now-playing-card` and clip the decorative elements another way, OR render the popup with `position: fixed` near the icon.
- The `.progress-row` div gets `position: relative`.
- The `<span class="volume-badge">` becomes `<button @onclick="ToggleVolumePopup">` with just the icon.
- A `@if (showVolumePopup)` block renders:
  1. A full-screen transparent dismiss overlay (`position: fixed; inset: 0; z-index: 99`)
  2. The popup card (`position: absolute; bottom: 2rem; right: 0; z-index: 100`) with "Vol" label, `@volumeLevel` number, and `<input type="range">`
- Slider uses `@oninput` for live number update and `@onchange` to call `svc.SetVolumeAsync()`

### New code methods
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

### Update RefreshStatusAsync
```csharp
volumeLevel = status?.Volume ?? volumeLevel;
```

## Styling

Matches the app's dark amber theme:
- Popup: dark gradient background, amber border, rounded corners, `box-shadow`
- Slider: styled with `accent-color: var(--hs-primary)`
- Overlay: `position: fixed; inset: 0; z-index: 99; background: transparent`
- Popup: `z-index: 100`

## Out of Scope

- No changes to Queue page, NavMenu, or PlayControls
- No debounce (gRPC call on each slider release is sufficient)
