---
name: "blazor-ssr-migration"
description: "How to collapse a hosted Blazor WebAssembly app into a server-hosted Blazor SSR/Interactive Server app without keeping redundant transport layers."
domain: "architecture"
confidence: "high"
source: "manual"
---

## Context
Use this when a solution has a Blazor WebAssembly client talking to an ASP.NET Core server that already owns the real application services. It applies when startup time matters more than offline execution and the UI does not need a separate deployment unit.

## Patterns
- Prefer **one server-hosted Blazor Web App** over a hosted WASM + API split when the browser is only a thin shell over server-owned state and workflows.
- Default product routes to **Interactive Server with prerendering** if they use event handlers, timers, JS interop, drag-drop, forms, or live status updates.
- Reserve **plain SSR** for routes that are effectively static, iframe wrappers, or simple redirects.
- Replace client transport wrappers with an **in-process UI facade** over existing server services instead of bouncing through REST or gRPC just for ceremony.
- Keep existing external APIs for real consumers; remove internal-only transports after the UI no longer depends on them.
- Audit client-only assumptions: `WebAssemblyHostBuilder`, `IWebAssemblyHostEnvironment`, gRPC channels, base-address config, and browser-only startup hooks usually need to disappear or be reworked.
- Move static assets and JS modules into the server host’s `wwwroot` so existing interop code can keep working under Interactive Server.

## Examples
- `HomeSpeaker.WebAssembly\Services\HomeSpeakerService.cs` is a transport wrapper that should become direct server DI.
- `HomeSpeaker.Server2\Program.cs` already hosts static files and APIs, so it is the natural place to host Razor components too.
- `HomeSpeaker.WebAssembly\Components\Layout\MainLayout.razor` and `Pages\Index.razor` are Interactive Server candidates because they depend on JS interop, timers, and player controls.
- `HomeSpeaker.WebAssembly\Pages\Health\NightScout.razor` and `Pages\Admin\AspireDashboard.razor` are plain SSR candidates because they mostly render iframe shells.

## Anti-Patterns
- Keeping gRPC or REST between the UI and the same server process after moving to server-side rendering.
- Treating every route as interactive when some are just static wrappers.
- Recreating a second service layer in the UI project instead of reusing the server’s actual services.
- Dragging unused client plumbing into the new server UI because it already exists.
