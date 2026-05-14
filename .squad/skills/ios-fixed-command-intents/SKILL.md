# iOS Fixed Command Intents

## Use When
- Siri/App Intents expose a small, explicit command set.
- A friendly spoken phrase must trigger a specific backend action reliably.

## Pattern
1. Map each spoken command directly to a stable backend identifier or operation.
2. Do **not** depend on fuzzy alias matching for commands that already have a decided target.
3. Keep widget intents and app intents on the same behavior contract.
4. Preserve exact behavior rules in code, not approximate ones. If the rule says “halve volume, but keep non-zero at least 1,” implement exactly that.

## HomeSpeaker Example
- `play fun music` should call AI genre `family-singalong`, not search for whatever happens to resemble “fun”.
- `play hymns` should call AI genre `hymns`.
- `quiet down` should share the same non-zero clamp logic in `HomeSpeakerMobile\iOS\Intents\HomeSpeakerIntents.swift` and `HomeSpeakerMobile\watchOS\Widget\WidgetIntents.swift`.
