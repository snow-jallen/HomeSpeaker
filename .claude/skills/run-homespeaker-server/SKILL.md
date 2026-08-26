---
name: run-homespeaker-server
description: Run, build, launch, smoke-test, or screenshot the HomeSpeaker server (HomeSpeaker.Server2) locally. Use when asked to run the server, start the app, verify a change against the running server, hit the REST API, or capture the web UI.
---

# Run HomeSpeaker.Server2 locally

HomeSpeaker.Server2 is an ASP.NET Core (net10.0) Blazor Server app + REST API
that normally runs on a Raspberry Pi in Docker. It runs fine on macOS for
development: the web UI and every REST/database feature work; only actual
audio playback fails (it shells out to `sox`/`cvlc`, which aren't installed —
see Gotchas). All paths below are relative to the repo root.

## Prerequisites

- .NET SDK 10.0.1xx (`global.json` pins 10.0.103, rollForward latestFeature;
  verified with 10.0.107 from Homebrew).
- `python3`, `jq`, `curl` — all present on stock macOS.
- No media files or config needed: `appsettings.json` defaults point at the
  repo-relative `HomeSpeaker.Server2/HomeSpeakerMedia/` folder and
  `HomeSpeaker.db` (both gitignored). The smoke script synthesizes a test
  MP3 itself — no ffmpeg required.

## Run (agent path) — smoke script

```bash
bash .claude/skills/run-homespeaker-server/smoke.sh
```

Starts the server on port 5280, runs 9 REST assertions (health, library
scan picks up the generated MP3, player status, queue, playlist
add/list/delete, radio stream create/delete), then shuts the server down.
Exit 0 = all passed. Server log: `$TMPDIR/homespeaker-smoke/server.log`.

Flags:

- `--keep` — leave the server running after the checks so you can iterate
  against `http://localhost:5280` (kill later with
  `pkill -f 'bin/Debug/net.*HomeSpeaker.Server2'`).
- `--screenshot` — also capture the web UI to
  `$TMPDIR/homespeaker-smoke/ui.png` using headless Edge (or Chrome).

Useful endpoints once running (all verified):

```bash
curl -s http://localhost:5280/health
curl -s http://localhost:5280/api/homespeaker/songs
curl -s http://localhost:5280/api/homespeaker/player/status
curl -s http://localhost:5280/api/homespeaker/playlists
curl -s http://localhost:5280/api/homespeaker/radio
```

Full API list: `HomeSpeaker.Server2/Docs/REST-API.md`.

## Build only

```bash
dotnet build HomeSpeaker.Server2/HomeSpeaker.Server2.csproj
```

Debug builds have `TreatWarningsAsErrors=true` — new warnings fail the build.

## Run (human path)

```bash
dotnet run --project HomeSpeaker.Server2 --launch-profile http
```

Binds `http://0.0.0.0:5280` (from `Properties/launchSettings.json`), applies
EF migrations to the SQLite db on startup, scans the media folder. Open
http://localhost:5280 in a browser. Ctrl-C to stop.

## Screenshot the UI headlessly

```bash
"/Applications/Microsoft Edge.app/Contents/MacOS/Microsoft Edge" \
  --headless --disable-gpu --window-size=1280,900 \
  --virtual-time-budget=8000 --screenshot=/tmp/hs-ui.png http://localhost:5280/
```

## Gotchas

- **Playback endpoints 500 on macOS.** `POST .../songs/{id}/play` returns
  `Failed to play song: An error occurred trying to start process 'play'` —
  the Linux player shells out to sox (`play`) and VLC (`cvlc`), which only
  exist on the Pi. Everything else (library, queue, playlists, radio CRUD,
  volume reads) works; volume reads fall back to 50 because `amixer` is
  also missing. Don't "fix" this locally — test playback on the Pi.
- **`pkill -f HomeSpeaker.Server2.dll` does NOT kill the server.** On macOS
  `dotnet run` launches an apphost binary whose command line is
  `.../bin/Debug/net10.0/HomeSpeaker.Server2` (no `.dll`). Use
  `pkill -f 'bin/Debug/net.*HomeSpeaker.Server2'`.
- **Headless screenshots: use `--virtual-time-budget`, not `--timeout`.**
  A wall-clock `--timeout` waits long enough for the Blazor SignalR circuit
  to reconnect and captures a blank main pane mid-re-render. Even with
  virtual time, the main pane content varies (SSR frame vs hydrated frame);
  the sidebar/nav rendering proves the UI serves. Assert functionality via
  the REST checks, not pixels.
- **The library only scans `*.mp3`** (`IFileSource.cs`). `.m4a`/`.flac`
  files in the media folder are invisible. The smoke script synthesizes a
  valid MP3 with pure Python (silent MPEG-1 Layer III frames + ID3v1 tag)
  because ffmpeg isn't installed.
- **OTEL/Seq exporter errors in the log are noise** — appsettings points at
  `localhost:4317`; no collector runs locally. The app is unaffected.
- **AI features are dormant locally** (no OpenAI/Azure key configured) —
  the app logs "AI analysis is disabled" and uses a NullChatClient. AI
  endpoints still respond.

## Troubleshooting

- `Port 5280 is already in use` (from smoke.sh) — a previous server is
  still up: `pkill -f 'bin/Debug/net.*HomeSpeaker.Server2'`, wait 2s, rerun.
- Health check hangs >60s on first run — NuGet restore on a cold cache;
  check `$TMPDIR/homespeaker-smoke/server.log` for restore progress.
