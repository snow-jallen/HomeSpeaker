# Anchor feed contract

HomeSpeaker can show anchors that live in another app (for example a habit tracker) beside its own, and tick them off
there. The other app implements two HTTP routes; HomeSpeaker is configured with their base URL and a token
(`Anchors:Feed:Name`, `Anchors:Feed:Url`, `Anchors:Feed:Token`, also on the Settings page under Anchors).

Nothing else couples the two apps. Both sides ignore JSON fields they don't know, and the contract only grows by
adding optional fields.

Conventions: JSON with camelCase names; dates `yyyy-MM-dd`; times `HH:mm`; instants ISO-8601. Every request carries
`Authorization: Bearer {token}`. "Today" is the calling HomeSpeaker's local date; it never asks for more than 92 days
or for dates more than a day ahead.

## Read: `GET {url}/anchors?from={date}&to={date}`

```json
{
  "people": [ { "id": "u1", "name": "Dad" } ],
  "anchors": [
    {
      "id": "h1",
      "personId": "u1",
      "title": "Up by 6:00",
      "slot": "morning",
      "order": 0,
      "targetTime": "06:00",
      "activeFrom": "2026-09-01",
      "activeUntil": null,
      "missingIs": "missed",
      "writable": true
    }
  ],
  "logs": [
    { "anchorId": "h1", "date": "2026-11-04", "done": true, "doneAt": "2026-11-04T12:58:00Z", "actualTime": "06:12" }
  ]
}
```

| Field | Meaning |
|---|---|
| `people` | People with at least one shared anchor. Ids are opaque strings. |
| `anchors[].slot` | `morning`, `day` or `evening`; anything else shows as `day`. |
| `anchors[].order` | Order within the person's slot. |
| `anchors[].targetTime` | Optional; shown as "by 6:00 AM". |
| `anchors[].activeFrom` / `activeUntil` | Required `activeFrom` (anchors without one are ignored). The anchor counts every day in `[activeFrom, activeUntil)`; `activeUntil` null = still active. Include any anchor active at some point in `[from, to]`. Days outside the range show as blank, never as missed. |
| `anchors[].missingIs` | What a past day with no log means: `missed` (default) or `unknown` (no data, e.g. no watch overnight). Unknown days are neutral and don't break streaks. |
| `anchors[].writable` | Whether HomeSpeaker may tick it (default `true`). `false` shows it read-only. |
| `logs` | Entries in `[from, to]`. `doneAt` and `actualTime` are optional; `actualTime` is shown beside `targetTime` (e.g. "11:04 PM · target 10:30 PM"). |

Any non-2xx answer, or no answer within 10 seconds, counts as "unreachable": HomeSpeaker keeps showing the last good
copy (marked stale on the board) and never fails its own board because of the feed.

## Write: `PUT {url}/anchors/{anchorId}/days/{date}`

```json
{ "done": true, "clientUpdatedAt": "2026-11-04T13:02:11+00:00" }
```

`clientUpdatedAt` is when the person tapped. The feed should apply the change only if it is newer than the last change
it has for that anchor and day (last write wins), and treat repeats as harmless: HomeSpeaker retries.

| Answer | HomeSpeaker does |
|---|---|
| 2xx | Done; the tick stops showing as pending. |
| 404, 409, 400 and other 4xx | Drops the tick (unknown or unshared anchor, read-only, date out of range); the board shows what the feed says. |
| 401, 403, 408, 429, 5xx, no answer | Keeps it queued and retries: after 10 s, doubling, at most every 5 minutes. |

While a tick is queued the board shows it as pending (a dashed check).

## Metanoeo

Metanoeo implements this contract at `https://<metanoeo host>/api/household`. Its admin portal issues the token
(**House displays**), and each person chooses which habits to share in the app's stack editor. Check habits are
writable; sleep anchors are read-only with `missingIs: unknown` and show the actual time beside the target. Setup:
Metanoeo's README, "House display".

## In HomeSpeaker

On the People page, a person can be linked to one feed person; that person's feed anchors then show in their day,
marked with the feed's name. Feed people nobody is linked to appear as people of their own. Feed anchors are edited
only in the app they come from.

Code: `Services/Anchors/HttpAnchorFeed.cs` (client), `FeedAnchorMerge.cs` (merge), `AnchorFeedOutbox.cs` and
`AnchorFeedSender.cs` (write-back), tests in `HomeSpeaker.Server2.Tests/Anchors/`.
