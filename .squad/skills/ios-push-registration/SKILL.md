# iOS Push Registration

## Use When
- A SwiftUI iOS app needs APNs registration tied to a backend installation record.
- The backend exposes device registration, with or without extra per-feature toggles.

## Pattern
1. Create a single `@Observable` store for push state instead of spreading permission, token, and subscription logic across views.
2. Bind a small `UIApplicationDelegateAdaptor` to hand APNs token callbacks back into that store.
3. Persist an installation ID locally so the same app install can re-register with the selected server after relaunch.
4. Keep backend sync server-scoped: when the active connection changes, refresh status for that server and reuse the same installation ID/device token.
5. Prefer a registration-first contract:
   - installation registration (`deviceToken`, `platform`, optional device metadata)
   - put stable alert-preference booleans (for example `temperatureAlertsEnabled`, `bloodSugarAlertsEnabled`) on that installation resource when possible
   - only add per-feature subscription calls if the backend truly requires a separate lifecycle
   - if the server can own alert categories itself, keep the mobile client push-only and avoid module-shaped UI/copy
6. Give users a dedicated mobile status surface instead of burying toggle state in an unrelated settings row.
7. Treat missing push endpoints as an unsupported-server state, not a fatal error, so reverted or older servers still feel graceful in the app.
8. Add the push entitlement and keep the APNs environment configurable by build config (`development` vs `production`).
9. Back the server contract with automated tests: use `WebApplicationFactory` plus a fake `IPushNotificationSender` to prove registration, direct alert-flag persistence, unregister behavior, and repeat-suppressed alert delivery without needing live APNs.
10. QA the feature as two concerns only:
   - installation registration/status lifecycle
   - delivery on the existing alert-producing paths

   If tests start talking about module catalogs or per-feature subresources, re-check whether the backend actually needs that extra abstraction.

## HomeSpeaker Example
- App hook-up: `HomeSpeakerMobile/iOS/HomeSpeakerApp.swift`
- Push state owner: `HomeSpeakerMobile/iOS/PushNotificationStore.swift`
- UI surface: `HomeSpeakerMobile/iOS/Views/PushNotificationsView.swift`
- API contract shim: `HomeSpeakerMobile/Shared/APIClient.swift`, `HomeSpeakerMobile/Shared/Models.swift`
- Shared DTOs: `HomeSpeaker.Shared/WindowMonitoring/*Push*.cs`

## QA Checklist
- Register a device and assert the status resource echoes `temperatureAlertsEnabled` / `bloodSugarAlertsEnabled`.
- Re-register the same installation and prove preference changes overwrite prior values.
- Unregister and verify the installation becomes inactive.
- Trigger the existing temperature and blood-sugar services with a fake sender and prove:
  - only opted-in devices receive notifications
  - duplicate polls within the repeat window do not resend
  - a clear/in-range state resets dedupe so a later alert can fire again
