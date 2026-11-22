# Anchors on the home screen and a column board

Date: 2026-11-21. Status: approved. Prototypes: https://claude.ai/artifact/GsAxyK3SUTJGbN12QDPEPN (options A and D).

## Why

On the Pi touchscreens (1024×600) the anchors get a thin strip on the home screen: a badge, one dot per anchor and a count. You can't read the anchors or check one off without leaving the home screen. The `/anchors` page stacks one card per person, so at 1024×600 you scroll past the first person or two.

## Goals

- The home screen shows each person's anchors for today by name, and a tap checks one off.
- `/anchors` shows everyone side by side on one screen, with a short history.

## Non-goals

- No change to how anchors are stored, synced or computed (`AnchorRules`, stores, feed).
- No change to the home screen on speakers without anchors.

## Approaches considered

1. **Change the existing pages, A only when anchors are on (chosen).** The other speakers keep today's home screen, and the anchor components are reused.
2. A separate kiosk-only home page. Rejected because it means keeping two home layouts in step.
3. Layout A for every speaker. Rejected because a speaker without anchors would have an empty right column.

## Home screen (A)

Applies when `Dashboard.AnchorsEnabled` and at least one person exists. Otherwise the current layout is unchanged.

- **Left column (420px):** Now Playing, then Climate and Glucose as two small cards side by side.
  - If only one of them is on, it takes the full width.
  - If neither is on, Next hours takes the spot.
- **Right column:** a slim Playlists/Radio/Library row (icon and label inline), then the Anchors card filling the rest.
  - The card header reads "Anchors · today" and has an "Open board ›" link to `/anchors`.
  - Each person gets a row with their badge (which links to `/anchors/{id}`), a chip per anchor, and a done/total count. The count turns the person's colour with a ✓ when they're all done.
  - The chips are today's anchors whose cell is open or kept (or pending), in `AnchorView.Ordered` order.
- **Tapping a chip** toggles it for `AnchorClock.Today` through `IAnchorStore.SetDoneAsync`, the same as the board does.
  - A non-writable anchor (from an outside feed) is shown disabled.
  - While a toggle is in flight the chip is busy.
  - A failure shows briefly in the card.
- **Too many people or anchors:** the chips wrap, and past that the card scrolls inside itself.
- **Next hours** is not on the home screen while anchors are shown. It's still on `/climate`.
- **The small Climate card** shows the outside temperature, the window advice, H/L, conditions, and the indoor rooms. The rooms wrap and are clipped to what fits.
- **The small Glucose card** shows the value, arrow, delta, age and the 3-hour trend line.
- **Phones (<700px):** the layout stacks as today.
- **The short-screen rule** that hides `.hs-anchors` when a Home row exists is removed. Only `NullHomeControl` exists, so the rule does nothing useful.

## `/anchors` board (D)

- **Header:** the title, ‹ day › navigation ("Today · Sat Nov 21" style label), "My day" (when a "me" is remembered) and "People".
  - › is disabled on today.
  - Moving the day reloads the board ending on that day.
- **One column per person,** side by side.
  - Columns have a minimum width of about 210px, and the row scrolls sideways when there are more than fit.
  - Columns stack under 700px.
- **Each column:**
  - A done/total ring in the person's colour, the name (links to `/anchors/{id}`) and the streak text.
  - Big chips (`AnchorCheck`) for the anchors open or kept on the shown day. Toggling one records it for the shown day.
  - A "Last 2 weeks" grid with one row per anchor active in the range. It has 14 cells, oldest to newest, ending on the shown day, and that day's cell is outlined. Cell states are kept, missed, off/none, and pending.
- **Kept from today's page:** "This is me" (not on the kiosk, local people only), the feed and stale notices, the error line, the loading and empty states, and "Add X's anchors" for a person with no anchors.
- **Removed:** the 7/14/30 range chips and `AnchorWeekGrid`.

## Left to the builder

- Exact sizes and spacing, matched to the prototype and the glass styles.
- How the 14-day cells are computed. This should be a small pure helper with unit tests.
