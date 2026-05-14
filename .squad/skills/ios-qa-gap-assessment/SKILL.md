---
name: "ios-qa-gap-assessment"
description: "How to QA iOS feature work in this repo when Swift automation is unavailable but the .NET backend and contracts can still be validated."
domain: "qa"
confidence: "high"
source: "manual"
---

## Context
Use this when HomeSpeaker mobile changes land in Swift/App Intents, but the working environment cannot run Xcode-based automated tests. It applies when QA still needs to separate what can be proven automatically from what must be held for focused manual validation.

## Patterns
- Start by proving the shared/server baseline still builds with `dotnet build HomeSpeaker.sln`.
- Run `dotnet test HomeSpeaker.sln` to confirm whether real automated coverage exists; in this repo it currently does not.
- Inspect the Swift client contract files directly (`Shared\Models.swift`, `Shared\APIClient.swift`, feature views, and intent files) to verify whether the requested behavior is actually wired end-to-end.
- Treat Siri/App Intents as high-risk unless there are explicit intents and phrases for the user’s requested commands; generic free-form entity intents are not enough for recognition-sensitive scenarios.
- For offline/mobile features, verify three layers separately: persisted model/state, transfer/download orchestration, and management UI.
- In HomeSpeaker specifically, reject any "durable" offline implementation that keys local files or saved selections by `songId`; server `SongId` values are scan-order IDs and can change, so QA should expect `Song.Path` (or another stable path key) for offline durability.
- When automation is not practical, document a concrete manual matrix that covers lifecycle edges: app relaunch, no network, duplicate actions, partial state, and delete/retry flows.

## Examples
- `HomeSpeakerMobile\iOS\Intents\HomeSpeakerShortcuts.swift` and `HomeSpeakerMobile\iOS\Intents\HomeSpeakerIntents.swift` are the first place to confirm whether Siri commands are explicit or still generic.
- `HomeSpeakerMobile\Shared\APIClient.swift` and `HomeSpeakerMobile\Shared\Models.swift` show whether a mobile workflow has real API/state support or just UI placeholders.
- `HomeSpeakerMobile\iOS\Views\MusicLibraryView.swift` and `HomeSpeakerMobile\iOS\Views\NowPlayingView.swift` show whether there is a usable surface for download management and playback fallbacks.

## Anti-Patterns
- Marking an iOS feature done because the .NET solution builds.
- Assuming Siri phrases are reliable without dedicated command intents and phrase-to-parser consistency.
- Calling an offline workflow "implemented" when it only streams from `/api/music/{songId}` and does not persist files on device.
