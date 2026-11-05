# Anchors, rebuilt — design

Date: 2026-11-04. Status: built, phases 1–5 (Jonathan asked to proceed once no
questions remained; decisions made after the builder review are marked
"my call" inline for him to check).

## Intent

Anchors are the few things that hold down and define each family member's day
(up by 6, scripture, a walk, lights out by 10:30). The point is twofold: each
person reports on their own anchors quickly, wherever they are, and the whole
house can *see* how everyone's day is going — on the kitchen kiosk, the web
page, the iPhone app and the watch.

The current implementation (free-typed user ids, a global definition pool, a
midnight worker that copies rows into `DailyAnchors`, a 14-day checkbox table)
does not deliver that. This design replaces it.

Some family members also keep morning/evening **habit stacks** in Metanoeo, a
separate private app. HomeSpeaker must work fully on its own, and may
optionally pull in habits from Metanoeo (or any other app that speaks the same
small feed format) and check them off there.

## Goals

1. Each person can see and mark complete their own anchors on the kiosk, the
   web site, the iOS app and the watchOS app.
2. Each person can edit their own anchors (add, rename, reorder, schedule,
   retire) on the web site and in the iOS app.
3. The house can see everyone's day at a glance (Home card, family board) and
   history (week grid, streaks).
4. Several HomeSpeakers in one house share one set of anchors: a device's
   settings choose its anchor source — *this device* or *another HomeSpeaker*
   (e.g. upstairs uses kitchen).
5. Optional external feed: the anchor home can pull anchors from an external
   app (first one: Metanoeo) and write completions back to it.
6. Metanoeo users choose, per habit, which habits are shared as anchors.

## Non-goals (this round)

- **Authentication / per-person permissions.** Decided: not now. Anyone who can
  reach the HomeSpeaker can view, check off and edit anyone's anchors, the same
  trust model as playing music. Design so a person identity can later be
  enforced (all writes name the person/anchor; nothing assumes the caller *is*
  the person), but build no PINs or tokens for people.
- Metanoeo morning *commitments* (daily free text). Only habits are shared.
- Widgets, Live Activities, Siri intents, push reminders, spoken reminders.
- Editing a feed anchor's definition from HomeSpeaker (rename/reorder/retire of
  Metanoeo habits stays in Metanoeo).
- Multiple external feeds. One feed in v1; config shape should allow a list
  later.
- Server-side mDNS discovery of other HomeSpeakers for the source picker (type
  the URL).

## Anti-goals (would be failure even if it "works")

- Checking off takes more than one tap after choosing who you are.
- A missing server or unreachable feed makes the board error out or lose a tick.
- Days with no data look like failures (a day before an anchor existed, or a
  non-scheduled weekday, must never show as missed). Today is never "missed".
- Metanoeo habits appear on the house display without that user choosing it,
  per habit.
- HomeSpeaker and Metanoeo share code, a NuGet package or a database. The only
  coupling is the documented HTTP feed contract below.

## Constraints

- HomeSpeaker conventions (see `CLAUDE.md`): .NET 10, EF Core SQLite with
  migrations, `TimeProvider` for time, warnings as errors in Debug, analyzers
  (no underscore fields, one type per file), `HomeSpeakerService`-style facades
  for UI, minimal APIs, app tests via `TestServerFactory`.
- The whole feature stays behind `Features:Anchors=true` (`FeatureFlags`),
  surfaced to iOS via `GET /api/features` (`anchorsEnabled`, already decoded).
- "Today" on a HomeSpeaker is the anchor home's local date
  (`timeProvider.LocalTimeZone`; containers get `TZ`). Clients that ask for a
  specific day pass `date=yyyy-MM-dd`.
- No existing client calls the current `/api/anchors/*` routes (iOS only reads
  `anchorsEnabled`), so those routes can be replaced, not versioned.
- Metanoeo's own rules (`~/code/metanoeo/CLAUDE.md`) apply to its half:
  additive-only wire, nullable-with-default request fields, idempotent upserts
  with `clientUpdatedAt`, client owns "today", contract fixtures.
- The iOS/watch code cannot be compiled on the Linux dev box; it is
  compile-checked by `.github/workflows/ios-build.yml` on a non-master branch.

## Concepts

- **Person** — a family member. Has a display name, a colour, optional emoji,
  sort order, active flag. Replaces free-typed `UserId`.
- **Anchor** — belongs to one person. Title, slot (`morning|day|evening`),
  sort order within the slot, optional target time (display only), and one or
  more **active periods**. Anchors are retired, never deleted, so history keeps
  its meaning.
- **Active period** — a date range `[from, to)` (`to` null = still active) plus
  the days of week it is scheduled (default every day). **Edits never rewrite
  the past** (decided by me after review): changing the days closes the current
  period at today and opens a new one from today with the new days (if the
  current period started today, it is edited in place); retiring closes the
  current period at today; restoring opens a new period from today. Gaps
  between periods are `none`, not misses. The anchor's identity (and streak)
  carries across periods.
- **Log** — one per anchor per date: done/not done, when it was done, and the
  client timestamp of the change (last write wins).
- **Expected** — computed, never stored: an anchor is expected on date *d* if
  one of its periods covers *d* and *d*'s weekday is in that period's days. No
  midnight worker; `DailyAnchorWorker` is deleted.
- **Cell state** for (anchor, date): `kept` (logged done), `missed` (expected,
  past, not done), `open` (expected, today, not done yet), `off` (in a period
  but not scheduled that weekday), `none` (outside every period), `unknown`
  (expected, past, no log, and the anchor says a missing log means unknown —
  only feed anchors such as Metanoeo sleep habits; see the feed contract). A
  log for a day the anchor was not expected still shows `kept` (bonus, not
  penalised).
- **Streak** (per anchor): consecutive expected days kept, counting back from
  today; today counts if kept and does not break the streak if still open;
  `off`, `none` and `unknown` days are skipped. **Anchored day** (per person): a
  past day where every expected anchor was kept or unknown and at least one was
  kept. Person streak = consecutive anchored days, same today rule.
- **Anchor home** — the HomeSpeaker that stores the anchors. Every HomeSpeaker
  is its own home unless its settings point at another one.
- **Feed** — an external source of people + anchors + logs over HTTP (the
  contract below). Configured on the anchor home only.

## Data model (HomeSpeaker)

New tables (names are the builder's call within conventions):

- `AnchorPeople`: Id, Name, Color (one of a fixed palette of ~8 keys), Emoji
  (nullable), SortOrder, IsActive, CreatedAt, FeedPersonId (nullable string —
  links this local person to a feed person, see *Linking*).
- `Anchors`: Id, PersonId (FK), Title (≤ 120 chars), Slot, SortOrder,
  TargetTime (TimeOnly?), CreatedAt.
- `AnchorPeriods`: Id, AnchorId (FK), From (DateOnly), To (DateOnly?,
  exclusive), Days (int bitmask, Sunday=1 … Saturday=64; 127 = every day).
  At most one open (`To` null) period per anchor; periods never overlap.
- `AnchorLogs`: Id, AnchorId (FK), Date, Done, DoneAt (DateTimeOffset?),
  ClientUpdatedAt (DateTimeOffset?), UpdatedAt. Unique (AnchorId, Date).
- `AnchorFeedOutbox`: Id, FeedAnchorId (string), Date, Done, ClientUpdatedAt,
  Attempts, LastError, NextAttemptAt, CreatedAt. Unique (FeedAnchorId, Date) —
  a newer change for the same cell replaces the queued one.

**Migration of existing data** (my call): one EF migration that
- creates one person per distinct `UserAnchors.UserId`/`DailyAnchors.UserId`
  (name = the old id, colours assigned in order);
- creates one anchor per (user, definition) pair seen in `UserAnchors` or
  `DailyAnchors`; title from the definition (or the latest snapshot name);
  slot `day`;
- builds its periods from the old daily rows: each maximal run of consecutive
  dates that have a `DailyAnchors` row becomes one every-day period, so days the
  old worker never wrote (server down, anchor unassigned) become `none`, not
  misses. If the pair is still assigned and its definition active, the last
  period is left open (`To` null) — and if that last run ended before today,
  a new open period starts today, so the gap stays `none`. A pair assigned but
  with no daily rows gets one open period from the assignment date;
- copies completed `DailyAnchors` rows into `AnchorLogs` (done, `DoneAt` from
  `CompletedAt`);
- drops `AnchorDefinitions`, `UserAnchors`, `DailyAnchors`.
Data migration can be SQL in the migration or a one-time startup step; builder's
call, but it must be idempotent and covered by a test.

**Delete**: `DailyAnchorWorker`, `AnchorHub` (+ `/anchorHub` mapping and the
`BlazorNoiseSamplerTests` case that uses it — retarget that test at another hub
or drop the case), `AnchorNotificationService`/`IAnchorNotificationService`,
old DTOs in `HomeSpeaker.Shared/AnchorDefinition.cs`, `Pages/Admin/Anchors*.razor`.

## Server structure

- **`AnchorDay` / `AnchorStreaks`** — pure static domain logic (expected, cell
  state, streaks, anchored days) with unit tests. No EF, no HTTP.
- **`IAnchorStore`** — everything the UI and API need:
  - `GetBoardAsync(from, to)` → people, their anchors (with source + editable
    flag), logs in range, today's date, and feed health (ok / stale + age /
    error).
  - people: list, create, update, set order, archive.
  - anchors: create, update (title, slot, target time, days — days via the
    period rule above), set order within person, retire, restore.
  - `SetDoneAsync(anchorId, date, done, clientUpdatedAt)`.
  - `Changed` event (in-process) for Blazor to re-render.
- **`LocalAnchorStore`** — EF-backed; merges in the feed (below). Used when this
  device is the anchor home.
- **`RemoteAnchorStore`** — HTTP client to another HomeSpeaker's anchor API
  (same operations, same DTOs). Used when this device's source is a remote
  HomeSpeaker. Caches the board ~15 s; polls every 30 s while a page is
  watching so the kiosk notices ticks made elsewhere; on failure shows the last
  board marked stale and surfaces write errors to the UI (no outbox for
  HomeSpeaker→HomeSpeaker writes — they are interactive; show an inline error
  and leave the checkbox unchanged).
- The store is chosen at runtime from settings (so changing the source in
  settings takes effect without a restart — builder may use a selector like
  `PhotoSourceSelector`).
- **Anchor ids on the wire are opaque strings.** Local anchors use their int id
  as a string; feed anchors use `feed:<feedAnchorId>`. People likewise
  (`feed:<feedPersonId>` for unlinked feed people). Routes accept these strings.

### Settings

Added to `SettingKeys` (editable on `/settings`, admin-gated like the rest):

- `Anchors:Source` — `local` (default) or `remote`.
- `Anchors:RemoteUrl` — base URL of the anchor-home HomeSpeaker
  (e.g. `https://kitchen.<tailnet>.ts.net`).
- `Anchors:Feed:Name` — label shown on feed anchors (e.g. "Metanoeo").
- `Anchors:Feed:Url` — feed base URL.
- `Anchors:Feed:Token` — bearer token (secret, write-only like other secrets).

The settings UI shows a "Test connection" result for the remote URL and the
feed. When `Anchors:Source=remote`, the feed fields are hidden/ignored (feeds
are configured on the anchor home only, and remote devices see them through the
home). A household setting for a single device is valid: `local` with no feed.

## HomeSpeaker anchor API

Mapped by `MapAnchorApi()` when anchors are enabled. JSON camelCase, dates
`yyyy-MM-dd`, instants ISO-8601 UTC. No auth (see non-goals). These routes are
used by the iOS app, the watch app, and remote HomeSpeakers; Blazor pages call
`IAnchorStore` directly.

- `GET /api/anchors/board?from=&to=` → `{ today, people: [ { id, name, color,
  emoji, source, anchors: [ { id, title, slot, order, targetTime, days
  (current), retired, periods: [ { from, to, days } ], source, editable,
  writable } ], logs: [ { anchorId, date, done,
  doneAt, actualTime } ], streak } ], feed: { name, status, asOf, error } }`.
  Defaults: from = to = today. Max range 92 days. Anchor cell state and streaks
  are included pre-computed (`cells` per anchor or per log — builder's call) so
  Swift clients don't reimplement the rules.
- `GET /api/anchors/people/{personId}/day?date=` → that person's anchors for
  the day, grouped by slot, with state — the watch's one call.
- `GET/POST /api/anchors/people`, `PUT /api/anchors/people/{id}`,
  `PUT /api/anchors/people/order`, `DELETE /api/anchors/people/{id}` (archive).
- `POST /api/anchors/people/{personId}/anchors`, `PUT /api/anchors/anchors/{id}`,
  `PUT /api/anchors/people/{personId}/anchors/order`,
  `POST /api/anchors/anchors/{id}/retire`, `POST /api/anchors/anchors/{id}/restore`.
  Edits to non-editable (feed) anchors return 409.
- `PUT /api/anchors/anchors/{id}/days/{date}` body `{ done, clientUpdatedAt? }`
  → upsert, last write wins by `clientUpdatedAt` (missing = server time).
  Idempotent. Feed anchors are routed through the feed (below); a feed anchor
  with `writable: false` returns 409. Dates more than 1 day ahead of today are
  rejected (400).

## External feed contract (what Metanoeo implements)

Documented in `docs/anchor-feed.md` so any app could implement it.

```
GET  {url}/anchors?from=yyyy-MM-dd&to=yyyy-MM-dd
Authorization: Bearer {token}
200 {
  "people":  [ { "id": "…", "name": "Dad" } ],
  "anchors": [ { "id": "…", "personId": "…", "title": "Up by 6:00",
                 "slot": "morning|day|evening", "order": 0,
                 "targetTime": "06:00" | null,
                 "activeFrom": "2026-09-01", "activeUntil": null,
                 "missingIs": "missed" | "unknown",
                 "writable": true } ],
  "logs":    [ { "anchorId": "…", "date": "2026-11-04", "done": true,
                 "doneAt": "…" | null, "actualTime": "06:12" | null } ]
}

PUT  {url}/anchors/{anchorId}/days/{date}
Authorization: Bearer {token}
{ "done": true, "clientUpdatedAt": "2026-11-04T13:02:11Z" }
204 (applied or stale-ignored) | 404 (unknown/unshared anchor) | 400 (bad date)
```

- Anchors include any shared anchor active at some point in [from, to];
  every-day schedule is implied (the feed has no `days`). HomeSpeaker maps
  `activeFrom`/`activeUntil` (exclusive, null = open) to one period.
- `missingIs` (default `missed` when absent) says what a past day with no log
  means; `unknown` renders neutrally and never breaks a streak.
- `writable: false` anchors are shown but cannot be ticked from HomeSpeaker.
- Unknown JSON fields are ignored on both sides; the contract grows additively.

### How HomeSpeaker uses the feed (on the anchor home)

- **Read**: `HttpAnchorFeed` fetches the requested range, caches each range
  ~60 s, and merges people/anchors/logs into the board. If the fetch fails, the
  last good copy is used and the board reports `feed.status = stale` with its
  age; with no good copy, feed people/anchors are simply absent and the board
  shows a small "Metanoeo unreachable" note. The local board never fails
  because of the feed.
- **Write-back**: a tick on a feed anchor is applied optimistically to the
  cached feed data and written to `AnchorFeedOutbox`; a background worker sends
  it immediately and retries with backoff (e.g. 10 s → 5 min cap) while the
  feed is unreachable. 2xx removes the row; 4xx removes it, logs a warning and
  reverts the optimistic value on next refresh; 5xx/network retries. Pending
  outbox entries keep overriding the fetched value for that cell, and the UI
  shows them as *pending* (e.g. a small clock on the check). The feed resolves
  conflicts with `clientUpdatedAt`; HomeSpeaker always sends the time the user
  tapped.
- **Linking**: on the people page, a local person can be linked to a feed
  person (`FeedPersonId`). Linked: the feed person's anchors appear in the local
  person's day, marked with the feed name, non-editable. Unlinked feed people
  appear as their own (read/tick-only) people with source = feed. A feed person
  can be linked to at most one local person.

## Metanoeo side (separate repo, implemented after HomeSpeaker's feed client)

- `HabitItem` gains `SharedOn` (DateOnly?, null = not shared; set to the
  user's local date when sharing is turned on, cleared when turned off) and
  `RetiredOn` (DateOnly?, set when a habit is retired, cleared if
  reactivated). Migration; existing retired habits get null `RetiredOn` and are
  excluded from the feed.
- **Sharing publishes from the day it is turned on** (my call): the feed
  anchor's `activeFrom` is `max(CreatedOn, SharedOn)`, so turning sharing on
  never exposes older history (including misses). Turning it off removes the
  habit from the house display entirely; turning it on again starts from the
  new date.
- Wire, additive: `HabitItemDto` gains `shareAsAnchor` (bool, = `SharedOn !=
  null`); `HabitItemEditDto`
  gains `shareAsAnchor: bool? = null` where **null means unchanged** (old app
  builds send no key and must not unshare). Contract fixtures updated.
- iOS stack editor: per-habit toggle "Show on the house display". Default off.
- **Household token**: admin portal issues (shown once) and revokes tokens,
  stored hashed like device tokens; a separate `Household` auth scheme/policy;
  routes under `/api/household` (not `MapApi`, whose `Device` policy would
  reject it), with the same date-window sanity check.
- `GET /api/household/anchors?from&to` (range ≤ 92 days): people = active users
  with ≥ 1 shared habit (id = user id, name = DisplayName); anchors = shared
  habits (slot morning/evening, title, order); logs = `HabitLogs` in range for
  those habits on or after `activeFrom`.
  - `check` habits: `writable: true`, `missingIs: missed`.
  - `sleep` habits (my call): `writable: false`, `missingIs: unknown`,
    `targetTime` set, `actualTime` on each log, `done` exactly as Metanoeo has
    it. HomeSpeaker shows the actual time next to the target (e.g. "1:04 ·
    target 10:30") and does not judge lateness itself. Read-only because a
    house tick would otherwise overwrite the Health-filled time and stop
    Health from filling that day again.
- `PUT /api/household/anchors/{habitId}/days/{date}`: 404 unless the habit is
  shared and `date >= activeFrom`; 409 for sleep habits; upsert `HabitLog`
  through `RetryOnConflictAsync` + `LastWrite`, source `manual` (no new enum
  value — older apps would fail to decode it), leaving `ActualTime` untouched.
  The log is flagged as written by the household (new non-wire column, e.g.
  `ViaHousehold`), and `FamilyEndpoints.LastActivityAsync` ignores flagged logs
  so a sibling's tick on the kiosk can't hide someone's inactivity from the
  family-encouragement prompt.
- Tests in Metanoeo's suite for: sharing defaults off, null-means-unchanged,
  history starts at `SharedOn`, unshared habit 404, sleep habit 409, stale
  replay ignored, household ticks excluded from last activity, token revocation.

## UI (web + kiosk, Blazor, glass style)

- **Home card** (`Pages/Index.razor`): a row per person — colour dot/emoji,
  name, one small dot per anchor expected today (filled = kept, hollow = open,
  pending style for queued feed writes). Tap a person → their day. Shown when
  anchors are enabled and at least one person exists.
- **Family board** `/anchors`: people as sections; for each, today's anchors as
  large tappable chips, then a 7-day grid (14/30 selectable) of cell states and
  streaks; "anchored days" summary. Feed status note when stale/unreachable.
  Link to manage people.
- **My day** `/anchors/{personId}`: the person's anchors for today grouped
  Morning / Day / Evening, big touch targets (kiosk at 1024×600 and phone
  width), one tap toggles, brief undo. Arrows/selector to view yesterday (late
  ticks allowed for past days within the range shown). "Edit" link.
- **Edit anchors** `/anchors/{personId}/edit`: list by slot, add, rename,
  slot, days of week, optional target time, reorder (up/down buttons are fine),
  retire/restore; retired list collapsed. Feed anchors shown read-only with
  "Edit in {feed name}".
- **People** `/anchors/people`: add, rename, colour, emoji, reorder, archive,
  link to feed person.
- **Who am I on the web**: the board lets you pick yourself; the choice is
  remembered per browser (cookie or localStorage) and the nav "Anchors" link
  then opens your day. On the kiosk (`Kiosk` mode) nothing is remembered — the
  board is the entry point and the idle screen returns there as today.
- None of the anchor pages are admin-gated. `/settings` stays admin-gated.
- Pages re-render on `IAnchorStore.Changed` (local) or the remote poll.

## iOS and watchOS

- Shared (`HomeSpeakerMobile/Shared`): Codable models for the board/day DTOs,
  `APIClient` calls for the routes above. The chosen person is stored per saved
  server in the `group.com.homespeaker` App Group so the watch (and later
  widgets) can read it; the watch gets it the same way it gets the server list
  today (builder: follow `SharedConnections`).
- iOS: an Anchors screen reachable from the main navigation when
  `anchorsEnabled`: first visit asks "Who are you?" (people list, changeable
  later); shows My day (tap to toggle, optimistic, error toast on failure); a
  Family view (today per person); Edit my anchors (same capabilities as the
  web editor; feed anchors read-only). Talks to whichever server is selected —
  that server answers for its anchor home (local or proxied), so the app needs
  no source logic.
- watchOS: one list of the chosen person's anchors for today grouped by slot;
  tap to toggle; pull to refresh. No editing.
- Old builds that ignore anchors keep working (no existing wire changes).

## Testing

- Unit: `AnchorDay` / streak rules (period boundaries, gaps, weekday masks,
  days edits not rewriting the past, today-open, bonus logs, `unknown`),
  migration mapping (runs of daily rows → periods), outbox replace/backoff logic,
  feed merge + linking, stale handling.
- App tests via `TestServerFactory`: board/day/people/anchors/log endpoints,
  feature flag off → routes absent, 409 on editing feed anchors, idempotent log
  PUT with stale `clientUpdatedAt`, remote store against a second test server
  (or a fake handler), feed client against a fake `HttpMessageHandler`
  implementing the contract.
- UI verified by running the server (run-homespeaker-server skill) and
  screenshots at kiosk (1024×600) and phone widths.
- iOS/watch: compile check via the iOS workflow; manual on device.

## Phases (implementation order)

1. Domain logic, new tables + data migration, `LocalAnchorStore`, new API,
   removal of the old pieces, web/kiosk pages and Home card.
2. Anchor source setting + `RemoteAnchorStore`.
3. Feed client, outbox worker, linking, feed settings, `docs/anchor-feed.md`.
4. iOS app + watch app.
5. Metanoeo: sharing flag, household token, feed routes, iOS toggle.

Each phase is shippable on its own.

## Left to the builder

Exact table/class names within conventions; colour palette; whether cell
states are precomputed per log or per date; page layout details within the
glass style; poll intervals within the ranges above; how the Settings UI groups
the new fields; how the iOS screens slot into the existing navigation.
