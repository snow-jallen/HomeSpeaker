# Offline Media Manifest Pattern

Use this when adding mobile offline playback to an existing media server without introducing a full sync platform.

## Rule
Persist **selection rules** on the server, but keep **device download state** on the device.

Persist server-side:
- artist offline targets
- album offline targets
- song offline targets

Do not persist server-side:
- per-device download progress
- per-device local file presence
- device-specific cleanup state

Expand the saved rules into concrete songs from the current catalog whenever the client requests its offline manifest.

## Durable key
Use a durable content key such as **library file path** or another stable content path.

Do **not** key offline rules or downloadable media by transient runtime IDs if those IDs can change between scans or app launches.

## Minimal API shape
1. `GET manifest` → saved targets plus resolved songs to download
2. `POST target` → mark artist/album/song for offline use
3. `DELETE target` → remove a saved offline rule
4. `GET media` → stream validated playable bytes for one resolved song

## Server responsibilities
- validate requests against the live catalog
- dedupe overlapping rules
- return download metadata such as file name, size, ETag, and last-modified
- allow range requests for resumable downloads
- reject arbitrary file reads by serving only catalog-backed media paths
- resolve any catalog-relative media path to an absolute filesystem path before returning `Results.File` / physical-file responses, or downloads can fail even when the manifest resolves correctly

## Client responsibilities
- diff manifest songs against local files
- track download progress/failures locally
- delete orphaned local files only when no remaining manifest rule still covers them
- prefer local file playback when available
- migrate any legacy on-device rule cache into server targets on first sync, then delete the stale local rule data
- if legacy local downloads or queues still use old song IDs, fetch the current catalog during background/app-start sync so file migration does not depend on a later library-screen visit

## Why this works
It stays small, fits existing catalog models, avoids per-device server bloat, and still gives the app everything it needs to manage offline media cleanly.
