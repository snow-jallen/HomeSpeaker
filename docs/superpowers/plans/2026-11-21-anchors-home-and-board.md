# Anchors on the Home Screen and a Column Board: Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** When anchors are on, the Pi home screen gives anchors the right column, with tappable chips that show each anchor's name. The `/anchors` page becomes one column per person, with day navigation and a two-week grid.

**Architecture:** This is Blazor markup and CSS only, with one new toggle method on `DashboardState` and a few pure helpers on `AnchorView`. The home screen's Climate, Glucose and Next-hours cards move into their own components so the two home layouts can share them. Nothing changes in the stores, rules or API.

**Tech Stack:** .NET 10, Blazor Server, xUnit, the glass CSS in `wwwroot/css/glass.css` and `wwwroot/css/anchors.css`.

**Spec:** `docs/superpowers/specs/2026-11-21-anchors-home-and-board-design.md`. Prototype (options A and D): https://claude.ai/artifact/GsAxyK3SUTJGbN12QDPEPN

## Global Constraints

- Debug builds treat warnings as errors: `dotnet build HomeSpeaker.sln` must be clean.
- One top-level type per file (MA0048). Private fields are camelCase with no underscore. Match the surrounding file.
- Sizes in the glass CSS are px. Person colour comes from `var(--hs-person)` via `data-color`.
- The home screen is unchanged on speakers where `Dashboard.AnchorsEnabled` is false or nobody has been added.
- The target screen is 1024×600. Under 700px wide everything stacks.
- **No git commands.** The owner commits. Skip the plan's commit points and leave changes in the working tree.

## Review Focus

1. **A person with many anchors (8+) or a household of 6+ on the home screen:** the chips wrap, the anchors card scrolls inside itself, and nothing runs off the screen or pushes the page to scroll. (Task 3 verification)
2. **Anchors from an outside feed (`Writable=false`):** the chip shows disabled on the home screen and does nothing when tapped. (Task 3 verification)
3. **A past day on the board:** an anchor that was missed shows as an unchecked chip and can be checked off, and › is disabled on today. (Task 1 `CheckableOn` test, Task 4 verification)
4. **A person with nothing scheduled today (all off) on the home screen:** their row says "Nothing today" instead of being an empty row. (Task 3)
5. **5+ people on the board at 1024px:** the columns keep about 210px each and scroll sideways inside the panel, and the page body never scrolls sideways. (Task 4 verification)

---

### Task 1: Pure helpers for chips, progress and the two-week grid

**Files:**
- Modify: `HomeSpeaker.Server2/Services/Anchors/AnchorView.cs`
- Create: `HomeSpeaker.Server2/Services/Anchors/AnchorDayProgress.cs`
- Test: `HomeSpeaker.Server2.Tests/Anchors/AnchorViewTests.cs`

**Interfaces:**
- Produces:
  - `public sealed record AnchorDayProgress(int Done, int Total)`
  - `AnchorView.CheckableOn(IEnumerable<AnchorDto> anchors, DateOnly date) -> IReadOnlyList<AnchorDto>`: anchors in `Ordered` order whose cell on `date` is `open`, `kept` or `missed`, or is `Pending`.
  - `AnchorView.Progress(AnchorPersonDto person, DateOnly date) -> AnchorDayProgress`: Total is the cells on `date` that are `open|kept|missed`; Done is the `kept` ones.
  - `AnchorView.History(AnchorDto anchor, DateOnly to, int days) -> IReadOnlyList<AnchorCellDto>`: exactly `days` cells, oldest first, ending on `to`. A date with no cell becomes `new AnchorCellDto(date, "none")`.
  - `AnchorView.HistoryRows(AnchorPersonDto person) -> IReadOnlyList<AnchorDto>`: `Ordered` anchors with any cell whose state isn't `none`. This is the rule moved from `AnchorWeekGrid.rows`.

- [ ] **Step 1: Write the failing tests** in `AnchorViewTests` (reuse its `anchor(...)`/`person(...)` helpers, adding a multi-cell variant where needed):
  - `CheckableOn_IncludesMissedForPastDays_InSlotOrder`: states evening/open, day/off, morning/missed, morning/none, day/kept → ids `["morning-missed", "day-kept", "evening-open"]`.
  - `CheckableOn_IncludesPendingEvenWhenOff`: an `off` cell with `Pending: true` is included.
  - `Progress_CountsExpectedAndKept`: kept, missed, open, off, none → `new AnchorDayProgress(1, 3)`.
  - `History_FillsMissingDatesWithNone_OldestFirst`: an anchor with cells only on `today` (kept) and `today-2` (missed), `History(a, today, 4)` → dates `today-3..today`, states `["none", "missed", "none", "kept"]`.
  - `HistoryRows_SkipsAnchorsWithOnlyNoneCells`.

- [ ] **Step 2: Run them and confirm they fail.** Run `dotnet test HomeSpeaker.Server2.Tests --filter AnchorViewTests`. Expected: build errors, because the members don't exist yet.

- [ ] **Step 3: Implement the four members and the record.** Add XML summaries in the file's style.

- [ ] **Step 4: Run the tests and confirm they pass.** Run `dotnet test HomeSpeaker.Server2.Tests --filter AnchorViewTests`. Expected: all pass.

### Task 2: Move the home cards into components (no visible change)

**Files:**
- Create: `HomeSpeaker.Server2/Components/Glass/HomeClimateCard.razor`, `HomeGlucoseCard.razor`, `HomeHoursCard.razor`
- Modify: `HomeSpeaker.Server2/Pages/Index.razor`

**Interfaces:**
- Produces:
  - `<HomeClimateCard Compact="bool" />`
  - `<HomeGlucoseCard Compact="bool" />`
  - `<HomeHoursCard />`
  - Each injects `DashboardState` and `TimeProvider` and re-renders from its parent.
  - `Compact` adds the class `is-compact` to the card's root element, so Task 3's CSS can style it.

- [ ] **Step 1: Move the markup and its helpers.**
  - Move the Climate `<a class="glass hs-info-card hs-climate">` block, with `indoorRooms`, `highLow`, `conditions` and `outlook`, into `HomeClimateCard`.
  - Move the Glucose block, with `toneClass`, `glucoseCard` and `pct`, into `HomeGlucoseCard`.
  - Move the Next-hours block, with `hourly`, into `HomeHoursCard`.
  - `Index.razor` renders the components where the blocks were. The rendered HTML must stay the same, so the existing CSS keeps applying (`.hs-source-tiles + .hs-home-lower ...` selectors included).
  - Re-rendering: `Index` already calls `StateHasChanged` on `Dashboard.Changed`. Child components with no parameters don't re-render with their parent, so either pass a `Version` int parameter that `Index` bumps in `onChanged`, or have each card subscribe to `Dashboard.Changed` itself and unsubscribe in `Dispose`. Subscribing is preferred; it matches `Index`.

- [ ] **Step 2: Build.** Run `dotnet build HomeSpeaker.sln`. Expected: 0 warnings, 0 errors.

- [ ] **Step 3: Check that the home screen looks the same.** Use the `run-homespeaker-server` skill and take a 1024×600 screenshot of `/` before and after this task (stash the screenshot path, not the code). They should match.

### Task 3: Home layout A with tappable anchor chips

**Files:**
- Create: `HomeSpeaker.Server2/Components/Glass/HomeAnchorsCard.razor`
- Modify: `HomeSpeaker.Server2/Services/Dashboard/DashboardState.cs`, `HomeSpeaker.Server2/Pages/Index.razor`, `HomeSpeaker.Server2/wwwroot/css/glass.css`, `HomeSpeaker.Server2/wwwroot/css/anchors.css`

**Interfaces:**
- Consumes: `AnchorView.CheckableOn`, `AnchorView.Progress` (Task 1); `HomeClimateCard`, `HomeGlucoseCard`, `HomeHoursCard` (Task 2); the existing `AnchorCheck` and `PersonBadge`.
- Produces:
  - `DashboardState.AnchorsToday : DateOnly`, set from `board.Today` in `refreshAnchorsAsync`.
  - `DashboardState.SetAnchorDoneAsync(AnchorDto anchor, bool done) -> Task<AnchorResult>`. It opens its own scope, as `refreshAnchorsAsync` does, calls `IAnchorStore.SetDoneAsync(anchor.Id, AnchorsToday, new AnchorLogRequest(done, DateTimeOffset.UtcNow))`, then runs `refreshAnchorsAsync()` and `raiseChanged("anchor toggled")`. On `AnchorSourceUnavailableException`, `HttpRequestException` or `TaskCanceledException` it logs a warning and returns `AnchorResult.Unavailable`.

- [ ] **Step 1: Add `AnchorsToday` and `SetAnchorDoneAsync` to `DashboardState`,** then build.

- [ ] **Step 2: Build `HomeAnchorsCard`.**
  - Root: `<section class="glass hs-info-card hs-anchors-today">`.
  - Header: "Anchors · today" label, and an "Open board ›" link to `anchors`.
  - A scrolling list with one `.hs-anchor-line` per person (`data-color`, plus `is-done` when `Progress.Done >= Total > 0`). Each line has:
    - `PersonBadge`, wrapped in a link to `anchors/{id}`.
    - `.hs-anchor-chips`, holding an `AnchorCheck` for each anchor from `CheckableOn(person.Anchors, Dashboard.AnchorsToday)`, with `Busy` taken from a `HashSet<string>`. `OnToggle` calls `SetAnchorDoneAsync(anchor, cell.State != "kept")`.
    - A `Done/Total` count. A person with no checkable anchors shows `<span class="hs-anchor-none">Nothing today</span>`.
  - A non-Ok result sets an error line in the card ("Couldn't update "{title}".") until the next successful toggle.

- [ ] **Step 3: Change `Index.razor`.**
  - Add `hs-home--anchors` to `.hs-home` when `anchorsVisible`.
  - In that mode:
    - The left column is the now-playing card, then `<div class="hs-home-minis">` holding `HomeClimateCard Compact` and/or `HomeGlucoseCard Compact`. If neither is on, it holds `HomeHoursCard`.
    - The right column is `.hs-source-tiles`, then `HomeAnchorsCard`, then `HomeTileRow`.
  - Without anchors, the markup is the same as after Task 2. Delete the old dot strip (`.hs-anchors`, `.hs-anchors-list`, `.hs-anchor-person`, `.hs-anchor-dots`), its CSS in `glass.css` (including the `:only-child` block), and the `(max-height: 700px)` rule hiding `.hs-anchors`. Remove `AnchorView.TodayCells` and its test (no callers left). Keep `DotClass`, which Task 4 uses.

- [ ] **Step 4: Add the CSS.** Match prototype A.
  - `.hs-home--anchors .hs-home-minis`: a flex row with gap 12px, filling the rest of the left column (`flex: 1; min-height: 0`). Each card is `flex: 1`.
  - Compact cards: climate big number 46px; rooms wrap with `overflow: hidden`; the outlook is hidden; glucose value 46px with the trend line kept.
  - `.hs-home--anchors .hs-source-tiles`: tiles 56px tall, icon and label inline.
  - `.hs-anchors-today`: `flex: 1; min-height: 0`, with the list `overflow-y: auto`.
  - Each `.hs-anchor-line` is a row: a 70px name column, chips that wrap (`flex: 1; flex-wrap: wrap; gap: 6px`), and the count. `.is-done .hs-anchor-count` uses `var(--hs-person)` followed by a ✓.
  - Compact chip, scoped `.hs-anchors-today .hs-check`: `width: auto; min-height: 36px; padding: 4px 12px 4px 6px; border-radius: 999px; font-size: 14px; gap: 6px`. The mark is 22px with a 2px border. Hide `.hs-check-meta` here.
  - Under 700px, `.hs-home--anchors` stacks everything in one column. The minis stay a two-card row.

- [ ] **Step 5: Build and run the tests.** Run `dotnet build HomeSpeaker.sln && dotnet test HomeSpeaker.Server2.Tests`. Expected: clean build, and all tests pass.

- [ ] **Step 6: Check it on screen.**
  - Run the server with the `run-homespeaker-server` skill and `Features__Anchors=true`.
  - Seed 4 people with 3-4 anchors each through `/api/anchors/*` (read `Endpoints/AnchorEndpoints.cs` for the routes).
  - Screenshot `/` at 1024×600 and 400×800.
  - Tap a chip and confirm it turns kept and the count updates.
  - Then cover Review Focus 1 and 4: add a person with 9 anchors and one whose anchors are all off today, and confirm the card scrolls and the row reads "Nothing today".

### Task 4: Column board with day navigation and two-week grid

**Files:**
- Create: `HomeSpeaker.Server2/Components/Anchors/AnchorRing.razor`, `HomeSpeaker.Server2/Components/Anchors/AnchorHistoryGrid.razor`
- Modify: `HomeSpeaker.Server2/Pages/Anchors/AnchorBoard.razor`, `HomeSpeaker.Server2/wwwroot/css/anchors.css`
- Delete: `HomeSpeaker.Server2/Components/Anchors/AnchorWeekGrid.razor`, and the `.hs-grid*` and `.hs-cell*` CSS once nothing uses it.

**Interfaces:**
- Consumes: `AnchorView.CheckableOn`, `Progress`, `History`, `HistoryRows`, `DotClass` (Task 1).
- Produces:
  - `<AnchorRing Done="int" Total="int" Size="int" />`: an SVG ring stroked with `var(--hs-person)` over a 15% white track, with `Done/Total` in the centre. Size defaults to 54.
  - `<AnchorHistoryGrid Person To Days />`: one row per `HistoryRows(Person)`, with a truncated name (50px) and `Days` 8px cells from `History(anchor, To, Days)`, classed `hs-hcell` plus `DotClass`. The last cell is outlined. A "Last 2 weeks" label sits above.

- [ ] **Step 1: Build `AnchorRing` and `AnchorHistoryGrid`.**

- [ ] **Step 2: Rewrite the `AnchorBoard.razor` layout.**
  - State: replace `rangeDays` with `DateOnly? shownDay` (null means the store's today). `LoadAsync` calls `Store.GetBoardAsync(day.AddDays(-13), day)`, where `day = shownDay ?? AnchorClock.Today(Time)`.
  - Header:
    - The title.
    - `.hs-day-nav`: the ‹ and › buttons copied from `AnchorDay.razor`, with › disabled when `day >= board.Today`. The label is "Today · Sat Nov 21", or "Yesterday · …", or "Thu Nov 19", using `CultureInfo.InvariantCulture`.
    - "My day" and "People", as now.
  - Remove the range chips.
  - Body: `<div class="hs-board-cols">` with one `<section class="hs-board-col" data-color>` per person. Each column holds:
    - A head with `AnchorRing` (from `Progress(person, day)`), a `PersonBadge` linking to the person's day, and the streak text.
    - The "This is me" chip, under the same condition as now.
    - An `AnchorCheck` for each anchor from `CheckableOn(person.Anchors, day)`. `ToggleAsync` records the shown day, not `board.Today`.
    - "Add {Name}'s anchors" when the person has no unretired anchors.
    - `AnchorHistoryGrid To="day" Days="14"` at the bottom.
  - Keep the notices, the error line, and the loading and empty states as they are.

- [ ] **Step 3: Add the CSS.**
  - `.hs-board-cols`: `display: grid; grid-auto-flow: column; grid-auto-columns: minmax(210px, 1fr); gap: 12px; overflow-x: auto; flex: 1; min-height: 0`.
  - `.hs-board-col`: a flex column with gap 8px, padding 12px, radius 18px, background `rgba(255,255,255,.04)`, and `overflow-y: auto`.
  - Chips inside it use `min-height: 52px; font-size: 17px` and hide the meta line only when it's empty.
  - Under 700px: `grid-auto-flow: row; grid-auto-columns: auto`.
  - Delete the old `.hs-grid*`, `.hs-cell*` and `.hs-board-person` rules.

- [ ] **Step 4: Build and run the tests.** Run `dotnet build HomeSpeaker.sln && dotnet test HomeSpeaker.Server2.Tests`. Expected: clean, all pass.

- [ ] **Step 5: Check it on screen.**
  - Screenshot `/anchors` at 1024×600 and 400×800 using the Task 3 seed data.
  - Press ‹ once. Confirm a missed anchor shows unchecked, check it off, and confirm the grid's outlined cell turns kept.
  - Confirm › is disabled on today.
  - Add a fifth and sixth person and confirm the columns scroll sideways inside the panel, with no sideways scroll on the page (Review Focus 3 and 5).

### Task 5: Docs

**Files:**
- Modify: `CLAUDE.md` (the Anchors paragraph under "Auxiliary widgets", and the `MainLayout`/Home description if it mentions the anchor strip)

- [ ] **Step 1: Update the docs.** Note in one or two sentences that the home screen gives anchors the right column when they're on, with chips that check off today through `DashboardState.SetAnchorDoneAsync`, and that `/anchors` is a column board with day navigation and a 14-day grid.
