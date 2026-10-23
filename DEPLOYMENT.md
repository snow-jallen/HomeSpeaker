# HomeSpeaker Deployment (maintainer pipeline)

This file describes the release pipeline the maintainer uses to keep their own Raspberry Pis up to date. A household that just wants to run HomeSpeaker should use the one-line installer in `readme.md` instead. Nothing here is needed for that.

## How a release flows

1. A push to `master` (or a `20*.*.*` tag) runs `build-and-push.yml`. It builds a multi-architecture image (linux/arm64 and linux/amd64) on top of the prebuilt base image `ghcr.io/<owner>/homespeaker-base:latest` and pushes it to `ghcr.io/<owner>/homespeaker` as `latest`, as `YYYY.M.D-<sha>`, and as the tag name for tag builds. The owner name is lowercased.
2. When the build succeeds, `deploy.yml` runs once per entry in `DEPLOY_TARGETS`, on that entry's self-hosted runner. It logs in to GHCR, checks for Docker Compose 2.24 or newer (and fails before stopping anything if it is older), renews the Tailscale certificate, writes a `.env`, pulls, restarts with the HTTPS overlay, waits for the site and refreshes the touchscreen browser.
3. `build-base-image.yml` rebuilds the base image (sox, ffmpeg, vlc) when `Dockerfile.base` changes on `master`, or when dispatched by hand. Dispatch it once for a new owner or fork, and wait for it to finish, before the first app build.

Forks do not deploy: `deploy.yml` is skipped while the `DEPLOY_TARGETS` repository variable is unset.

## Repository variables

Set these under Settings, Secrets and variables, Actions, Variables (or with `gh variable set`). Deploy fails early with a clear message if one of the last three is missing.

| Variable | Meaning | Example |
| --- | --- | --- |
| `DEPLOY_TARGETS` | JSON array of `{runner, host, airplay_name}`: the self-hosted runner label, the Pi's host name, and the AirPlay name | `[{"runner":"kitchen","host":"kitchen","airplay_name":"Kitchen HomeSpeaker"}]` |
| `TAILNET_DOMAIN` | Tailscale domain, used for the certificate (`<host>.<domain>`) | `example.ts.net` |
| `DEPLOY_TZ` | Time zone for the containers | `America/Denver` |
| `DEPLOY_USER` | Linux user on the Pis; home of `music`, `cert` and the secrets file | `pi` |

```bash
gh variable set DEPLOY_TARGETS --body '[{"runner":"kitchen","host":"kitchen","airplay_name":"Kitchen HomeSpeaker"}]'
gh variable set TAILNET_DOMAIN --body 'example.ts.net'
gh variable set DEPLOY_TZ --body 'America/Denver'
gh variable set DEPLOY_USER --body 'pi'
```

## What a Pi needs

- A self-hosted GitHub Actions runner labelled with the `runner` name from `DEPLOY_TARGETS`, with Docker and the Docker Compose plugin 2.24 or newer, and passwordless `sudo` for `tailscale cert`, `openssl` and `chown`.
- `/home/<DEPLOY_USER>/music` (the media folder) and `/home/<DEPLOY_USER>/cert` (the certificate is written there as `certificate.pfx`).
- Optionally `/home/<DEPLOY_USER>/homespeaker-secrets.env`, which holds settings and secrets. The deploy only touches it to create it empty if missing; it never overwrites it. The template is `homespeaker-secrets.env.example`.

The deploy writes `.env` in the runner's workspace with `AIRPLAY_NAME`, `HOMESPEAKER_IMAGE`, `HOMESPEAKER_ENV_FILE`, `MUSIC_DIR`, `CERT_DIR`, `TZ`, `COMPOSE_PROFILES=airplay` and `PUID=1000`, and runs `docker compose -f docker-compose.yml -f docker-compose.https.yml`. Containers have fixed names, so each Pi runs one stack. The telemetry profile is not enabled by deploy.

## Settings precedence

`appsettings.json`, then settings saved in the app (SQLite `AppSettings` table in the database on the music volume), then environment variables (including the secrets file), then the command line. Later wins, and an empty environment variable still overrides the database. Anything you put in the secrets file therefore locks that setting against the settings page.

## HTTPS overlay

`docker-compose.https.yml` adds port 443 and mounts `CERT_DIR` (required, containing `certificate.pfx`) at `/certs`, and sets Kestrel's default certificate. The Pi in this pipeline gets its certificate from `tailscale cert`, converted to a PFX with an empty password. A household install using the overlay must also put `COMPOSE_FILE=docker-compose.yml:docker-compose.https.yml` in `/opt/homespeaker/.env`, because the nightly update timer runs plain `docker compose` there.

## Local development

1. Copy `.env.example` to `.env` (git-ignored) when you want to override compose values locally.
2. For the app itself, use `dotnet user-secrets` or environment variables for keys such as `Temperature__ApiKey`; never commit them.
3. `homespeaker-secrets.env` is git-ignored; copy `homespeaker-secrets.env.example` to it on a Pi.

## Push Notifications

HomeSpeaker supports APNs-backed push notifications for registered iOS devices. Pushes are off until all four values below are set.

### APNs configuration

Set these values in the secrets file or environment:

- `PushNotifications__Apns__Topic`
- `PushNotifications__Apns__TeamId`
- `PushNotifications__Apns__KeyId`
- `PushNotifications__Apns__PrivateKeyBase64` (base64-encoded contents of the `.p8` auth key)
- `PushNotifications__Apns__UseSandbox` (`true` for development/TestFlight-style sandbox, `false` for production APNs)

Optional: `PushNotifications__PollIntervalSeconds` (default 120, minimum 30).

### Device registration API

The canonical mobile flow is:

1. `PUT /api/homespeaker/push/installations/{installationId}` to register/update the APNs token
2. `GET /api/homespeaker/push/installations/{installationId}` to read current status

The iOS app keeps this flow registration-only. Alert categories and delivery rules stay server-side so the device UI can remain a simple push status surface.

Example registration payload:

```json
PUT /api/homespeaker/push/installations/ios-main-phone
{
  "installationId": "ios-main-phone",
  "platform": "ios",
  "deviceToken": "<hex-token>",
  "deviceName": "Jonathan's iPhone",
  "bundleId": "com.example.HomeSpeaker"
}
```

A simplified compatibility route also exists for direct registration/unregistration:

```text
POST /api/push/devices/register
DELETE /api/push/devices/{installationId}
```
