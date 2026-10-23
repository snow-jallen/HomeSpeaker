# HomeSpeaker

A self-hosted music player for a Raspberry Pi. Put one in the kitchen, one upstairs, and so on. Each Pi runs its own independent copy (there is no central server) and gives you:

- a web UI (phone, laptop or a touchscreen on the Pi) for your MP3s, playlists, internet radio and YouTube audio
- playback through the Pi's own audio output
- an AirPlay receiver, so the Pi shows up as a speaker in the iOS and macOS audio menus
- an ambient photo slideshow and clock for a wall-mounted touchscreen
- optional extras: blood sugar (Nightscout), room temperatures (Govee), a weather forecast, daily habit "anchors" and AI-made playlists
- an iPhone and Apple Watch app (`HomeSpeakerMobile/`) that can discover speakers on your network

HomeSpeaker is licensed under the MIT license (see `LICENSE`).

## What you need

- A Raspberry Pi 4 or 5 with 2 GB of RAM or more, running Raspberry Pi OS (Bookworm) or another Debian or Ubuntu with systemd
- Docker with the Compose plugin, version 2.24 or newer (`docker compose version`)
- Speakers: a USB DAC, or the 3.5 mm headphone jack. HomeSpeaker picks the output automatically: USB audio first, then the headphone jack, then built-in or HDMI. Override it with `ALSA_CARD` (uncomment it in the compose file).
- Optional: a 1024x600 touchscreen. It is detected as a kiosk automatically.

Only one HomeSpeaker can run per host (the containers have fixed names).

## Install

On the Pi:

```bash
curl -fsSL https://raw.githubusercontent.com/snow-jallen/homespeaker/master/install/install.sh | sudo bash
```

The installer asks whether to enable the AirPlay receiver and whether to set up a full-screen Chromium kiosk. It then:

- creates `/opt/homespeaker` with the compose file and a `.env`
- creates your `music` folder (in the home directory of the user who ran `sudo`)
- starts the containers and waits for the health check
- advertises the speaker on your network over mDNS (Avahi, `_homespeaker._tcp`) so the iOS app can find it
- installs a nightly update timer

Options go after `bash -s --`, variables between `sudo` and `bash`:

```bash
curl -fsSL https://raw.githubusercontent.com/snow-jallen/homespeaker/master/install/install.sh | sudo HTTP_PORT=8080 bash -s -- --yes --kiosk
```

`--yes` accepts the defaults, `--kiosk` sets up the kiosk, `--no-start` skips starting, `--dry-run` prints what would happen, `--help` lists them. `HOMESPEAKER_REPO` and `HOMESPEAKER_REF` pick another repo or branch, and `HTTP_PORT` changes the web port (default 80). Re-running the installer leaves an existing `.env` alone. The `.env` it writes also sets `TZ` (your time zone, taken from the Pi), `PUID`, `MUSIC_DIR` and `AIRPLAY_NAME`; edit it and run `docker compose up -d` to change them. To remove everything except your music, run `install/uninstall.sh` with `sudo`.

## First run

Open `http://<pi-name>.local/` (or the Pi's IP address). Until you set a location, the app offers a short setup at `/setup`: your location (for the forecast), photos, optional features and an optional PIN. You can skip it, and it goes away once a location is saved or `Setup__Completed=true` is set.

Everything else is configured on the `/settings` page, which saves to the database in your music folder. Settings can also come from files and environment variables. Later sources win:

1. `appsettings.json` (built in)
2. settings saved on the settings page (the `AppSettings` table in `HomeSpeaker.db`)
3. environment variables (including `/opt/homespeaker/homespeaker.env`)
4. command-line arguments

An environment variable always wins, even when it is empty, and the settings page shows such a setting as locked. In an env file, write each `:` in a key as `__` (`Immich:BaseUrl` becomes `Immich__BaseUrl`).

For environment variables, copy `homespeaker.env.example` to `/opt/homespeaker/homespeaker.env`, edit it, and run `cd /opt/homespeaker && sudo docker compose up -d`. The file is optional. To keep it somewhere else, set `HOMESPEAKER_ENV_FILE` in `.env`.

### Admin PIN

Set an optional PIN on the settings page (4 to 12 digits). With a PIN, `/settings` and the other admin pages need it. After 5 wrong tries in a minute from one address, that address is locked out for 60 seconds. That slows guessing but does not stop it: a 4-digit PIN can still be brute-forced over the network in a day or two, so use 6 or more digits if your network is not just family. Without a PIN, anyone on your network can change settings.

### Forgot the admin PIN

Put an empty `Admin__PinHash=` in `/opt/homespeaker/homespeaker.env` and run `cd /opt/homespeaker && sudo docker compose up -d`. The PIN is now off. The old PIN is still saved, and returns if you remove that line. To pick a new one, delete the saved PIN first (install `sqlite3` if needed), then remove the line and restart:

```bash
sudo sqlite3 ~/music/HomeSpeaker.db "DELETE FROM AppSettings WHERE Key='Admin:PinHash';"
```

Use the path of your music folder (`MUSIC_DIR` in `/opt/homespeaker/.env`). Then set a new PIN on the settings page.

## Music and photos

**Music.** Copy MP3s into the `music` folder (`MUSIC_DIR` in `/opt/homespeaker/.env`, `~/music` by default). The library rescans by itself when files change. To use a network share, mount it (for example with `/etc/fstab`) and point `MUSIC_DIR` at the mount. The folder also holds the database, the photo cache and `.keys` (which keeps sign-ins valid across restarts), so it must be writable and is worth backing up.

**Photos.** The ambient slideshow starts on a kiosk after `Ambient:IdleSeconds` without a touch. `Photos:Source` chooses where pictures come from:

- `folder`: `.jpg` and `.jpeg` files in `music/photos` (subfolders are fine). PNG and HEIC files are skipped.
- `immich`: one album from your Immich server (see below).

If `Photos:Source` is not set, Immich is used when it is configured, and the folder otherwise.

## Optional features

A feature is off, and its widgets hidden, until you configure it. Configure each on `/settings` or with the environment variable.

| Feature | Turn it on with |
| --- | --- |
| Blood sugar (Nightscout) | `NIGHTSCOUT_URL` |
| Room temperatures (Govee) | `Temperature:ApiBaseUrl` and `Temperature:ApiKey` |
| Weather forecast | `Forecast:Latitude` and `Forecast:Longitude` |
| Anchors (daily habits) | `Features:Anchors=true` |
| Immich slideshow | `Immich:BaseUrl`, `Immich:AlbumId` and `Immich:ApiKey` |
| AI playlists | `AI:OpenAI:ApiKey` (and optionally `AI:OpenAI:ChatModel`) |
| iOS push notifications | the four `PushNotifications:Apns:*` values, see below |

`GET /api/features` reports which features a server has on.

### Nightscout

Set `NIGHTSCOUT_URL` to your Nightscout site. The home page then shows your glucose.

### Govee temperature sensors

Get an API key in the Govee Home app (Profile, About, Apply for API Key). Set `Temperature:ApiBaseUrl` to `https://openapi.api.govee.com/router/api/v1/` (the settings page has a checkbox that fills this in) and `Temperature:ApiKey`. On the settings page, pick which sensor belongs to which room, or write `Temperature:Rooms` yourself as `Room: part of device name, another part; Next room: ...` (first match wins; a room with no patterns matches its own name).

- `Temperature:OutsideRoom` and `Temperature:IndoorRoom` name the rooms used for window advice (by default the outside room is the one called Outside).
- `Temperature:WindowAdvice` (default true) and `Temperature:WindowMinOutside` (default 50) control the "open the windows" hint.
- `TemperatureThreshold` (default 2) is the margin, in degrees F, within which two temperatures count as "about the same".
- `Units:Temperature` is `F` or `C`.

### Weather forecast

Set `Forecast:Latitude` and `Forecast:Longitude`. The forecast comes from Open-Meteo and needs no key.

### Anchors

Set `Features:Anchors` to `true` for a daily-habit checklist.

### Immich

Set `Immich:BaseUrl`, `Immich:AlbumId` and `Immich:ApiKey` (the key is also accepted as `IMMICH_API_KEY`). The key needs the `asset.read`, `asset.view`, `album.read` and `face.read` permissions (or "all"). Only a small rotating set of photos is kept on the Pi. Tuning options: `Immich:SyncIntervalMinutes` (15), `Immich:SlideSeconds` (30), `Immich:MaxVideoSeconds` (60), `Immich:CacheSize` (40), `Immich:RotatePercent` (25), `Immich:VideoPrefetchAhead` (5; 0 turns video prefetch off) and `Immich:VideoPrefetchMaxMB` (300).

### AI playlists

Set `AI:OpenAI:ApiKey` and optionally `AI:OpenAI:ChatModel`. Without a key, AI features switch off quietly. Azure OpenAI is configured through the other `AI:*` keys (see `Program.cs`).

### iOS push notifications

Push notifications and background Live Activity updates only work on a server configured with an Apple Developer account's APNs key (all four values in `homespeaker-secrets.env.example`). The published app is tied to the developer's own keys, so on your own server the iPhone and Watch apps still work with live in-app updates, without background pushes.

## AirPlay

Answer yes to the AirPlay question in the installer, or add `COMPOSE_PROFILES=airplay` to `/opt/homespeaker/.env` and set `AIRPLAY_NAME` to the name senders should see. The receiver runs in its own `shairport-sync` container, shares the Pi's audio device, and uses the PulseAudio socket of the user in `PUID` (default 1000).

## Updates

The installer sets up a systemd timer, `homespeaker-update.timer`, that pulls the latest image and restarts every night. Check or run it:

```bash
systemctl list-timers homespeaker-update.timer
sudo systemctl start homespeaker-update.service
```

To stay on a fixed version, set `HOMESPEAKER_IMAGE` in `/opt/homespeaker/.env` to a tag, for example `ghcr.io/snow-jallen/homespeaker:2026.10.22`. To turn automatic updates off, run `sudo systemctl disable --now homespeaker-update.timer`. To update by hand: `cd /opt/homespeaker && sudo docker compose pull && sudo docker compose up -d`.

## HTTPS (optional)

The default install is plain HTTP on your home network. The HTTPS overlay adds port 443 with a certificate you provide:

1. Put a PFX file with no password at `<some folder>/certificate.pfx` (for example from `tailscale cert` plus `openssl pkcs12 -export`).
2. In `/opt/homespeaker/.env`, set `CERT_DIR` to that folder and `COMPOSE_FILE=docker-compose.yml:docker-compose.https.yml`.
3. Download the overlay next to the compose file: `sudo curl -fsSL -o /opt/homespeaker/docker-compose.https.yml https://raw.githubusercontent.com/snow-jallen/homespeaker/master/docker-compose.https.yml`
4. Run `cd /opt/homespeaker && sudo docker compose up -d`.

Setting `COMPOSE_FILE` in `.env` matters: the nightly update runs plain `docker compose` in that folder and would otherwise restart the site without HTTPS.

## Telemetry (optional)

An Aspire Dashboard container can show logs, traces and metrics. Add `telemetry` to `COMPOSE_PROFILES` in `.env` (for example `COMPOSE_PROFILES=airplay,telemetry`), set `OTEL_EXPORTER_OTLP_ENDPOINT=http://aspire-dashboard:18889`, and run `docker compose up -d`. The dashboard is on port 18888. It has no login, so only use it on a trusted network.

## Developer quick start

Needs the .NET 10 SDK and ffmpeg (`winget install gyan.ffmpeg.shared` on Windows).

```powershell
dotnet build HomeSpeaker.sln
dotnet test HomeSpeaker.Server2.Tests
dotnet run --project HomeSpeaker.Server2 --launch-profile https   # https://localhost:7238
```

To see telemetry locally, run the Aspire Dashboard in a container and use the `https` launch profile:

```bash
docker run --rm -it -p 18888:18888 -p 4317:18889 -p 4318:18890 -e DOTNET_DASHBOARD_UNSECURED_ALLOW_ANONYMOUS=true -e DASHBOARD__OTLP__CORS__ALLOWEDORIGINS=http://localhost:5028 --name aspire-dashboard mcr.microsoft.com/dotnet/aspire-dashboard:9.0
```

`CLAUDE.md` describes the architecture. `DEPLOYMENT.md` is for the maintainer's own release pipeline. If you fork the repo, the build workflow publishes a multi-architecture image to your own GitHub Container Registry; deploys only run when you set the `DEPLOY_TARGETS` repository variable.
