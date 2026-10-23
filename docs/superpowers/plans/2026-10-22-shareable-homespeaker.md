# Shareable HomeSpeaker Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make HomeSpeaker installable by another household (one install command, a first-run setup page, no personal defaults), while the owner's two Pis keep running unchanged through configuration alone.

**Architecture:** The public repo becomes the product. Everything house-specific moves into configuration: env vars in the owner's secrets file, GitHub repo variables, or settings stored in a SQLite table that a custom `IConfigurationProvider` reads, with env vars taking precedence. Personal features (Nightscout, Govee temperature, Anchors, forecast, iOS push) are gated on config: their endpoints and workers are only mapped or registered when enabled, and the UI hides them. Packaging adds a generic compose file with profiles, multi-arch versioned images, an install script that advertises `_homespeaker._tcp` over the host's Avahi, and Bonjour discovery in the iOS app.

**Tech Stack:** .NET 10 / ASP.NET Core Blazor Server, EF Core + Microsoft.Data.Sqlite, xUnit, Docker Compose, GitHub Actions (buildx), bash, SwiftUI + Network.framework (`NWBrowser`).

**Spec:** The decisions in the conversation of 2026-10-22, summarized here:
- **Phase 0:** remove personal defaults.
- **Phase 1:** make features config-gated modules.
- **Phase 2:**
  - settings in the `/music` database
  - an admin PIN
  - HTTP by default
  - a local-folder photo source
  - a first-run setup page
- **Phase 3:**
  - install script
  - compose profiles (no Immich profile)
  - multi-arch versioned images
  - Avahi advertisement
  - nightly update timer
- **Phase 4:**
  - Bonjour discovery in iOS
  - no server push for other households: the app hides push and starts Live Activities without a push token when the server's push isn't configured

## Global Constraints

- Work on branch `feature/shareable` off `master`. The user has allowed commits **on that branch only**. Commit at the end of each task, with a message `<area>: <summary>` and the session's attribution trailer. Never merge into master or push; the user does that after the owner-migration checklist.
- Debug builds treat warnings as errors. `dotnet build HomeSpeaker.sln` must pass with zero warnings after every task, and `dotnet test HomeSpeaker.Server2.Tests` must pass.
- Naming:
  - private fields are camelCase with no underscore
  - private methods are camelCase
  - one top-level type per file (MA0048)
  - `private const` names are PascalCase
- **Owner continuity:** every behavior the owner relies on today must be reproducible by config only. Task 13 lists the exact env lines. No task may remove a feature the owner uses.
- Config precedence is lowest to highest: `appsettings.json` → **database settings** → environment variables → command line. An empty env var (`Admin__PinHash=`) overrides the database.
- New `/api/features` fields are additive, and Swift decodes them as optionals so older app builds keep working.
- No new NuGet packages except where a task names one. Use the `Microsoft.Data.Sqlite` that EF Core Sqlite already brings in.
- iOS code can't be compiled here (Windows). Add new Swift files to `project.pbxproj` by hand (explicit file refs, see Task 14), and say they were not compiled.

## Review Focus

1. **Owner upgrade path.** After the change the owner deploys with their secrets file plus repo variables, and the kitchen and upstairs Pis show the same features: HTTPS on 443, Immich slideshow, climate rooms, glucose, anchors, AirPlay. Task 4 pins env-over-database precedence. Task 13 is the checklist.
2. **First boot with no database or no table.** The settings provider must load an empty set, not throw, when the SQLite file or `AppSettings` table doesn't exist yet. `MigrationApplier` runs only after configuration is built. Test in Task 4.
3. **Lost or locked-out PIN.** An empty `Admin__PinHash` env var disables the PIN. After 5 wrong tries from one IP within a minute, further tries are refused for 60 s. Tests in Task 5.
4. **Unlock redirect.** `returnUrl` must be a local path. `//evil.com` and `https://evil.com` fall back to `/`. Test in Task 5.
5. **Folder photos.**
   - files are found in subfolders
   - names with spaces or unicode work
   - `.png`, `.heic` and dotfiles are ignored
   - ids are stable across restarts (they're derived from the relative path)
   - a missing `photos` folder gives an empty list, not an error

   Tests in Task 6.
6. **Settings page vs. env overrides.** A key set by an env var shows as "Set by the server's environment" and is read-only; saving never writes it. Test in Task 7 (`SettingsForm`).

---

## Phase 0 — Remove personal defaults

### Task 1: Remove personal defaults and dead files

**Files:**
- Modify:
  - `HomeSpeaker.Server2/appsettings.json`: set `Immich.BaseUrl` and `Immich.AlbumId` to `""`; delete `OTEL_EXPORTER_OTLP_ENDPOINT` and `OTEL_EXPORTER_OTLP_PROTOCOL`.
  - `HomeSpeaker.Server2/Services/ForecastService.cs:113-114`: no default coordinates. Read `Forecast:Latitude` and `Forecast:Longitude` as `double?`. If either is missing, return an empty `ForecastStatus` and log once at Information level.
  - `HomeSpeaker.Server2/FeatureFlags.cs`: add `ForecastEnabled`.
  - `homespeaker-secrets.env.example`: drop the Grafana and lgtm section; add `Forecast__Latitude` and `Forecast__Longitude`.
  - `.github/workflows/diagnose.yml`: drop the lgtm and Grafana steps.
- Delete:
  - `HomeSpeaker.Server2/wwwroot/appsettings.json`, `appsettings.Production.json` and `appsettings.Tailscale.json` (nothing reads `ServerAddress`)
  - `HomeSpeaker.Server2/Pages/Demo/` (Counter, FetchData)
  - `scripts/homespeaker-airplay-start` and `scripts/homespeaker-airplay-stop` (unreferenced)
  - the untracked `HomeSpeaker.WebAssembly/` folder (bin/obj only)
  - `HomeSpeaker.Server2/HomeSpeaker.Server2.sln` (stale)
  - `refresh-cert.sh` (duplicates deploy.yml)
- Test: create `HomeSpeaker.Server2.Tests/FeatureFlagsTests.cs`.

**Interfaces:**
- Produces: `FeatureFlags.ForecastEnabled(IConfiguration) -> bool`, true when both `Forecast:Latitude` and `Forecast:Longitude` parse as doubles.

- [ ] **Step 1: Write failing tests** `ForecastEnabled_WithBothCoordinates_IsTrue`, `ForecastEnabled_MissingLongitude_IsFalse` and `ForecastEnabled_NonNumeric_IsFalse`. Use an in-memory `ConfigurationBuilder().AddInMemoryCollection(...)`.
- [ ] **Step 2: Run** `dotnet test HomeSpeaker.Server2.Tests --filter FeatureFlagsTests`. Expected: FAIL, because `ForecastEnabled` doesn't exist.
- [ ] **Step 3: Implement** `ForecastEnabled` and the ForecastService change. Make the appsettings, file deletions and workflow edits above.
- [ ] **Step 4: Verify** that `dotnet build HomeSpeaker.sln` passes and `dotnet test HomeSpeaker.Server2.Tests` passes. `grep -rn "boston-cloud\|39.2683" HomeSpeaker.Server2 --include=*.cs --include=*.json --include=*.razor` should return nothing.

### Task 2: Configurable temperature rooms

The rooms come from the config string `Temperature:Rooms`, for example `Outside: Outside; Greenhouse: Greenhouse; Emma: Emma; Boys: Boy; Downstairs: Downstairs, Mom, Dad, Parent; Girls: Girl`.
- Entries are separated by `;`. Each entry is `Room name: pattern, pattern`, and order matters (first match wins).
- An entry with no `:` uses its name as its only pattern.
- If the setting is empty or unset, every Govee device becomes its own room, named after the device.
- `Temperature:OutsideRoom` names the outdoor room. Its default is `Outside` when a room with that name exists; otherwise there's no outside room, and window advice and alerts are off.
- `Temperature:IndoorRoom` (default: the first room that isn't the outside room) is compared against outside for window advice and push alerts.
- **Window settings.** These are always stored in °F, and the settings UI converts them for display:
  - `Temperature:WindowAdvice` (default `true`; `false` hides the advice line and suppresses the temperature push alert)
  - `Temperature:WindowMinOutside` (default `50`; replaces the hard-coded 50 in `TemperatureService`)
  - `TemperatureThreshold` stays as the existing "about the same" margin, default `2`
- **Units.** `Units:Temperature` is `F` (default) or `C`. Readings stay °F internally (Govee and Open-Meteo are requested in °F). Conversion happens **only when displayed**, through one helper.

**Files:**
- Create:
  - `HomeSpeaker.Server2/Services/TemperatureRoomConfig.cs`
  - `Services/TemperatureUnit.cs` (`enum TemperatureUnit { Fahrenheit, Celsius }`)
  - `Services/TemperatureDisplay.cs`
- Modify:
  - Every place that renders a temperature goes through `TemperatureDisplay.Format`:
    - `Components/Health/TemperatureMonitor.razor`
    - `Components/Weather/ForecastMonitor.razor`
    - `Pages/Index.razor` (climate card, "Next hours" columns, high/low)
    - `Components/Glass/AmbientOverlay.razor`
    - the push alert body
    - `WindowAdvice` text (degree differences convert with `TemperatureDisplay.ConvertDelta`)

    `grep -rn "°" HomeSpeaker.Server2 --include=*.razor --include=*.cs` lists the sites.
  - `Services/TemperatureService.cs`: drop the hard-coded `roomPatterns` and use `TemperatureRoomConfig`. Drop the code that fills the legacy single-room properties. Window logic compares `OutsideRoom` with `IndoorRoom`.
  - `Services/TemperatureDeviceMatcher.cs`: when the room list is empty, each device becomes its own room, `RoomPattern(device.DeviceName, [device.DeviceName])`.
  - `HomeSpeaker.Shared/Temperature/TemperatureStatus.cs`:
    - Remove `YoungerGirlsRoomTemperature`, `OlderGirlsRoomTemperature`, `BoysRoomTemperature`, `MomAndDadsRoomTemperature` and `GreenhouseTemperature`.
    - Add `string? IndoorRoomName` and `double? IndoorTemperature`.
    - Keep `OutsideTemperature`.
    - The Swift fields are optional, so decoding survives.
  - `Services/Dashboard/ClimateRoom.cs`: `From(status)` returns every room in `status.Rooms` except the outside one, in config order, labelled with the room name.
  - `Services/Dashboard/WindowAdvice.cs`, `Services/PushNotificationService.cs:281-296`: use `IndoorTemperature`. The push body becomes `$"Outside {outside:F0}°, {status.IndoorRoomName} {indoor:F0}° (Δ {diff:F0}°)."`
  - `Components/Health/TemperatureMonitor.razor`: render `ClimateRoom.From(status)` instead of the named fields.
  - `Pages/Index.razor:196-255` and `Components/Glass/AmbientOverlay.razor`: replace any legacy-property reads.
  - Tests: `Temperature/TemperatureServiceTests.cs`, `Dashboard/ClimateCardTests.cs`, `PushNotificationServiceTests.cs` and `PushNotificationTests.cs` move to rooms and indoor fields.
- Test: create `HomeSpeaker.Server2.Tests/Temperature/TemperatureRoomConfigTests.cs`.

**Interfaces:**
- Produces:
  - `TemperatureRoomConfig.Parse(string? value) -> IReadOnlyList<TemperatureDeviceMatcher.RoomPattern>`
  - `TemperatureRoomConfig.FromConfiguration(IConfiguration) -> TemperatureSettings`, where `sealed record TemperatureSettings(IReadOnlyList<RoomPattern> Rooms, string? OutsideRoom, string? IndoorRoom, bool WindowAdvice, double WindowMinOutsideF, double ThresholdF)` lives in `Services/TemperatureSettings.cs`
  - `TemperatureDisplay.UnitFrom(IConfiguration) -> TemperatureUnit`
  - `TemperatureDisplay.Convert(double fahrenheit, TemperatureUnit) -> double`
  - `TemperatureDisplay.ConvertDelta(double fahrenheitDelta, TemperatureUnit) -> double`
  - `TemperatureDisplay.ToFahrenheit(double value, TemperatureUnit) -> double` (settings UI save path)
  - `TemperatureDisplay.Format(double? fahrenheit, TemperatureUnit) -> string`: `"72°"` (whole degrees, away-from-zero rounding, no unit letter on tiles), or `"–"` for null
  - `TemperatureStatus` gains `string? OutsideRoomName`. `ClimateRoom.From` excludes that room and keeps the others in config order.
  - `/api/features` gains `temperatureUnit` (`"F"` or `"C"`) in Task 3.

- [ ] **Step 1: Write failing tests:**
  - `Parse_OwnerString_KeepsOrderAndPatterns`: 6 rooms; `Downstairs` has 4 patterns; `Boys` has pattern `Boy`.
  - `Parse_EntryWithoutColon_UsesNameAsPattern`
  - `Parse_EmptyOrWhitespace_ReturnsEmpty`
  - `Parse_TrimsAndSkipsEmptyEntries`: `" A: x ;; B "` gives 2 rooms.
  - `FromConfiguration_IndoorDefaultsToFirstNonOutsideRoom`
  - `Match_NoRooms_EachDeviceIsItsOwnRoom` in `TemperatureDeviceMatcherTests`
  - `ClimateRoom_From_ListsConfiguredIndoorRoomsInOrder`
  - `PushAlert_UsesIndoorRoomName`: the body contains `"Girls 74°"` for IndoorRoomName "Girls".
  - `FromConfiguration_NoOutsideRoom_WindowAdviceOff`
  - `FromConfiguration_WindowMinOutsideDefaultsTo50`
  - `Status_OutsideBelowMin_ShouldCloseWindows`: with min 40 and outside 45 °F, warmer inside, the result is false (open); with min 50, true.
  - `WindowAdviceDisabled_NoAdviceAndNoPushAlert`
  - `TemperatureDisplay` theory: `Convert(212, Celsius)` = 100, `Convert(32, Celsius)` = 0, `ConvertDelta(9, Celsius)` = 5, `ToFahrenheit(10, Celsius)` = 50, `Format(72.5, Fahrenheit)` = `"73°"`, `Format(null, …)` = `"–"`, `UnitFrom` with `"c"` gives Celsius and with garbage gives Fahrenheit
  - `WindowAdvice_Celsius_ConvertsDifference`: a 9 °F difference reads `"5° cooler outside"`
- [ ] **Step 2: Run** `dotnet test HomeSpeaker.Server2.Tests --filter "TemperatureRoomConfigTests|TemperatureDeviceMatcherTests|ClimateCardTests|PushNotification"`. Expected: FAIL (compile errors).
- [ ] **Step 3: Implement** the parser, the matcher fallback and the consumers listed above.
- [ ] **Step 4: Verify** that the full build and test suite pass, and that `grep -rn "YoungerGirls\|girl's room" HomeSpeaker.Server2 HomeSpeaker.Shared` returns nothing. The Swift side is Task 14.

---

## Phase 1 — Features as config-gated modules

### Task 3: Feature modules

**Flags** (all in `FeatureFlags.cs`):
- `TemperatureEnabled` stays as it is.
- `BloodSugarEnabled` stays as it is.
- `ForecastEnabled` comes from Task 1.
- New `AnchorsEnabled`: `Features:Anchors` is `"true"` (case-insensitive). Default off.
- New `PushEnabled`: all four of `PushNotifications:Apns:Topic`, `TeamId`, `KeyId` and (`PrivateKeyBase64` or `PrivateKeyPath`) are non-empty.

**Files:**
- Create, moving each feature's endpoints out of `Program.cs` verbatim:
  - `Endpoints/TemperatureEndpoints.cs` (`MapTemperatureApi`, from Program.cs:437-504)
  - `Endpoints/BloodSugarEndpoints.cs` (`MapBloodSugarApi`, :464-530 plus `/ns`)
  - `Endpoints/ForecastEndpoints.cs` (`MapForecastApi`, :532-571)
  - `Endpoints/AnchorEndpoints.cs` (`MapAnchorApi`, :573-744 and :777-789, plus `MapHub<AnchorHub>`)
  - `Endpoints/PushEndpoints.cs` (`MapPushApi`, :380-435)
  - `FeatureModules.cs`, with `MapFeatureModules(this WebApplication app)`. It maps each group only if its flag is on.
- Modify:
  - `Program.cs`:
    - Register `DailyAnchorWorker` only if `AnchorsEnabled`.
    - Register `HealthAlertWorker` only if `PushEnabled && (TemperatureEnabled || BloodSugarEnabled)`.
    - Keep the services themselves registered, because `DashboardState` injects them.
    - `/api/features` adds `ForecastEnabled`, `AnchorsEnabled` and `PushEnabled`.
  - `Services/HealthAlertWorker.cs:76-110`: replace the inline checks with `FeatureFlags`.
  - `Services/Dashboard/DashboardState.cs`: add `ForecastEnabled` and `AnchorsEnabled`, fixed at construction like the existing two. `refreshHealthAsync` skips the forecast and anchor fetches when they're off.
  - `Pages/Index.razor`, `Components/Glass/AmbientOverlay.razor`, `Components/Glass/NightClock.razor`: gate the forecast parts on `Dashboard.ForecastEnabled`, and the anchors card on `Dashboard.AnchorsEnabled && anchors.Count > 0`.
  - `Components/Glass/AppMenu.razor:34-36`: filter the static `MenuItem[]`. Anchors shows only when `AnchorsEnabled`, NightScout when `BloodSugarEnabled`, Climate when `TemperatureEnabled || ForecastEnabled`. Inject `IConfiguration`.
  - `Components/Layout/NavMenu.razor:65-75`: the same gates, plus remove the AspireDashboard link unless `OTEL_EXPORTER_OTLP_ENDPOINT` is set.
  - `Pages/Health/NightScout.razor`, `Pages/Admin/Anchors*.razor`, `Pages/Health/Climate.razor`: when their flag is off, show "This feature is turned off. Turn it on in Settings." with a link to `/settings`.
  - Delete `Components/Health/BloodSugarMonitor.razor` and its `.css`. They're dead code: nothing renders them.
- Test:
  - extend `HomeSpeaker.Server2.Tests/FeatureFlagsTests.cs`
  - create `HomeSpeaker.Server2.Tests/FeatureModuleEndpointTests.cs`, using `WebApplicationFactory<Program>` with in-memory config: `MediaFolder` = temp dir, `SqliteConnectionString` = a temp file

**Interfaces:**
- Produces:
  - `FeatureFlags.AnchorsEnabled(IConfiguration) -> bool`
  - `FeatureFlags.PushEnabled(IConfiguration) -> bool`
  - `app.MapFeatureModules()`
  - `DashboardState.ForecastEnabled` and `DashboardState.AnchorsEnabled` (both `bool`)
  - JSON fields `forecastEnabled`, `anchorsEnabled` and `pushEnabled` on `/api/features`

- [ ] **Step 1: Write failing tests:**
  - `AnchorsEnabled_DefaultsOff`, `AnchorsEnabled_TrueCaseInsensitive`
  - `PushEnabled_RequiresAllFourApnsValues`, `PushEnabled_AcceptsKeyPathInsteadOfBase64`
  - endpoint tests:
    - `Features_DefaultConfig_AllPersonalModulesOff`: `/api/features` returns all three new fields false
    - `AnchorApi_WhenDisabled_Returns404`
    - `AnchorApi_WhenEnabled_Returns200`
    - `BloodSugarApi_WhenNightscoutUnset_Returns404`
- [ ] **Step 2: Run** `dotnet test HomeSpeaker.Server2.Tests --filter "FeatureFlagsTests|FeatureModuleEndpointTests"`. Expected: FAIL.
- [ ] **Step 3: Implement.** Move the endpoint code without changing its behavior, add the flags, and gate the UI.
- [ ] **Step 4: Verify** that the build and full test suite pass. Then use the `run-homespeaker-server` skill to run locally with default config, and confirm that Home shows no glucose, climate, forecast or anchors and the menu has no NightScout, Climate or Anchors entries. Run again with `Features__Anchors=true` and confirm the Anchors menu item appears.

---

## Phase 2 — Settings, PIN, photos, setup

### Task 4: Database-backed settings configuration

The `AppSettings` table is owned by the store, not by the EF model, so it needs no migration: `CREATE TABLE IF NOT EXISTS AppSettings (Key TEXT PRIMARY KEY, Value TEXT NOT NULL)`. Keys use `:` form (`Immich:BaseUrl`).

**Files:**
- Create:
  - `HomeSpeaker.Server2/Settings/SqliteSettingsStore.cs`
  - `Settings/DatabaseSettingsConfigurationSource.cs`
  - `Settings/DatabaseSettingsConfigurationProvider.cs`
  - `Settings/SettingsConfigurationExtensions.cs`
- Modify `Program.cs`: right after `CreateBuilder`, call `builder.Configuration.AddDatabaseSettings(builder.Configuration["SqliteConnectionString"])`. It inserts the source just before the first `EnvironmentVariablesConfigurationSource`. Register the store as a singleton and the provider (for `Reload`) as `DatabaseSettingsConfigurationProvider` via the source.
- Test: `HomeSpeaker.Server2.Tests/Settings/SqliteSettingsStoreTests.cs` and `Settings/DatabaseSettingsConfigurationTests.cs` (temp-file SQLite; reuse `Photos/TempDirectory.cs`).

**Interfaces:**
- Produces:
  - `SqliteSettingsStore(string connectionString)`, with `IReadOnlyDictionary<string,string> LoadAll()` and `void Save(IReadOnlyDictionary<string,string?> changes)`. A null value deletes the key. All changes go in one transaction.
  - `IConfigurationBuilder.AddDatabaseSettings(string? connectionString) -> IConfigurationBuilder`. A null or empty connection string adds nothing.
  - `DatabaseSettingsConfigurationProvider.Reload()`, which re-reads and fires the change token.
  - `ISettingsWriter`, a singleton with `void Save(IReadOnlyDictionary<string,string?> changes)` that saves and then reloads. Tasks 5, 7 and 8 use it.

- [ ] **Step 1: Write failing tests:**
  - `LoadAll_NoDatabaseFile_ReturnsEmpty`: no exception; the file may be created.
  - `Save_ThenLoadAll_RoundTrips`
  - `Save_NullValue_DeletesKey`
  - `Configuration_DatabaseValueOverridesAppsettings`
  - `Configuration_EnvVarOverridesDatabaseValue`
  - `Configuration_EmptyEnvVarOverridesDatabaseValue`: `Admin__PinHash=""` makes `config["Admin:PinHash"] == ""`.
  - `SettingsWriter_Save_ReloadsConfiguration`: after `Save`, `IConfiguration` sees the new value without a rebuild.
- [ ] **Step 2: Run** `dotnet test HomeSpeaker.Server2.Tests --filter Settings`. Expected: FAIL.
- [ ] **Step 3: Implement** with `Microsoft.Data.Sqlite` (`SqliteConnection`). The provider's `Load()` catches `SqliteException`, logs nothing (there's no logger yet), and leaves `Data` empty.
- [ ] **Step 4: Verify** that the full suite passes, and that the app still starts on the real `HomeSpeaker.db` (run-homespeaker-server skill, `/alive` returns 200).

### Task 5: Admin PIN

**Rules:**
- A PIN is 4–12 digits.
- It's stored as `Admin:PinHash` = `pbkdf2-sha256$100000$<base64 salt>$<base64 hash>` (16-byte salt, 32-byte hash, `Rfc2898DeriveBytes.Pbkdf2`).
- With no or empty `Admin:PinHash`, admin pages are open.

**Auth setup:**
- Cookie scheme `HomeSpeakerAdmin`, cookie `hs-admin`, 30-day sliding expiry, `SameSite=Lax`, not `Secure` (it has to work over HTTP).
- `LoginPath` is `/unlock`.
- The policy `"Admin"` uses `AdminPinRequirement`. Its handler succeeds when no PIN is set, or when the user is authenticated with the `HomeSpeakerAdmin` scheme.

**Files:**
- Create:
  - `Settings/AdminPin.cs`
  - `Settings/PinAttemptLimiter.cs`
  - `Settings/AdminPinRequirement.cs`
  - `Settings/AdminPinHandler.cs`
  - `Endpoints/AdminEndpoints.cs` (`MapAdminApi`):
    - `POST /api/admin/unlock` takes form fields `pin` and `returnUrl`.
      - Success: sign in and redirect to the safe return URL.
      - Wrong PIN: redirect to `/unlock?error=1&returnUrl=…`.
      - Rate-limited: redirect to `/unlock?error=locked&returnUrl=…`.
    - `POST /api/admin/lock` signs out and redirects to `/`.
  - `Pages/Admin/Unlock.razor` (`@page "/unlock"`): static SSR form with `<AntiforgeryToken/>`, posting to `/api/admin/unlock`.
  - `Components/RedirectToUnlock.razor`
- Modify:
  - `Program.cs`: `AddAuthentication().AddCookie(...)`, `AddAuthorization(o => o.AddPolicy("Admin", ...))`, `AddCascadingAuthenticationState()`, the `IAuthorizationHandler` registration and `PinAttemptLimiter` singleton, `UseAuthentication()`/`UseAuthorization()` after `UseRouting()`, and `app.MapAdminApi()`.
  - `Components/Routes.razor`: `RouteView` becomes `AuthorizeRouteView`, with `<NotAuthorized><RedirectToUnlock/></NotAuthorized>`.
- Test:
  - `Settings/AdminPinTests.cs`
  - `Settings/PinAttemptLimiterTests.cs` (`ManualTimeProvider`)
  - `Settings/AdminEndpointTests.cs` (`WebApplicationFactory`, setting `Admin:PinHash` via in-memory config)

**Interfaces:**
- Produces:
  - `AdminPin.IsValidFormat(string? pin) -> bool`
  - `AdminPin.Hash(string pin) -> string`
  - `AdminPin.Verify(string pin, string? storedHash) -> bool`
  - `AdminPin.IsSet(IConfiguration) -> bool`
  - `AdminPin.SafeReturnUrl(string? returnUrl) -> string`: returns a value only if it starts with a single `/` and isn't `//` or `/\`; otherwise `/`.
  - `PinAttemptLimiter(TimeProvider)`, with `bool IsLocked(string clientKey)`, `void RecordFailure(string clientKey)` and `void Reset(string clientKey)`. A client is locked after 5 failures within 60 s, for 60 s from the 5th failure.
  - Pages that need the PIN use `@attribute [Authorize(Policy = "Admin")]`.

- [ ] **Step 1: Write failing tests:**
  - `Hash_ThenVerify_RoundTrips`
  - `Verify_WrongPin_False`
  - `Verify_MalformedHash_False` (no throw)
  - `Hash_SamePinTwice_DifferentSalts`
  - `IsValidFormat` theory: `"1234"` true, `"123"` false, `"12a4"` false, 13 digits false
  - `IsSet_EmptyHash_False`
  - `SafeReturnUrl` theory: `/settings`→`/settings`, `//evil.com`→`/`, `https://evil.com`→`/`, `/\evil`→`/`, null→`/`
  - limiter: `FiveFailures_Locks`, `LockExpiresAfter60Seconds`, `Reset_ClearsFailures`
  - endpoints:
    - `Unlock_CorrectPin_SetsCookieAndRedirects`
    - `Unlock_WrongPin_RedirectsWithError`
    - `Unlock_SixthTry_RedirectsLocked`
    - `SettingsPage_WithPinAndNoCookie_RedirectsToUnlock` (GET `/settings` → 302 to `/unlock?ReturnUrl=…`; `/settings` comes from Task 7, so use `/unlock`'s neighbor `/admin-check`, a tiny `[Authorize(Policy="Admin")]` endpoint mapped in `MapAdminApi` and returning 204)
    - `AdminCheck_NoPinSet_Returns204`
- [ ] **Step 2: Run** `dotnet test HomeSpeaker.Server2.Tests --filter "AdminPin|PinAttemptLimiter|AdminEndpoint"`. Expected: FAIL.
- [ ] **Step 3: Implement.** The client key is `HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown"`.
- [ ] **Step 4: Verify** that the full suite passes. Run locally with `Admin__PinHash` set to a hash of `1234`, then check the redirect, the unlock flow and lock in the browser (run-homespeaker-server skill).

### Task 6: Photo source seam and local-folder source

**Selection:** `Photos:Source` = `immich` | `folder`. If unset, use `immich` when `ImmichOptions.IsConfigured`, otherwise `folder`.

**The folder source:**
- Reads `{MediaFolder}/photos`, created at startup when the folder source is selected.
- Includes `*.jpg` and `*.jpeg`, recursively and case-insensitively, skipping names that start with `.`.
- `AssetId` is the GUID formed from the first 16 bytes of `SHA256(UTF8(relativePath with '/' separators, lower-cased))`. That keeps it GUID-shaped for `PhotoCacheStore.IsValidAssetId`.
- `TakenAt` is the file's last-write time in UTC. There's no location, no faces and no video.
- `DownloadPreviewAsync` copies the file bytes.

**Files:**
- Create:
  - `Services/Photos/IPhotoSource.cs`
  - `Services/Photos/ImmichPhotoSource.cs` (wraps `IImmichClient` plus `ImmichOptions.AlbumId`)
  - `Services/Photos/FolderPhotoSource.cs`
  - `Services/Photos/PhotoSourceKind.cs` (`enum PhotoSourceKind { Immich, Folder }` plus `static PhotoSourceKind Resolve(IConfiguration, ImmichOptions)`; one type per file, so put `Resolve` in `PhotoSourceSelector.cs`)
- Modify:
  - `Services/Photos/PhotoSyncer.cs`: take `IPhotoSource` instead of `IImmichClient` and `AlbumId`. Keep the rest of its behavior.
  - `Services/Photos/PhotoSyncWorker.cs`: gate on `IPhotoSource.IsConfigured` instead of `ImmichOptions.IsConfigured`.
  - `Services/Photos/VideoPrefetcher.cs:58`: also require the resolved kind to be `Immich`.
  - `Program.cs:117-135`: register `IPhotoSource` by kind and pass it to `PhotoSyncer`.
  - `Endpoints/PhotoEndpoints.cs`: video falls through to 404 for the folder source (no change needed if `library.IsVideo` is false; check it).
  - `Photos/PhotoSyncerTests.cs`: point the existing tests at a `FakePhotoSource`. Keep `FakeImmichClient` for the `ImmichPhotoSource` tests.
- Test:
  - create `HomeSpeaker.Server2.Tests/Photos/FolderPhotoSourceTests.cs`, `Photos/PhotoSourceSelectorTests.cs` and `Photos/FakePhotoSource.cs`
  - extend `Photos/PhotoSyncerTests.cs` with one folder-backed integration test

**Interfaces:**
- Produces:
  - `IPhotoSource`, with:
    - `bool IsConfigured { get; }`
    - `Task<IReadOnlyList<ImmichAsset>> ListAssetsAsync(CancellationToken)`
    - `Task DownloadPreviewAsync(string assetId, Stream destination, CancellationToken)`
    - `Task<IReadOnlyList<ImmichFace>> GetFacesAsync(string assetId, CancellationToken)`
  - `ImmichAsset` keeps its name, to limit churn.
  - `FolderPhotoSource(string folder)`. `IsConfigured` is true when the folder exists. `ListAssetsAsync` rebuilds the internal id→path map. `DownloadPreviewAsync` throws `FileNotFoundException` for an unknown id.
  - `PhotoSourceSelector.Resolve(IConfiguration, ImmichOptions) -> PhotoSourceKind`

- [ ] **Step 1: Write failing tests:**
  - `List_FindsJpgAndJpegRecursively_CaseInsensitive`
  - `List_IgnoresPngHeicAndDotfiles`
  - `List_SpacesAndUnicodeNames_Work`
  - `AssetId_IsStableAcrossInstances_AndValidGuid`
  - `AssetId_DiffersForSameNameInDifferentFolders`
  - `List_MissingFolder_ReturnsEmpty_AndIsConfiguredFalse`
  - `DownloadPreview_CopiesBytes`
  - `DownloadPreview_UnknownId_Throws`
  - selector:
    - `Resolve_ImmichConfigured_Immich`
    - `Resolve_NothingConfigured_Folder`
    - `Resolve_ExplicitFolder_WinsOverImmich`
  - `Sync_FolderSource_CachesPhotosAndLibraryCountsThem`
- [ ] **Step 2: Run** `dotnet test HomeSpeaker.Server2.Tests --filter Photos`. Expected: the new tests FAIL, and the existing ones fail to compile until the refactor.
- [ ] **Step 3: Implement.**
- [ ] **Step 4: Verify** that the full suite passes. Locally, with no Immich config, drop 3 jpgs into `HomeSpeakerMedia/photos`, `POST /api/photos/sync`, and `/api/features` should report `photosEnabled: true`.

### Task 7: Settings page and settings API

**Sections and keys** (`SettingKeys` constants):
- **Location:** `Forecast:Latitude`, `Forecast:Longitude`. The page has a "Find my town" search that calls `GET https://geocoding-api.open-meteo.com/v1/search?name={q}&count=5`.
- **Photos:**
  - `Photos:Source`
  - `Immich:BaseUrl`, `Immich:ApiKey` (masked, write-only), `Immich:AlbumId`. A "Load albums" button calls Immich `GET {base}/api/albums` with the typed URL and key.
  - `Immich:SlideSeconds`
  - `Ambient:IdleSeconds`
- **General:** `Units:Temperature` (°F/°C)
- **Features:**
  - `NIGHTSCOUT_URL`
  - Temperature (Govee):
    - `Temperature:ApiBaseUrl` (a checkbox that sets it to `https://openapi.api.govee.com` or `""`) and `Temperature:ApiKey` (masked)
    - **Device picker** (`Components/Settings/TemperatureRoomPicker.razor`):
      - Once a key is saved, it lists the Govee sensors from `ITemperatureService.GetDevicesAsync`.
      - Each row shows the Govee device name, a room-name text box (blank means hidden), an "Outside" radio, and up/down buttons for card order.
      - If the current `Temperature:Rooms` contains rules the picker can't represent (more than one pattern per room, or patterns that don't equal a device name), show a notice: "Rooms are set with matching rules; editing here replaces them". The picker is still pre-filled by running the current rules through `TemperatureDeviceMatcher`.
      - Saving writes `Temperature:Rooms` (`Room: <exact device name>` per row, in order) and `Temperature:OutsideRoom`.
      - When `Temperature:Rooms` is env-overridden, the picker is read-only.
    - Window advice, shown only when an Outside room is chosen:
      - `Temperature:WindowAdvice` (switch)
      - `Temperature:IndoorRoom` (dropdown of the named rooms)
      - `Temperature:WindowMinOutside` and `TemperatureThreshold`, shown in the chosen unit and saved via `TemperatureDisplay.ToFahrenheit` (the threshold via the delta conversion)
  - `Features:Anchors` (checkbox)
  - `AI:OpenAI:ApiKey` (masked), `AI:OpenAI:ChatModel`
- **Admin PIN:** set, change or clear `Admin:PinHash`. Setting it requires typing it twice. Clearing it requires the current session to be unlocked.
- **Restart:** "Restart HomeSpeaker" calls `IHostApplicationLifetime.StopApplication()`; Docker `restart: unless-stopped` brings it back. After saving, show "Saved. Some changes take effect after a restart." Options are bound once at startup (`IOptions<ImmichOptions>`, the chat client, module mapping).

**Files:**
- Create:
  - `Settings/SettingKeys.cs`
  - `Settings/SettingsForm.cs`
  - `Settings/SettingField.cs`
  - `Services/LocationSearchService.cs`
  - `Services/Photos/ImmichAlbumBrowser.cs`
  - `Services/Photos/ImmichAlbum.cs`
  - `Endpoints/SettingsEndpoints.cs` (`MapSettingsApi`, all `RequireAuthorization("Admin")`):
    - `GET /api/settings` returns the field list, with masked values shown as `"••••"` when set
    - `PUT /api/settings` takes `Dictionary<string,string?>`
    - `POST /api/settings/restart`
  - `Pages/Admin/Settings.razor` (`@page "/settings"`, `[Authorize(Policy="Admin")]`, InteractiveServer)
  - section components `Components/Settings/LocationSettings.razor`, `PhotoSettings.razor`, `FeatureSettings.razor`, `PinSettings.razor` (reused by Task 8)
- Modify:
  - `Program.cs`: register the services, the HttpClients and `app.MapSettingsApi()`
  - `Components/Glass/AppMenu.razor`, `Components/Layout/NavMenu.razor`: add a "Settings" item (gear icon)
- Test:
  - `Settings/SettingsFormTests.cs`
  - `LocationSearchServiceTests.cs` and `Photos/ImmichAlbumBrowserTests.cs` (both with `FakeHttpMessageHandler`)
  - `Settings/SettingsEndpointTests.cs`

**Interfaces:**
- Consumes: `ISettingsWriter.Save` and `SqliteSettingsStore.LoadAll` (Task 4), and `AdminPin.Hash` (Task 5).
- Produces:
  - `SettingField(string Key, string Label, string? Value, bool IsSecret, bool IsOverridden)`.
    - `IsOverridden` means the effective `IConfiguration` value differs from the database value and isn't the appsettings default. In practice: an env var or command line sets it. Detect this by walking `((IConfigurationRoot)config).Providers` in reverse: the first provider that has the key, if it's not the database or JSON provider, means overridden.
  - `SettingsForm.Read(IConfiguration, IReadOnlyDictionary<string,string> dbValues) -> IReadOnlyList<SettingField>`
  - `SettingsForm.Changes(IReadOnlyList<SettingField> current, IReadOnlyDictionary<string,string?> submitted) -> IReadOnlyDictionary<string,string?>`. It drops overridden keys, unknown keys, and secrets submitted as `"••••"` (unchanged). An empty string deletes the key.
  - `LocationSearchService.SearchAsync(string query, CancellationToken) -> IReadOnlyList<LocationMatch(string Name, string? Region, string? Country, double Latitude, double Longitude)>`
  - `ImmichAlbumBrowser.GetAlbumsAsync(string baseUrl, string apiKey, CancellationToken) -> IReadOnlyList<ImmichAlbum(string Id, string Name, int AssetCount)>` (it sends the `x-api-key` header)
  - `TemperatureRoomPicker` logic lives in the testable `Settings/RoomAssignments.cs`:
    - `RoomAssignments.FromRules(IReadOnlyList<string> deviceNames, string? roomsSetting, string? outsideRoom) -> IReadOnlyList<RoomAssignment(string DeviceName, string? Room, bool IsOutside)>`, ordered by room order and then unassigned devices
    - `RoomAssignments.ToSettings(IReadOnlyList<RoomAssignment>) -> (string Rooms, string? OutsideRoom)`
    - `RoomAssignments.IsRepresentable(string? roomsSetting, IReadOnlyList<string> deviceNames) -> bool`

- [ ] **Step 1: Write failing tests:**
  - `Read_EnvOverriddenKey_IsOverriddenAndReadOnly`
  - `Read_SecretValue_IsMasked`
  - `Changes_DropsOverriddenAndUnknownKeys`
  - `Changes_MaskedSecretUnchanged_NotWritten`
  - `Changes_EmptyString_DeletesKey`
  - `Search_ParsesOpenMeteoResults` (with a canned JSON sample of 2 results)
  - `Search_BlankQuery_ReturnsEmptyWithoutHttpCall`
  - `GetAlbums_SendsApiKeyAndParsesAlbums`
  - `GetAlbums_Unauthorized_ThrowsHttpRequestException`
  - `PutSettings_WithPinNoCookie_Returns401or302`
  - `PutSettings_NoPin_SavesAndGetReflectsValue`
  - `RoomAssignments_FromOwnerRules_MapsDevicesToRooms` (devices "Outside Sensor", "Girl room", "Emma's room", "Boy room", "Mom and Dad" → the rooms Outside, Girls, Emma, Boys, Downstairs)
  - `RoomAssignments_ToSettings_WritesExactDeviceNamesInOrder`
  - `RoomAssignments_BlankRoom_IsOmitted`
  - `RoomAssignments_IsRepresentable_FalseForMultiPatternRooms`
  - `RoomAssignments_RoundTrip_FromRulesToSettingsToRules`
- [ ] **Step 2: Run** `dotnet test HomeSpeaker.Server2.Tests --filter "SettingsForm|LocationSearch|ImmichAlbumBrowser|SettingsEndpoint"`. Expected: FAIL.
- [ ] **Step 3: Implement** the services, endpoints and page. The page follows the glass styling (`Components/Glass`, `wwwroot/css/glass.css`), and every section stacks to one column at 1024×600.
- [ ] **Step 4: Verify** that the full suite passes. Locally: set a location via search, save, restart, and the Home "Next hours" card appears. Load albums against the owner's Immich (`https://immich.boston-cloud.ts.net`, with the user's key, only if they provide it; otherwise skip and say so).

### Task 8: First-run setup and HTTP by default

Setup is needed when `Setup:Completed` isn't `"true"` **and** `FeatureFlags.ForecastEnabled` is false. That makes any already-configured install (the owner's Pis, once Task 13 sets the location) skip it automatically.

The wizard at `/setup` has four steps, reusing the Task 7 section components:
1. Welcome and location
2. Photos (default folder: "Drop .jpg files into the `photos` folder of your music share," or connect Immich)
3. Optional features
4. Admin PIN (skippable)

Finishing saves `Setup:Completed=true` and goes to `/`. When a PIN is already set, `/setup` requires the `Admin` policy.

**Files:**
- Create: `Settings/SetupState.cs`, `Pages/Admin/Setup.razor`
- Modify:
  - `Pages/Index.razor`: in `OnInitialized`, if `SetupState.IsNeeded(config)`, call `NavigateTo("/setup")`.
  - `Components/Glass/AppMenu.razor`: no change. Setup is reachable from Settings via a "Run setup again" link.
  - `HomeSpeaker.Server2/Dockerfile`: no change to ports. The image already exposes 80, and the HEALTHCHECK uses HTTP.
  - `HomeSpeaker.Server2/Properties/launchSettings.json`: no change.
- Test: `Settings/SetupStateTests.cs`

**Interfaces:**
- Produces: `SetupState.IsNeeded(IConfiguration) -> bool`, `SetupState.CompletedKey = "Setup:Completed"`

- [ ] **Step 1: Write failing tests:** `IsNeeded_FreshConfig_True`, `IsNeeded_CompletedTrue_False`, `IsNeeded_LocationConfigured_False`, `IsNeeded_CompletedFalseString_True`.
- [ ] **Step 2: Run** `dotnet test HomeSpeaker.Server2.Tests --filter SetupStateTests`. Expected: FAIL.
- [ ] **Step 3: Implement.**
- [ ] **Step 4: Verify** that the full suite passes. Locally, with an empty temp `MediaFolder` and a fresh DB: `/` redirects to `/setup`, the four steps complete, and `/` then shows Home. Take a screenshot at 1024×600 with the run-homespeaker-server skill.

---

## Phase 3 — Packaging

### Task 9: Generic compose with profiles, plus an HTTPS overlay

`docker-compose.yml` becomes the generic file. Use variables with defaults:
- `MUSIC_DIR` (default `./music`)
- `TZ` (default `Etc/UTC`)
- `HTTP_PORT` (default `80`)
- `PUID` (default `1000`, used for the Pulse path `/run/user/${PUID:-1000}/pulse`)
- `AIRPLAY_NAME` (default `HomeSpeaker`)
- `HOMESPEAKER_IMAGE` (default `ghcr.io/snow-jallen/homespeaker:latest`)
- `OTEL_EXPORTER_OTLP_ENDPOINT` (default empty)

`ASPNETCORE_URLS=http://+:80`. Only port `${HTTP_PORT}:80`. Keep `env_file`, but make it optional: `env_file: [{ path: ./homespeaker.env, required: false }]` (Compose ≥ 2.24). AirPlay goes under `profiles: [airplay]`.

New `telemetry` profile: an `aspire-dashboard` service (`mcr.microsoft.com/dotnet/aspire-dashboard:latest`, ports `18888:18888`, `DOTNET_DASHBOARD_UNSECURED_ALLOW_ANONYMOUS=true`). The README says to set `OTEL_EXPORTER_OTLP_ENDPOINT=http://aspire-dashboard:18889` in `.env` when using it.

`docker-compose.https.yml` is the overlay. It adds port `443:443` and the volume `${CERT_DIR}:/certs`, sets `ASPNETCORE_URLS=https://+:443;http://+:80` (this also fixes the `+443` typo) and `ASPNETCORE_Kestrel__Certificates__Default__Path=/certs/certificate.pfx`.

**Files:**
- Modify: `docker-compose.yml`
- Create: `docker-compose.https.yml`, `.env.example` (every variable above, commented)
- Modify `HomeSpeaker.Server2/Dockerfile`:
  - `ARG BASE_IMAGE=ghcr.io/snow-jallen/homespeaker-base:latest` before `FROM ${BASE_IMAGE}`
  - build stage `FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:10.0`
  - `ARG TARGETARCH`
  - `dotnet publish -a $TARGETARCH` (cross-compile, so multi-arch builds don't run the SDK under QEMU)

- [ ] **Step 1: Validate the generic file.** In a temp dir with no `.env`, run `docker compose -f docker-compose.yml config`. Expected: it renders, with port `80:80`, `MediaFolder=/music`, a `./music` volume and no airplay service.
- [ ] **Step 2: Validate the owner-style overlay.** Write `.env` with `MUSIC_DIR=/home/piuser/music`, `CERT_DIR=/home/piuser/cert`, `TZ=America/Denver` and `COMPOSE_PROFILES=airplay`, then run `docker compose -f docker-compose.yml -f docker-compose.https.yml config`. Expected: ports 80 and 443, the certs volume, the airplay service, and `ASPNETCORE_URLS=https://+:443;http://+:80`.
- [ ] **Step 3: Check the Dockerfile build.** Run `docker buildx build --platform linux/amd64 -f HomeSpeaker.Server2/Dockerfile --build-arg BASE_IMAGE=ghcr.io/snow-jallen/homespeaker-base:latest .`. The base is arm64-only until Task 10 runs, so expect `--platform linux/arm64` to work if Docker Desktop has QEMU; otherwise just confirm the sdk stage reaches `dotnet publish -a`. Report which.

### Task 10: Multi-arch versioned images

**Files:**
- Modify `.github/workflows/build-base-image.yml`: `platforms: linux/arm64,linux/amd64` with `docker/setup-qemu-action@v3`, and `runs-on: ubuntu-24.04`. Tag `ghcr.io/${{ github.repository_owner }}/homespeaker-base:latest`.
- Modify `.github/workflows/build-and-push.yml`:
  - Triggers: push to `master`, push of tags matching `20*.*.*`, and `workflow_dispatch`.
  - `runs-on: ubuntu-24.04`, `platforms: linux/arm64,linux/amd64`, with QEMU set up (only the final `COPY` stage runs emulated).
  - `build-args: BASE_IMAGE=ghcr.io/${{ github.repository_owner }}/homespeaker-base:latest`
  - Tags:
    - `latest` on master
    - `type=ref,event=tag` (for example `2026.10.22`)
    - `type=raw,value={{date 'YYYY.M.D'}}-{{sha}}` on master, so every master build is pinnable
  - Add the label `org.opencontainers.image.source`.
- Note in Task 13: the base image must be rebuilt once (dispatch `build-base-image.yml`) before the first multi-arch app build.

- [ ] **Step 1: Lint.** Run `npx --yes actionlint` if Node is available (it is). Expected: no errors in the two workflows.
- [ ] **Step 2: Review** the diff against `deploy.yml`'s `workflow_run` trigger name ("Build and Push", or whatever the `name:` is) so deploy still chains.

### Task 11: Owner deploy workflows driven by repo variables

The owner's specifics move into repo variables, so forks run nothing:
- `DEPLOY_TARGETS`: a JSON array, for example `[{"runner":"kitchen","host":"kitchen","airplay_name":"Kitchen HomeSpeaker"},{"runner":"upstairs","host":"rpi42","airplay_name":"Upstairs HomeSpeaker"}]`
- `TAILNET_DOMAIN`: `boston-cloud.ts.net`
- `DEPLOY_TZ`: `America/Denver`
- `DEPLOY_USER`: `piuser`

Each job gets `if: vars.DEPLOY_TARGETS != ''`, and the matrix is `include: ${{ fromJSON(vars.DEPLOY_TARGETS) }}`.

**Files:** modify `.github/workflows/deploy.yml`, `enable-kiosk.yml`, `refresh-browser.yml` and `diagnose.yml`.
- Replace `boston-cloud.ts.net` with `${{ vars.TAILNET_DOMAIN }}`, `piuser` and `/home/piuser` with `${{ vars.DEPLOY_USER }}`, and the hard-coded runners and hosts with the matrix.
- The `.env` that deploy.yml writes gains `MUSIC_DIR=/home/$USER/music`, `CERT_DIR=/home/$USER/cert`, `TZ=${{ vars.DEPLOY_TZ }}`, `COMPOSE_PROFILES=airplay` and `PUID=1000`.
- Switch compose commands to `docker compose -f docker-compose.yml -f docker-compose.https.yml` (the v2 plugin). Check the runner has it; if it doesn't, keep `docker-compose` and flag it in Task 13.
- Add `env_file` in the overlay: `/home/${DEPLOY_USER}/homespeaker-secrets.env`, required. This keeps the owner's secrets path.

- [ ] **Step 1:** Lint with `npx --yes actionlint`. Expected: clean.
- [ ] **Step 2:** `grep -rn "boston-cloud\|piuser\|kitchen\|rpi42" .github/workflows docker-compose*.yml` returns nothing.

### Task 12: Install script with Avahi advertisement, kiosk and nightly updates

`install/install.sh` targets Raspberry Pi OS Bookworm (also any Debian/Ubuntu with systemd). It's idempotent, runs with `sudo`, and is fetched with `curl -fsSL https://raw.githubusercontent.com/snow-jallen/homespeaker/master/install/install.sh | sudo bash`.

Steps:
1. Install Docker via `get.docker.com` if `docker` is missing, and add `$SUDO_USER` to the `docker` and `audio` groups.
2. Create `/opt/homespeaker` and download `docker-compose.yml` from the same ref (`HOMESPEAKER_REF`, default `master`).
3. Write `.env` (only if it doesn't exist):
   - `MUSIC_DIR=/home/$SUDO_USER/music`, which it also creates and `chown`s
   - `TZ` from `timedatectl show -p Timezone --value`
   - `PUID=$(id -u $SUDO_USER)`
   - `AIRPLAY_NAME="$(hostname) HomeSpeaker"`
   - `COMPOSE_PROFILES=airplay` if the user answers y (default y; `--yes` accepts all defaults)
4. Write `/etc/avahi/services/homespeaker.service`, advertising `_homespeaker._tcp` on `HTTP_PORT` with TXT records `host=$(hostname).local`, `port=<HTTP_PORT>`, `scheme=http` and `path=/`. Install `avahi-daemon` if it's missing, then reload it.
5. Optional kiosk (`--kiosk`, or asked when a desktop session exists): write `/home/$SUDO_USER/.config/labwc/autostart` (Bookworm Wayland) with `chromium-browser --kiosk --noerrdialogs --disable-infobars http://localhost/?kiosk=1 &`. Back up any existing file to `.bak`.
6. Install `homespeaker-update.service` and `homespeaker-update.timer` (daily at 03:30, `RandomizedDelaySec=30m`), running `docker compose pull && docker compose up -d` in `/opt/homespeaker`, then `docker image prune -f`.
7. Run `docker compose up -d`, wait up to 120 s for `curl -fsS http://localhost/alive`, and print `Open http://$(hostname).local to finish setup.`

**Files:**
- Create:
  - `install/install.sh`
  - `install/homespeaker.service.xml` (an Avahi template with `@HTTP_PORT@`/`@HOST@` placeholders, filled with `sed`)
  - `install/homespeaker-update.service`
  - `install/homespeaker-update.timer`
  - `install/uninstall.sh` (stops the stack, removes the units and Avahi file, and leaves the music folder)

- [ ] **Step 1:** `bash -n install/install.sh install/uninstall.sh`. Expected: no output.
- [ ] **Step 2:** Run `docker run --rm -v "$PWD/install:/i" koalaman/shellcheck:stable /i/install.sh /i/uninstall.sh`. Expected: no errors (warnings acceptable only with an inline `# shellcheck disable=` and a reason).
- [ ] **Step 3:** Run it in a container. From `docker run --rm -it debian:bookworm`, run the script with `--yes --no-start --dry-run`. The script supports `--dry-run`, which prints the actions without touching systemd or Docker. Expected: it prints the files it would write, and the Avahi XML contains `_homespeaker._tcp`.

### Task 13: Docs, license and the owner migration checklist

**Files:**
- Rewrite `readme.md` for a new household:
  - what it is
  - hardware (Pi 4/5 with 2 GB or more; USB DAC or 3.5 mm out; optional 1024×600 touchscreen)
  - the one-line install
  - first-run setup
  - adding music and photos (the `music` and `music/photos` folders, or a network share)
  - optional features (Nightscout, Govee, Anchors, AI, Immich)
  - AirPlay
  - updates (the nightly timer; pin a tag with `HOMESPEAKER_IMAGE`)
  - HTTPS (optional overlay)
  - forgetting the PIN: put `Admin__PinHash=` in `/opt/homespeaker/homespeaker.env` and run `docker compose up -d`
  - telemetry profile
  - developer quick start (moved from the old README)
- Rewrite `DEPLOYMENT.md` to describe the owner/maintainer pipeline (repo variables, self-hosted runners, the HTTPS overlay, APNs) and keep the APNs section.
- Update `CLAUDE.md`: the settings provider and precedence, admin PIN, feature modules, photo sources, compose files, install script and repo variables. Drop the stale-solution line.
- Update `homespeaker-secrets.env.example` with the new keys.
- Create `LICENSE`: MIT, `Copyright (c) 2026 Jonathan Allen` (the user chose MIT).
- Create `docs/owner-migration.md`. It is the checklist for the user and is not run by agents:
  1. Set the repo variables (`gh variable set DEPLOY_TARGETS --body '<json>'`, plus `TAILNET_DOMAIN`, `DEPLOY_TZ` and `DEPLOY_USER`).
  2. On each Pi, append to `/home/piuser/homespeaker-secrets.env`:
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
  3. Dispatch `build-base-image.yml` once.
  4. Check that the Pis' runners have `docker compose` v2.
  5. After the deploy, confirm the parity list in Review Focus #1 on both Pis via `curl -sk https://<host>.boston-cloud.ts.net/api/features`.

- [ ] **Step 1:** Write the docs and LICENSE.
- [ ] **Step 2:** Check that every config key named in the docs exists in the code: `grep` each key from `SettingKeys.cs` plus `.env.example` in the README.

---

## Phase 4 — iOS

### Task 14: Bonjour discovery, push only when the server supports it, and generic rooms

**Files** (all under `HomeSpeakerMobile/`):
- Modify `Shared/Models.swift`:
  - `Features` gains `forecastEnabled: Bool?`, `anchorsEnabled: Bool?`, `pushEnabled: Bool?` and `temperatureUnit: String?`.
  - The temperature views in `MoreView` convert with the server's `temperatureUnit` (`"C"` gives `(f - 32) * 5 / 9`; nil or `"F"` shows as-is).
  - `TemperatureStatus` drops the five family fields and gains `indoorRoomName: String?`, `indoorTemperature: Double?` and `rooms: [RoomReading]?`.
  - New struct `RoomReading: Codable { name: String; temperature: Double?; batteryPercent: Double? }`. Match the C# `RoomReading` JSON names; check `HomeSpeaker.Shared/Temperature/RoomReading.cs`.
- Modify `iOS/Views/MoreView.swift:147-170`: render `rooms` (outside first, then the others) instead of fixed labels.
- Modify `iOS/PushNotificationStore.swift`: on refresh, fetch features. If `pushEnabled == false`, set `serverSupportsPush = false` and skip the PUT. `nil` (an old server) keeps today's 404-based behavior.
- Modify `iOS/LiveActivity/SpeakerActivityController.swift:65`:
  - Request with `pushType: serverPushEnabled ? .token : nil`, and skip token registration when it's nil.
  - Let the controller accept a `serverPushEnabled: Bool`, set from the features fetched in `NowPlayingView` (it already calls `getFeatures` at :55).
- Create `iOS/Discovery/SpeakerBrowser.swift`:
  - `@MainActor final class SpeakerBrowser: ObservableObject`, with `@Published var found: [DiscoveredSpeaker]` and `start()`/`stop()`.
  - It uses `NWBrowser(for: .bonjourWithTXTRecord(type: "_homespeaker._tcp", domain: nil), using: .tcp)`.
  - For each result it reads TXT `host`, `port` and `scheme`. If `host` is missing, it falls back to `<service name>.local`. If `port` is missing, it uses 80.
- Create `iOS/Discovery/DiscoveredSpeaker.swift`: `struct DiscoveredSpeaker: Identifiable, Hashable { id: String; name: String; host: String; port: Int; scheme: String }`.
- Modify `iOS/Views/ServerListView.swift`:
  - In the empty state and at the top of `AddEditServerView`, add a "Nearby speakers" section listing `SpeakerBrowser.found`.
  - Tapping one prefills Name (the service name with " HomeSpeaker" stripped), Host and Port. Speakers already saved (matching host and port) are hidden.
  - The browser starts `onAppear` and stops `onDisappear`.
- Modify `Shared/Models.swift`: the `ServerConnection` default port goes from 5000 to 80.
- Modify `iOS/Resources/Info.plist`: add `NSBonjourServices` = [`_homespeaker._tcp`]. `NSLocalNetworkUsageDescription` already exists (:38); reword it to mention finding speakers.
- Modify `project.yml`: no change needed (folder globs). Modify `HomeSpeaker.xcodeproj/project.pbxproj` for the two new files, iOS app target only:
  - a `PBXFileReference` each
  - a new `Discovery` `PBXGroup` under the `iOS` group
  - a `PBXBuildFile` each
  - entries in the Sources phase `7984D897AA7DD367B5D1F218`
  - new 24-hex IDs that don't collide (grep to confirm they're unique)

- [ ] **Step 1:** Make the edits. There are no Swift tests in the repo, so verification is by review plus Xcode Cloud.
- [ ] **Step 2:** Check that the pbxproj is well-formed: each new ID appears exactly where expected (`grep -c` per ID: file ref 2, build file 2), and brace balance is unchanged (`python -I -c` count of `{` vs `}`).
- [ ] **Step 3:** `grep -rn "youngerGirls\|boysRoom\|greenhouseTemperature" HomeSpeakerMobile` returns nothing.
- [ ] **Step 4:** Commit on `feature/shareable`, but do **not** merge or push. Pushing master also deploys the server to the Pis, so the owner must finish Task 13's checklist first. Hand back to the user with the iOS changes listed and the statement that the Swift code was not compiled locally.
