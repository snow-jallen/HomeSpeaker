---
name: "ai-library-enrichment"
description: "How to add resumable, server-side AI enrichment for a local media library without introducing an unnecessary separate service or vector database."
domain: "architecture"
confidence: "high"
source: "manual"
---

## Context
Use this when a .NET server already owns the media catalog, playback state, and SQLite database, and the goal is to enrich tracks with AI-generated metadata, genre membership, and similarity for playlisting. It applies when clients should stay thin and the system needs restart-safe background processing.

## Patterns
- Keep AI orchestration in the main ASP.NET Core server if that server already owns the source catalog and playback decisions.
- Use `Microsoft.Extensions.AI` with provider-specific registration so the app codes against `IChatClient`, not an OpenAI-specific type.
- Bind AI configuration from `IConfiguration` under a dedicated section like `AI`, but keep secrets out of `appsettings.json`.
- When supporting both public OpenAI and Azure OpenAI, keep a stable `AI` root contract and add a sibling `AI:AzureOpenAI` section with `Endpoint`, `ApiKey`, and `DeploymentName`; prefer Azure only when that section is fully configured.
- Persist song-linked AI data by a durable key such as `SongPath`, not an ephemeral in-memory ID.
- Split persistence into: durable per-track profile, many-to-many genre scores, similarity edges, playback feedback, and a resumable work queue.
- Use a claim/lease work-item table for resumable background analysis. Reset expired leases on startup and only process missing or changed fingerprints.
- Batch multiple tracks into one structured AI request, then compute similarity locally from stored marker vectors instead of asking the model to compare tracks pairwise.
- Keep AI playlists separate from user-authored playlist tables; expose them through a dedicated API surface or view model.
- Extend existing player status with nullable AI session context so clients can light up feedback controls without adding an entirely separate live-status protocol.

## Examples
- `HomeSpeaker.Server2\Mp3Library.cs` owns the current library scan, so it is the natural source for AI work-item discovery.
- `HomeSpeaker.Server2\Data\MusicContext.cs` already persists playlists, impressions, and radio streams; AI enrichment tables belong beside them.
- `HomeSpeaker.Server2\Program.cs` already wires hosted services, EF Core, and configuration, so AI registration should live there too.
- `HomeSpeakerMobile\Shared\APIClient.swift` shows a thin REST client pattern; add `/api/ai/*` there instead of pushing AI into the device app.

## Anti-Patterns
- Persisting AI metadata by `SongId` when the ID is reassigned during library reloads.
- Writing AI-generated genre playlists into the same tables as hand-curated playlists.
- Creating a separate AI microservice before the main server proves insufficient.
- Adding a vector database just because the .NET AI stack can support one.
- Making one model call per song-to-song comparison for similarity.
