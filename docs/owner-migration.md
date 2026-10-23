# Owner migration checklist

Do these once, in order, before merging `feature/shareable` into `master` and pushing. This is a checklist for the maintainer; no agent runs it. The deploy workflow reads its owner-specific values from repository variables now, so without step 1 the first deploy refuses to run (nothing is stopped).

## 1. Before you merge

1. **Check Docker Compose on both Pis.** The deploy needs the Compose plugin 2.24 or newer and refuses otherwise, without stopping anything.

   ```bash
   ssh kitchen docker compose version
   ssh rpi42 docker compose version
   ```

   Update the `docker-compose-plugin` package if either is older than 2.24.

2. **Set the repository variables.**

   ```bash
   gh variable set DEPLOY_TARGETS --body '[{"runner":"kitchen","host":"kitchen","airplay_name":"Kitchen HomeSpeaker"},{"runner":"upstairs","host":"rpi42","airplay_name":"Upstairs HomeSpeaker"}]'
   gh variable set TAILNET_DOMAIN --body 'boston-cloud.ts.net'
   gh variable set DEPLOY_TZ --body 'America/Denver'
   gh variable set DEPLOY_USER --body 'piuser'
   ```

3. **Update the secrets file on each Pi.** Append to `/home/piuser/homespeaker-secrets.env` (the file already holds the Nightscout, Govee, APNs and Immich key values, and they stay):

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

4. **Build the base image first.** The first multi-architecture app build needs `homespeaker-base` to exist for amd64 and arm64 under the lowercase owner name. Dispatch it and wait until it is green:

   ```bash
   gh workflow run build-base-image.yml
   gh run watch
   ```

5. **Remove the old Aspire dashboard container, if one is still running.** Telemetry is now an opt-in compose profile, so a container left from the old compose file is no longer managed. On each Pi:

   ```bash
   docker ps -a --filter name=aspire
   docker rm -f <name>      # only if it is listed
   ```

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

If a Pi misbehaves, revert the merge on `master` and push; the pipeline redeploys the previous code.
