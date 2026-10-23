# Owner migration checklist

Do these once, in order, before merging `feature/shareable` into `master` and pushing. This is a checklist for the maintainer; no agent runs it. The deploy workflow reads its owner-specific values from repository variables now, so without step 3 the first deploy refuses to run (nothing is stopped).

## 1. Before you merge

1. **Check Docker Compose on both Pis.** The deploy needs the Compose plugin 2.24 or newer and refuses otherwise, without stopping anything.

   ```bash
   ssh kitchen docker compose version
   ssh rpi42 docker compose version
   ```

   If either is missing or older than 2.24, install the plugin. Leave the old `docker-compose` v1 binary installed; it doesn't conflict, and a revert (section 5) needs it.
   - Docker from Docker's apt repo (`docker-ce`): `sudo apt-get install -y docker-compose-plugin`.
   - Docker from Debian's `docker.io` package: bookworm has no plugin package, so install the binary. Use `aarch64` if `dpkg --print-architecture` prints `arm64`, or `armv7` for `armhf`:

     ```bash
     sudo mkdir -p /usr/local/lib/docker/cli-plugins
     sudo curl -fsSL https://github.com/docker/compose/releases/latest/download/docker-compose-linux-aarch64 \
       -o /usr/local/lib/docker/cli-plugins/docker-compose
     sudo chmod +x /usr/local/lib/docker/cli-plugins/docker-compose
     ```

2. **Tag the image that's running now**, so you have a pre-change image to roll back to (section 5). Master builds before this branch were only ever tagged `latest`, and the next build overwrites it. From any machine logged in to GHCR with a token that has `write:packages`:

   ```bash
   docker buildx imagetools create -t ghcr.io/snow-jallen/homespeaker:pre-shareable ghcr.io/snow-jallen/homespeaker:latest
   ```

   Without such a token, write down the digest instead. On a Pi, `docker image inspect --format '{{index .RepoDigests 0}}' ghcr.io/snow-jallen/homespeaker:latest` prints `ghcr.io/snow-jallen/homespeaker@sha256:…`, which can be used as an image reference.

3. **Set the repository variables.** Run these from bash (Git Bash on Windows). PowerShell strips the inner double quotes from the `DEPLOY_TARGETS` JSON, which leaves an invalid value; check it afterwards with `gh api repos/snow-jallen/HomeSpeaker/actions/variables/DEPLOY_TARGETS --jq .value`.

   ```bash
   gh variable set DEPLOY_TARGETS --body '[{"runner":"kitchen","host":"kitchen","airplay_name":"Kitchen HomeSpeaker"},{"runner":"upstairs","host":"rpi42","airplay_name":"Upstairs HomeSpeaker"}]'
   gh variable set TAILNET_DOMAIN --body 'boston-cloud.ts.net'
   gh variable set DEPLOY_TZ --body 'America/Denver'
   gh variable set DEPLOY_USER --body 'piuser'
   ```

4. **Update the secrets file on each Pi.** Append to `/home/piuser/homespeaker-secrets.env` (the file already holds the Nightscout, Govee, APNs and Immich key values, and they stay):

   ```
   Immich__BaseUrl=https://immich.boston-cloud.ts.net
   Immich__AlbumId=c7fcba71-bdc5-4de0-9511-aa45ea22959f
   Forecast__Latitude=39.2683
   Forecast__Longitude=-111.63686
   Features__Anchors=true
   Temperature__Rooms=Outside: Outside; Greenhouse: Greenhouse; Emma: Emma; Boys: Boy; Downstairs: Downstairs, Mom, Dad, Parent; Girls: Girl
   Temperature__OutsideRoom=Outside
   Temperature__IndoorRoom=Girls
   Setup__Completed=true
   ```

   Also check these:
   - `Temperature__ApiBaseUrl` must be `https://openapi.api.govee.com/router/api/v1/` (with the trailing slash). If the old value is the bare host, change it.
   - Keep all four `PushNotifications__Apns__*` values so the push endpoints stay enabled on your Pis.
   - If the file does not have `Immich__ApiKey` or `IMMICH_API_KEY`, add it.
   - These values lock the matching settings on the `/settings` page. Remove a line if you would rather edit that setting in the app.

5. **Build the base image first.** The first multi-architecture app build needs `homespeaker-base` to exist for amd64 and arm64 under the lowercase owner name. Dispatch it and wait until it is green:

   ```bash
   gh workflow run build-base-image.yml
   gh run watch
   ```

6. **Remove the old Aspire dashboard container from before this change, if it is still running.** Telemetry is now an opt-in compose profile, so a dashboard container left from the old compose file is no longer managed. On each Pi:

   ```bash
   docker ps -a --filter name=aspire
   docker rm -f <name>      # only the old one, if listed
   ```

   Leave the new `homespeaker-aspire` container (telemetry profile) alone if you enable it.

## 2. Merge

Merge `feature/shareable` into `master` and push. `build-and-push.yml` builds the image and `deploy.yml` then deploys to each target.

## 3. Confirm parity on both Pis

```bash
curl -sk https://kitchen.boston-cloud.ts.net/api/features
curl -sk https://rpi42.boston-cloud.ts.net/api/features
```

Check that the output reports the modules as on, and then confirm each in the UI:

- [ ] HTTPS answers on port 443 with the Tailscale certificate
- [ ] The Immich slideshow runs on the kiosk (photos from your album, videos play)
- [ ] The climate rooms show with the right names, and the window advice appears
- [ ] Glucose (Nightscout) shows on the home page
- [ ] Anchors are on and your habits are still there
- [ ] AirPlay shows "Kitchen HomeSpeaker" and "Upstairs HomeSpeaker" and plays
- [ ] Push is on (`/api/features` shows it) and a test notification reaches your phone
- [ ] `/setup` does not appear (`Setup__Completed=true`)

## 4. Transition notes

- **iOS app.** The TestFlight build installed today does not show room temperatures against the new server until the new iOS build ships. Everything else in it keeps working.
- **Climate card order.** Rooms now appear in the order of `Temperature__Rooms`, not a fixed order. Keep `Emma` before `Girls` in that line: the first matching room wins, and Emma's sensor name would otherwise match the `Girl` pattern.

## 5. Rolling back

If a Pi misbehaves, either:

- **Revert the merge** on `master` and push; the pipeline redeploys the previous code. The reverted `deploy.yml` is the old one, which calls the standalone `docker-compose` (v1) binary, so keep that binary installed on both Pis until a deploy with the new workflow has succeeded.
- **Or redeploy an older image** through the new workflow. In `deploy.yml`, change the `HOMESPEAKER_IMAGE=...:latest` line to the image to run, push, and run the deploy (`gh workflow run deploy.yml`). This keeps the new deploy steps and Compose v2. Change the line back to `latest` once the fix is in.
  - To get the code from before this change, use the `pre-shareable` tag (or the digest) from step 2 of section 1. It's the only pre-change image; master builds before this branch were tagged `latest` only, and dated `YYYY.M.D-<sha>` tags exist only for builds made after the merge. The old image runs fine on the new compose files and ignores the new settings, and this change adds no database migrations, so the database needs nothing.
  - To roll back between post-merge builds, use a dated tag from the package page (`https://github.com/snow-jallen/homespeaker/pkgs/container/homespeaker`).
