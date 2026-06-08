# Push Alert Dedupe Pattern

Use this when adding server-side push notifications to an existing status API without introducing a second monitoring subsystem.

## Rule
Keep the original status-producing services intact and let them call a push service only after a fresh upstream fetch completes.

## Persist separately
- **Device registrations** in one table (`PushNotificationDevices`)
- **Alert dedupe state** in another table (`PushNotificationAlertStates`)

Do not overload the status payload cache as your notification state store.

## Notification trigger model
1. Fetch the real upstream status.
2. Compute a compact alert state key (for example `keep-closed`, `open-windows`, `low`, `high`, `stale`).
3. If the state is clear/normal, clear the persisted alert state.
4. If the state is active and unchanged inside the repeat window, do nothing.
5. If the state changed or aged out, send notifications and only advance the stored alert state when at least one send succeeds.

## Why this works
- Preserves existing API behavior.
- Prevents poll-driven duplicate pushes.
- Allows future devices to register without needing a separate alert engine.
- Keeps transient push-provider failures from corrupting alert state.
