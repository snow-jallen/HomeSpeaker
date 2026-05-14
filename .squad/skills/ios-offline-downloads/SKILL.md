# iOS Offline Downloads

## Use When
- A SwiftUI client needs lightweight offline media without adding new server APIs.
- The app already has a stable authenticated or local media URL per track.

## Pattern
1. Persist offline selections separately from downloaded-file records.
2. Reuse the existing per-track media endpoint for downloads.
3. Store files under Application Support in a deterministic per-server folder.
4. Key durable offline state by a stable content identifier like `Song.path`, not transient runtime IDs.
5. If an older client stored transient IDs, migrate them to durable paths after the next library refresh before reconciling downloads.
6. Surface state in-library (artist, album, track) and add a dedicated management screen for saved and pending items.
7. Let the local player prefer the downloaded file before falling back to the network URL.
8. Treat legacy `songId` migration as best-effort only; once a bad transient key has already been persisted, QA should not promise perfect recovery if scan order changed before migration runs.
9. Keep failed downloads out of pending totals; summary math should report `.failed` separately so retryable errors are visible instead of being mislabeled as in-progress.

## HomeSpeaker Example
- Selection and queue logic: `HomeSpeakerMobile/iOS/OfflineDownloadsStore.swift`
- Library affordances: `HomeSpeakerMobile/iOS/Views/MusicLibraryView.swift`
- Management UI: `HomeSpeakerMobile/iOS/Views/OfflineDownloadsView.swift`
- Playback fallback: `HomeSpeakerMobile/iOS/LocalPlayer.swift`
