# Ambient Shell & Immich Slideshow Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Turn each HomeSpeaker Pi into an ambient display: when idle it shows an Immich photo slideshow with clock, now-playing, glucose and temperature overlays, and when touched it shows a glass-styled control screen with a reserved home-automation slot.

**Architecture:** A `PhotoSyncWorker` mirrors one Immich album into `{MediaFolder}/.photo-cache` (preview JPEGs plus a JSON manifest). `PhotoLibrary` serves that cache through `/api/photos/*`. Two small JS modules handle the idle state machine (`idleController.js`) and the crossfading slideshow (`ambientSlideshow.js`). Blazor renders the shell, the overlays and the Home page from one scoped `DashboardState`. Home automation plugs in through `IHomeControl`, which defaults to `NullHomeControl`.

**Tech Stack:** .NET 10, ASP.NET Core minimal APIs, Blazor Server (global InteractiveServer), xUnit (existing `HomeSpeaker.Server2.Tests`), vanilla JS, plain CSS.

**Spec:** `docs/superpowers/specs/2026-10-13-ambient-shell-design.md` (mockups: https://claude.ai/artifact/Dks5LXrMiysouhJWSnVkp8, design "A · Glass").

## Global Constraints

- **Do not run `git commit`, `git add`, or any other git write command.** The user does all git operations. Every task ends with "leave changes uncommitted".
- Debug builds treat warnings as errors (`TreatWarningsAsErrors=true`, Meziantou + Workleap analyzers). Fix warnings in code; never edit the csproj `NoWarn` lists.
- Naming: private fields `camelCase` with **no** underscore prefix; private methods `camelCase`; public members PascalCase. Use `string.Equals(a, b, StringComparison.Ordinal)` rather than `==` for strings, and pass `CultureInfo.InvariantCulture` when formatting or parsing numbers and dates.
- New glass UI sizes text and spacing in `px`, never `rem`. `app.css` sets `html { font-size: 24px }` at exactly 1024×600 for legacy pages.
- Target screen: 1024×600 touch. It must also work at ~390px phone width.
- Immich API (v3.3, verified against its OpenAPI spec). Header `x-api-key`. `POST api/search/metadata`, `GET api/assets/{id}/thumbnail?size=preview`, `GET api/faces?id={id}`.
- Config keys: `Immich:BaseUrl`, `Immich:ApiKey`, `Immich:AlbumId`, `Immich:SyncIntervalMinutes` (default 15), `Immich:SlideSeconds` (default 30), `Ambient:IdleSeconds` (default 120). Night mode idles after a fixed 60s.
- Cache directory: `Path.Combine(config["MediaFolder"], ".photo-cache")`.
- Quiet hours come from `QuietHours.IsQuietTime(TimeProvider)` (22:00–07:00).
- Run tests with: `dotnet test HomeSpeaker.Server2.Tests` (from repo root). Build with: `dotnet build HomeSpeaker.sln`.
- Running the app locally: use the `run-homespeaker-server` skill, or `dotnet run --project HomeSpeaker.Server2 --launch-profile https` (https://localhost:7238).

## Review Focus

1. **A photo with no location EXIF follows one that had a place.** Expect the caption's place line to go blank, not keep the previous photo's place. Pinned by `PhotoCaptionTests.Place_WithNoLocation_ReturnsNull` (Task 3); JS clears the text with `p.place || ''` (Task 9).
2. **The album is emptied, or the only photo is hidden.** Expect `/api/photos/next` to return 204, and the ambient screen to fall back to a dark background with overlays rather than freezing on a stale photo. Pinned by `PhotoLibraryTests.Next_WhenOnlyPhotoHidden_ReturnsNull` (Task 4) and `PhotoSyncerTests.Sync_WhenAlbumEmptied_RemovesEverything` (Task 5); JS handles 204 (Task 9).
3. **A crafted id on `/api/photos/{id}.jpg`, e.g. `..%2Fmanifest`.** Expect 404 and no file outside the cache served. Pinned by `PhotoCacheStoreTests.TryGetImagePath_RejectsNonGuidIds` (Task 2).
4. **A bad API key or Immich unreachable (401, DNS failure).** Expect the cache and manifest to be untouched and the slideshow to keep running. Pinned by `PhotoSyncerTests.Sync_WhenAlbumFetchFails_KeepsExistingCache` (Task 5).
5. **The localhost kiosk browser that someone opted out with `?kiosk=0`.** Expect it never to enter Ambient or Night. Pinned by `KioskTests.CookieZero_OverridesLocalhost` (Task 6).

## File Structure

**Create (server):**
- `HomeSpeaker.Server2/Services/Photos/ImmichOptions.cs`: config POCO.
- `HomeSpeaker.Server2/Services/Photos/ImmichClient.cs`: `IImmichClient`, `ImmichClient`, `ImmichAsset`, `ImmichFace`.
- `HomeSpeaker.Server2/Services/Photos/PhotoEntry.cs`: manifest records `PhotoEntry`, `FaceBox`.
- `HomeSpeaker.Server2/Services/Photos/PhotoCacheStore.cs`: disk I/O for images, manifest and hidden list.
- `HomeSpeaker.Server2/Services/Photos/PhotoCaption.cs`: place and date text.
- `HomeSpeaker.Server2/Services/Photos/OverlayLayoutChooser.cs`: face-aware default/mirrored choice.
- `HomeSpeaker.Server2/Services/Photos/PhotoCycle.cs`: shuffle without repeats.
- `HomeSpeaker.Server2/Services/Photos/PhotoLibrary.cs`: `PhotoLibrary` and the `NextPhoto` record.
- `HomeSpeaker.Server2/Services/Photos/PhotoSyncer.cs`: one sync pass.
- `HomeSpeaker.Server2/Services/Photos/PhotoSyncWorker.cs`: `PhotoSyncWorker` (hosted) and `PhotoSyncTrigger`.
- `HomeSpeaker.Server2/Endpoints/PhotoEndpoints.cs`: `MapPhotoApi()`.
- `HomeSpeaker.Server2/FeatureFlags.cs`: shared feature checks.
- `HomeSpeaker.Server2/Kiosk.cs`: kiosk detection and cookie override.
- `HomeSpeaker.Server2/Services/Dashboard/GlucoseTone.cs`: `GlucoseTone` enum and `GlucoseToneMap`.
- `HomeSpeaker.Server2/Services/Dashboard/NowPlayingInfo.cs`: display model built from `PlayerStatus`.
- `HomeSpeaker.Server2/Services/Dashboard/DashboardState.cs`: scoped live data.
- `HomeSpeaker.Server2/Services/Home/IHomeControl.cs`: interface, `HomeTile`, enums.
- `HomeSpeaker.Server2/Services/Home/NullHomeControl.cs`.

**Create (UI):**
- `HomeSpeaker.Server2/Components/Glass/Clock.razor`
- `HomeSpeaker.Server2/Components/Glass/AppMenu.razor`
- `HomeSpeaker.Server2/Components/Glass/AmbientOverlay.razor`
- `HomeSpeaker.Server2/Components/Glass/NightClock.razor`
- `HomeSpeaker.Server2/Components/Glass/HomeTileRow.razor`
- `HomeSpeaker.Server2/Pages/Health/Climate.razor`
- `HomeSpeaker.Server2/wwwroot/css/glass.css`
- `HomeSpeaker.Server2/wwwroot/js/idleController.js`
- `HomeSpeaker.Server2/wwwroot/js/ambientSlideshow.js`

**Create (tests, in `HomeSpeaker.Server2.Tests/`):**
- `Photos/FakeHttpMessageHandler.cs`, `Photos/FakeImmichClient.cs`, `Photos/TempDirectory.cs`, `Photos/FixedTimeProvider.cs`
- `Photos/ImmichClientTests.cs`, `Photos/PhotoCacheStoreTests.cs`, `Photos/PhotoCaptionTests.cs`, `Photos/OverlayLayoutChooserTests.cs`, `Photos/PhotoCycleTests.cs`, `Photos/PhotoLibraryTests.cs`, `Photos/PhotoSyncerTests.cs`
- `KioskTests.cs`, `Dashboard/GlucoseToneTests.cs`, `Dashboard/NowPlayingInfoTests.cs`

**Modify:**
- `HomeSpeaker.Server2/Program.cs`: DI registrations, kiosk middleware, `/api/features`, `MapPhotoApi()`.
- `HomeSpeaker.Server2/Components/App.razor`: fonts, `glass.css`, `data-kiosk`, scripts.
- `HomeSpeaker.Server2/Components/Layout/MainLayout.razor`: rewritten as the glass shell.
- `HomeSpeaker.Server2/Pages/Index.razor`: rewritten as the Home page.
- `HomeSpeaker.Server2/Components/_Imports.razor` and `HomeSpeaker.Server2/Pages/_Imports.razor`: new `@using` lines.
- `homespeaker-secrets.env.example` and `CLAUDE.md`.

**Delete:**
- `HomeSpeaker.Server2/wwwroot/js/sleepyTime.js`
- `HomeSpeaker.Server2/Components/Layout/MainLayout.razor.css`
- `HomeSpeaker.Server2/Pages/Index.razor.css`

---

### Task 1: Immich options and API client

**Files:**
- Create: `HomeSpeaker.Server2/Services/Photos/ImmichOptions.cs`
- Create: `HomeSpeaker.Server2/Services/Photos/ImmichClient.cs`
- Create: `HomeSpeaker.Server2.Tests/Photos/FakeHttpMessageHandler.cs`
- Test: `HomeSpeaker.Server2.Tests/Photos/ImmichClientTests.cs`

**Interfaces:**
- Consumes: nothing.
- Produces:
  - `ImmichOptions { string? BaseUrl; string? ApiKey; string? AlbumId; int SyncIntervalMinutes = 15; int SlideSeconds = 30; bool IsConfigured }`
  - `record ImmichAsset(string Id, DateTime TakenAt, string? City, string? State, string? Country)`
  - `record ImmichFace(double X1, double Y1, double X2, double Y2, int ImageWidth, int ImageHeight)`, in pixels
  - `interface IImmichClient` with:
    - `Task<IReadOnlyList<ImmichAsset>> GetAlbumImagesAsync(string albumId, CancellationToken cancellationToken)`
    - `Task DownloadPreviewAsync(string assetId, Stream destination, CancellationToken cancellationToken)`
    - `Task<IReadOnlyList<ImmichFace>> GetFacesAsync(string assetId, CancellationToken cancellationToken)`
  - `ImmichClient(HttpClient http) : IImmichClient`. Its HttpClient must already have `BaseAddress` (ending in `/`) and the `x-api-key` header set; Task 5 does that.

- [ ] **Step 1: Create the test helper**

`HomeSpeaker.Server2.Tests/Photos/FakeHttpMessageHandler.cs`:

```csharp
using System.Net;
using System.Text;

namespace HomeSpeaker.Server2.Tests.Photos;

internal sealed class FakeHttpMessageHandler(Func<HttpRequestMessage, string?, HttpResponseMessage> respond) : HttpMessageHandler
{
    public List<(HttpRequestMessage Request, string? Body)> Requests { get; } = [];

    public static HttpResponseMessage Json(string json, HttpStatusCode status = HttpStatusCode.OK) =>
        new(status) { Content = new StringContent(json, Encoding.UTF8, "application/json") };

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var body = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
        Requests.Add((request, body));
        return respond(request, body);
    }
}
```

- [ ] **Step 2: Write the failing tests**

`HomeSpeaker.Server2.Tests/Photos/ImmichClientTests.cs`:

```csharp
using System.Net;
using HomeSpeaker.Server2.Services.Photos;

namespace HomeSpeaker.Server2.Tests.Photos;

public sealed class ImmichClientTests
{
    private static (ImmichClient Client, FakeHttpMessageHandler Handler) create(Func<HttpRequestMessage, string?, HttpResponseMessage> respond)
    {
        var handler = new FakeHttpMessageHandler(respond);
        var http = new HttpClient(handler) { BaseAddress = new Uri("https://immich.test/") };
        return (new ImmichClient(http), handler);
    }

    [Fact]
    public async Task GetAlbumImagesAsync_FollowsNextPage_AndSkipsTrashedAndVideos()
    {
        var (client, handler) = create((_, body) => body!.Contains("\"page\":1", StringComparison.Ordinal)
            ? FakeHttpMessageHandler.Json("""
                {"assets":{"items":[
                  {"id":"a1","type":"IMAGE","isTrashed":false,"localDateTime":"2021-08-14T13:22:05.000Z","exifInfo":{"city":"Garden City","state":"Utah","country":"United States of America"}},
                  {"id":"a2","type":"IMAGE","isTrashed":true,"localDateTime":"2021-08-14T13:22:05.000Z","exifInfo":null}
                ],"nextPage":"2"}}
                """)
            : FakeHttpMessageHandler.Json("""
                {"assets":{"items":[
                  {"id":"a3","type":"IMAGE","isTrashed":false,"localDateTime":"2019-06-01T08:00:00.000Z","exifInfo":{"city":null,"state":"","country":"Mexico"}},
                  {"id":"a4","type":"VIDEO","isTrashed":false,"localDateTime":"2019-06-01T08:00:00.000Z","exifInfo":null}
                ],"nextPage":null}}
                """));

        var assets = await client.GetAlbumImagesAsync("album-1", CancellationToken.None);

        Assert.Equal(["a1", "a3"], assets.Select(a => a.Id));
        Assert.Equal("Garden City", assets[0].City);
        Assert.Equal("Utah", assets[0].State);
        Assert.Null(assets[1].City);
        Assert.Null(assets[1].State); // blank strings normalize to null
        Assert.Equal("Mexico", assets[1].Country);
        Assert.Equal(2, handler.Requests.Count);
        Assert.All(handler.Requests, r =>
        {
            Assert.Equal(HttpMethod.Post, r.Request.Method);
            Assert.Equal("/api/search/metadata", r.Request.RequestUri!.AbsolutePath);
            Assert.Contains("\"albumIds\":[\"album-1\"]", r.Body, StringComparison.Ordinal);
            Assert.Contains("\"type\":\"IMAGE\"", r.Body, StringComparison.Ordinal);
            Assert.Contains("\"withExif\":true", r.Body, StringComparison.Ordinal);
        });
        Assert.Contains("\"page\":2", handler.Requests[1].Body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetAlbumImagesAsync_KeepsLocalWallClockTime()
    {
        var (client, _) = create((_, _) => FakeHttpMessageHandler.Json("""
            {"assets":{"items":[{"id":"a1","type":"IMAGE","isTrashed":false,"localDateTime":"2021-08-14T13:22:05.000Z","exifInfo":null}],"nextPage":null}}
            """));

        var asset = Assert.Single(await client.GetAlbumImagesAsync("album-1", CancellationToken.None));

        Assert.Equal(new DateTime(2021, 8, 14, 13, 22, 5), asset.TakenAt);
        Assert.Equal(DateTimeKind.Unspecified, asset.TakenAt.Kind);
    }

    [Fact]
    public async Task GetAlbumImagesAsync_WhenUnauthorized_Throws()
    {
        var (client, _) = create((_, _) => FakeHttpMessageHandler.Json("{}", HttpStatusCode.Unauthorized));

        await Assert.ThrowsAsync<HttpRequestException>(() => client.GetAlbumImagesAsync("album-1", CancellationToken.None));
    }

    [Fact]
    public async Task DownloadPreviewAsync_RequestsPreviewSize_AndCopiesBytes()
    {
        var (client, handler) = create((_, _) => new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent([1, 2, 3]) });
        using var destination = new MemoryStream();

        await client.DownloadPreviewAsync("a1", destination, CancellationToken.None);

        Assert.Equal([1, 2, 3], destination.ToArray());
        Assert.Equal("/api/assets/a1/thumbnail", handler.Requests[0].Request.RequestUri!.AbsolutePath);
        Assert.Equal("?size=preview", handler.Requests[0].Request.RequestUri!.Query);
    }

    [Fact]
    public async Task GetFacesAsync_MapsBoundingBoxes()
    {
        var (client, handler) = create((_, _) => FakeHttpMessageHandler.Json("""
            [{"id":"f1","boundingBoxX1":100,"boundingBoxY1":50,"boundingBoxX2":300,"boundingBoxY2":250,"imageWidth":1000,"imageHeight":500,"person":null,"sourceType":"machine-learning"}]
            """));

        var face = Assert.Single(await client.GetFacesAsync("a1", CancellationToken.None));

        Assert.Equal(new ImmichFace(100, 50, 300, 250, 1000, 500), face);
        Assert.Equal("/api/faces", handler.Requests[0].Request.RequestUri!.AbsolutePath);
        Assert.Equal("?id=a1", handler.Requests[0].Request.RequestUri!.Query);
    }
}
```

- [ ] **Step 3: Run the tests to verify they fail**

Run: `dotnet test HomeSpeaker.Server2.Tests --filter "FullyQualifiedName~ImmichClientTests"`
Expected: a build failure, because `HomeSpeaker.Server2.Services.Photos` does not exist yet.

- [ ] **Step 4: Implement the options and client**

`HomeSpeaker.Server2/Services/Photos/ImmichOptions.cs`:

```csharp
namespace HomeSpeaker.Server2.Services.Photos;

public class ImmichOptions
{
    public string? BaseUrl { get; set; }
    public string? ApiKey { get; set; }
    public string? AlbumId { get; set; }
    public int SyncIntervalMinutes { get; set; } = 15;
    public int SlideSeconds { get; set; } = 30;

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(BaseUrl) &&
        !string.IsNullOrWhiteSpace(ApiKey) &&
        !string.IsNullOrWhiteSpace(AlbumId);
}
```

`HomeSpeaker.Server2/Services/Photos/ImmichClient.cs`:

```csharp
using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;

namespace HomeSpeaker.Server2.Services.Photos;

public sealed record ImmichAsset(string Id, DateTime TakenAt, string? City, string? State, string? Country);

/// <summary>A face bounding box in pixels of the image Immich measured it on.</summary>
public sealed record ImmichFace(double X1, double Y1, double X2, double Y2, int ImageWidth, int ImageHeight);

public interface IImmichClient
{
    Task<IReadOnlyList<ImmichAsset>> GetAlbumImagesAsync(string albumId, CancellationToken cancellationToken);
    Task DownloadPreviewAsync(string assetId, Stream destination, CancellationToken cancellationToken);
    Task<IReadOnlyList<ImmichFace>> GetFacesAsync(string assetId, CancellationToken cancellationToken);
}

public sealed class ImmichClient(HttpClient http) : IImmichClient
{
    private const int pageSize = 250;
    private const int maxPages = 200;
    private static readonly JsonSerializerOptions json = new(JsonSerializerDefaults.Web);

    public async Task<IReadOnlyList<ImmichAsset>> GetAlbumImagesAsync(string albumId, CancellationToken cancellationToken)
    {
        var results = new List<ImmichAsset>();
        var page = 1;
        for (var i = 0; i < maxPages; i++)
        {
            var request = new SearchRequest([albumId], "IMAGE", WithExif: true, Size: pageSize, Page: page);
            using var response = await http.PostAsJsonAsync("api/search/metadata", request, json, cancellationToken);
            response.EnsureSuccessStatusCode();
            var body = await response.Content.ReadFromJsonAsync<SearchResponse>(json, cancellationToken)
                ?? throw new InvalidOperationException("Immich returned an empty search response");

            foreach (var asset in body.Assets.Items)
            {
                if (asset.IsTrashed || !string.Equals(asset.Type, "IMAGE", StringComparison.Ordinal))
                {
                    continue;
                }

                results.Add(new ImmichAsset(
                    asset.Id,
                    DateTime.SpecifyKind(asset.LocalDateTime, DateTimeKind.Unspecified),
                    blankToNull(asset.ExifInfo?.City),
                    blankToNull(asset.ExifInfo?.State),
                    blankToNull(asset.ExifInfo?.Country)));
            }

            if (string.IsNullOrEmpty(body.Assets.NextPage) ||
                !int.TryParse(body.Assets.NextPage, NumberStyles.Integer, CultureInfo.InvariantCulture, out page))
            {
                break;
            }
        }

        return results;
    }

    public async Task DownloadPreviewAsync(string assetId, Stream destination, CancellationToken cancellationToken)
    {
        using var response = await http.GetAsync(
            $"api/assets/{Uri.EscapeDataString(assetId)}/thumbnail?size=preview",
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);
        response.EnsureSuccessStatusCode();
        await using var source = await response.Content.ReadAsStreamAsync(cancellationToken);
        await source.CopyToAsync(destination, cancellationToken);
    }

    public async Task<IReadOnlyList<ImmichFace>> GetFacesAsync(string assetId, CancellationToken cancellationToken)
    {
        var faces = await http.GetFromJsonAsync<List<FaceDto>>($"api/faces?id={Uri.EscapeDataString(assetId)}", json, cancellationToken) ?? [];
        return faces
            .Select(f => new ImmichFace(f.BoundingBoxX1, f.BoundingBoxY1, f.BoundingBoxX2, f.BoundingBoxY2, f.ImageWidth, f.ImageHeight))
            .ToList();
    }

    private static string? blankToNull(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;

    private sealed record SearchRequest(string[] AlbumIds, string Type, bool WithExif, int Size, int Page);

    private sealed record SearchResponse(SearchAssets Assets);

    private sealed record SearchAssets(List<AssetDto> Items, string? NextPage);

    private sealed record AssetDto(string Id, string Type, DateTime LocalDateTime, bool IsTrashed, ExifDto? ExifInfo);

    private sealed record ExifDto(string? City, string? State, string? Country);

    private sealed record FaceDto(int BoundingBoxX1, int BoundingBoxY1, int BoundingBoxX2, int BoundingBoxY2, int ImageWidth, int ImageHeight);
}
```

- [ ] **Step 5: Run the tests to verify they pass**

Run: `dotnet test HomeSpeaker.Server2.Tests --filter "FullyQualifiedName~ImmichClientTests"`
Expected: 5 passed. If the analyzers report warnings in the new files, fix them in code and re-run.

- [ ] **Step 6: Leave changes uncommitted.** The user handles git.

---

### Task 2: Photo cache store and manifest

**Files:**
- Create: `HomeSpeaker.Server2/Services/Photos/PhotoEntry.cs`
- Create: `HomeSpeaker.Server2/Services/Photos/PhotoCacheStore.cs`
- Create: `HomeSpeaker.Server2.Tests/Photos/TempDirectory.cs`
- Test: `HomeSpeaker.Server2.Tests/Photos/PhotoCacheStoreTests.cs`

**Interfaces:**
- Consumes: nothing.
- Produces:
  - `record FaceBox(double X1, double Y1, double X2, double Y2)`, normalized to 0–1
  - `record PhotoEntry { required string AssetId; DateTime TakenAt; string? City; string? State; string? Country; int ImageWidth; int ImageHeight; IReadOnlyList<FaceBox> Faces = [] }`
  - `PhotoCacheStore(string rootPath, ILogger<PhotoCacheStore> logger)` with:
    - `string RootPath`
    - `static bool IsValidAssetId(string)`, true only for a GUID
    - `IReadOnlyList<PhotoEntry> LoadManifest()` and `void SaveManifest(IReadOnlyList<PhotoEntry>)`
    - `bool HasImage(string)`, `bool TryGetImagePath(string, out string? path)`, `Task SaveImageAsync(string assetId, Func<Stream, Task> writeContent)` and `void DeleteImage(string)`
    - `IReadOnlySet<string> LoadHidden()` and `void AddHidden(string)`

- [ ] **Step 1: Create the test helper**

`HomeSpeaker.Server2.Tests/Photos/TempDirectory.cs`:

```csharp
namespace HomeSpeaker.Server2.Tests.Photos;

internal sealed class TempDirectory : IDisposable
{
    public TempDirectory() => Path = Directory.CreateTempSubdirectory("hs-photos-").FullName;

    public string Path { get; }

    public void Dispose()
    {
        try { Directory.Delete(Path, recursive: true); } catch (IOException) { }
    }
}
```

- [ ] **Step 2: Write the failing tests**

`HomeSpeaker.Server2.Tests/Photos/PhotoCacheStoreTests.cs`:

```csharp
using HomeSpeaker.Server2.Services.Photos;
using Microsoft.Extensions.Logging.Abstractions;

namespace HomeSpeaker.Server2.Tests.Photos;

public sealed class PhotoCacheStoreTests : IDisposable
{
    private const string idA = "11111111-1111-4111-8111-111111111111";
    private readonly TempDirectory temp = new();

    private PhotoCacheStore createStore() => new(temp.Path, NullLogger<PhotoCacheStore>.Instance);

    public void Dispose() => temp.Dispose();

    [Fact]
    public void LoadManifest_WhenMissing_ReturnsEmpty() => Assert.Empty(createStore().LoadManifest());

    [Fact]
    public void SaveManifest_ThenLoad_RoundTrips()
    {
        var store = createStore();
        var entry = new PhotoEntry
        {
            AssetId = idA,
            TakenAt = new DateTime(2021, 8, 14, 13, 22, 5),
            City = "Garden City",
            State = "Utah",
            ImageWidth = 1440,
            ImageHeight = 1080,
            Faces = [new FaceBox(0.1, 0.2, 0.3, 0.4)],
        };

        store.SaveManifest([entry]);
        var loaded = Assert.Single(createStore().LoadManifest());

        Assert.Equal(entry.AssetId, loaded.AssetId);
        Assert.Equal(entry.TakenAt, loaded.TakenAt);
        Assert.Equal("Garden City", loaded.City);
        Assert.Equal(1440, loaded.ImageWidth);
        Assert.Equal(new FaceBox(0.1, 0.2, 0.3, 0.4), Assert.Single(loaded.Faces));
        Assert.False(File.Exists(Path.Combine(temp.Path, "manifest.json.tmp")));
    }

    [Fact]
    public void LoadManifest_WhenCorrupt_ReturnsEmpty()
    {
        File.WriteAllText(Path.Combine(temp.Path, "manifest.json"), "{ not json");
        Assert.Empty(createStore().LoadManifest());
    }

    [Fact]
    public async Task SaveImageAsync_WritesFile_AndTryGetImagePathFindsIt()
    {
        var store = createStore();

        await store.SaveImageAsync(idA, s => s.WriteAsync(new byte[] { 9, 9 }).AsTask());

        Assert.True(store.HasImage(idA));
        Assert.True(store.TryGetImagePath(idA, out var path));
        Assert.Equal([9, 9], await File.ReadAllBytesAsync(path!));
        Assert.Empty(Directory.GetFiles(temp.Path, "*.tmp"));
    }

    [Fact]
    public async Task SaveImageAsync_WhenWriterThrows_LeavesNoFile()
    {
        var store = createStore();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            store.SaveImageAsync(idA, _ => throw new InvalidOperationException("network died")));

        Assert.False(store.HasImage(idA));
        Assert.Empty(Directory.GetFiles(temp.Path));
    }

    [Theory]
    [InlineData("../manifest")]
    [InlineData("manifest")]
    [InlineData("")]
    public void TryGetImagePath_RejectsNonGuidIds(string id)
    {
        File.WriteAllText(Path.Combine(temp.Path, "manifest.jpg"), "x");
        Assert.False(createStore().TryGetImagePath(id, out _));
    }

    [Fact]
    public async Task DeleteImage_RemovesFile()
    {
        var store = createStore();
        await store.SaveImageAsync(idA, s => s.WriteAsync(new byte[] { 1 }).AsTask());

        store.DeleteImage(idA);

        Assert.False(store.HasImage(idA));
    }

    [Fact]
    public void AddHidden_PersistsAcrossInstances()
    {
        createStore().AddHidden(idA);
        Assert.Contains(idA, createStore().LoadHidden());
    }
}
```

- [ ] **Step 3: Run the tests to verify they fail**

Run: `dotnet test HomeSpeaker.Server2.Tests --filter "FullyQualifiedName~PhotoCacheStoreTests"`
Expected: a build failure, because `PhotoCacheStore` and `PhotoEntry` are not defined.

- [ ] **Step 4: Implement**

`HomeSpeaker.Server2/Services/Photos/PhotoEntry.cs`:

```csharp
namespace HomeSpeaker.Server2.Services.Photos;

/// <summary>A face bounding box normalized to 0..1 of the image's width/height.</summary>
public sealed record FaceBox(double X1, double Y1, double X2, double Y2);

public sealed record PhotoEntry
{
    public required string AssetId { get; init; }
    public DateTime TakenAt { get; init; }
    public string? City { get; init; }
    public string? State { get; init; }
    public string? Country { get; init; }

    /// <summary>Dimensions of the image the face boxes were measured on; 0 when there are no faces.</summary>
    public int ImageWidth { get; init; }
    public int ImageHeight { get; init; }
    public IReadOnlyList<FaceBox> Faces { get; init; } = [];
}
```

`HomeSpeaker.Server2/Services/Photos/PhotoCacheStore.cs`:

```csharp
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

namespace HomeSpeaker.Server2.Services.Photos;

/// <summary>
/// Owns the on-disk photo cache: preview JPEGs named {assetId}.jpg, manifest.json and hidden.json.
/// Every write goes to a .tmp file first and is then renamed, so readers never see a partial file.
/// </summary>
public sealed class PhotoCacheStore
{
    private const string manifestFile = "manifest.json";
    private const string hiddenFile = "hidden.json";
    private static readonly JsonSerializerOptions json = new(JsonSerializerDefaults.Web);
    private readonly Lock hiddenGate = new();
    private readonly ILogger<PhotoCacheStore> logger;

    public PhotoCacheStore(string rootPath, ILogger<PhotoCacheStore> logger)
    {
        RootPath = rootPath;
        this.logger = logger;
        Directory.CreateDirectory(rootPath);
    }

    public string RootPath { get; }

    public static bool IsValidAssetId(string assetId) => Guid.TryParse(assetId, out _);

    public IReadOnlyList<PhotoEntry> LoadManifest() => readJson<List<PhotoEntry>>(manifestFile) ?? [];

    public void SaveManifest(IReadOnlyList<PhotoEntry> entries) => writeJsonAtomic(manifestFile, entries);

    public bool HasImage(string assetId) => IsValidAssetId(assetId) && File.Exists(imagePath(assetId));

    public bool TryGetImagePath(string assetId, [NotNullWhen(true)] out string? path)
    {
        path = null;
        if (!HasImage(assetId))
        {
            return false;
        }

        path = imagePath(assetId);
        return true;
    }

    public async Task SaveImageAsync(string assetId, Func<Stream, Task> writeContent)
    {
        if (!IsValidAssetId(assetId))
        {
            throw new ArgumentException($"Not a valid asset id: {assetId}", nameof(assetId));
        }

        var finalPath = imagePath(assetId);
        var tempPath = finalPath + ".tmp";
        try
        {
            await using (var stream = File.Create(tempPath))
            {
                await writeContent(stream);
            }

            File.Move(tempPath, finalPath, overwrite: true);
        }
        catch
        {
            tryDelete(tempPath);
            throw;
        }
    }

    public void DeleteImage(string assetId)
    {
        if (IsValidAssetId(assetId))
        {
            tryDelete(imagePath(assetId));
        }
    }

    public IReadOnlySet<string> LoadHidden()
    {
        lock (hiddenGate)
        {
            return readJson<HashSet<string>>(hiddenFile) ?? [];
        }
    }

    public void AddHidden(string assetId)
    {
        lock (hiddenGate)
        {
            var hidden = readJson<HashSet<string>>(hiddenFile) ?? [];
            if (hidden.Add(assetId))
            {
                writeJsonAtomic(hiddenFile, hidden);
            }
        }
    }

    private string imagePath(string assetId) => Path.Combine(RootPath, assetId + ".jpg");

    private T? readJson<T>(string fileName)
        where T : class
    {
        var path = Path.Combine(RootPath, fileName);
        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<T>(File.ReadAllText(path), json);
        }
        catch (Exception ex) when (ex is JsonException or IOException)
        {
            logger.LogWarning(ex, "Could not read {File}; treating it as empty", path);
            return null;
        }
    }

    private void writeJsonAtomic<T>(string fileName, T value)
    {
        var path = Path.Combine(RootPath, fileName);
        var tempPath = path + ".tmp";
        File.WriteAllText(tempPath, JsonSerializer.Serialize(value, json));
        File.Move(tempPath, path, overwrite: true);
    }

    private void tryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (IOException ex)
        {
            logger.LogWarning(ex, "Could not delete {File}", path);
        }
    }
}
```

- [ ] **Step 5: Run the tests to verify they pass**

Run: `dotnet test HomeSpeaker.Server2.Tests --filter "FullyQualifiedName~PhotoCacheStoreTests"`
Expected: 10 passed (the `[Theory]` counts as 3).

- [ ] **Step 6: Leave changes uncommitted.**

---

### Task 3: Caption text and face-aware overlay layout

**Files:**
- Create: `HomeSpeaker.Server2/Services/Photos/PhotoCaption.cs`
- Create: `HomeSpeaker.Server2/Services/Photos/OverlayLayoutChooser.cs`
- Test: `HomeSpeaker.Server2.Tests/Photos/PhotoCaptionTests.cs`
- Test: `HomeSpeaker.Server2.Tests/Photos/OverlayLayoutChooserTests.cs`

**Interfaces:**
- Consumes: `PhotoEntry` and `FaceBox` (Task 2).
- Produces:
  - `static class PhotoCaption` with `string? Place(PhotoEntry photo)` and `string When(DateTime takenAt, DateTime today)`
  - `enum OverlayLayout { Default, Mirrored }`
  - `static class OverlayLayoutChooser` with `OverlayLayout Choose(PhotoEntry photo)`

- [ ] **Step 1: Write the failing tests**

`HomeSpeaker.Server2.Tests/Photos/PhotoCaptionTests.cs`:

```csharp
using HomeSpeaker.Server2.Services.Photos;

namespace HomeSpeaker.Server2.Tests.Photos;

public sealed class PhotoCaptionTests
{
    private static PhotoEntry photo(string? city, string? state, string? country) =>
        new() { AssetId = "x", City = city, State = state, Country = country };

    [Fact]
    public void Place_CityAndState() => Assert.Equal("Bear Lake, Utah", PhotoCaption.Place(photo("Bear Lake", "Utah", "United States of America")));

    [Fact]
    public void Place_CityAndCountry_WhenNoState() => Assert.Equal("Cancún, Mexico", PhotoCaption.Place(photo("Cancún", null, "Mexico")));

    [Fact]
    public void Place_RegionOnly() => Assert.Equal("Utah", PhotoCaption.Place(photo(null, "Utah", null)));

    [Fact]
    public void Place_WithNoLocation_ReturnsNull() => Assert.Null(PhotoCaption.Place(photo(null, " ", null)));

    [Theory]
    [InlineData("2021-08-14", "2026-10-13", "August 2021 · 5 years ago")]
    [InlineData("2021-11-01", "2026-10-13", "November 2021 · 4 years ago")]
    [InlineData("2025-10-01", "2026-10-13", "October 2025 · 1 year ago")]
    [InlineData("2026-03-05", "2026-10-13", "March 2026")]
    public void When_FormatsMonthAndYearsAgo(string taken, string today, string expected) =>
        Assert.Equal(expected, PhotoCaption.When(DateTime.Parse(taken, System.Globalization.CultureInfo.InvariantCulture), DateTime.Parse(today, System.Globalization.CultureInfo.InvariantCulture)));
}
```

`HomeSpeaker.Server2.Tests/Photos/OverlayLayoutChooserTests.cs`:

```csharp
using HomeSpeaker.Server2.Services.Photos;

namespace HomeSpeaker.Server2.Tests.Photos;

public sealed class OverlayLayoutChooserTests
{
    private static PhotoEntry photo(int width, int height, params FaceBox[] faces) =>
        new() { AssetId = "x", ImageWidth = width, ImageHeight = height, Faces = faces };

    [Fact]
    public void NoFaces_IsDefault() => Assert.Equal(OverlayLayout.Default, OverlayLayoutChooser.Choose(photo(0, 0)));

    [Fact]
    public void FaceUnderTopRightChips_IsMirrored() =>
        Assert.Equal(OverlayLayout.Mirrored, OverlayLayoutChooser.Choose(photo(1024, 600, new FaceBox(0.75, 0.10, 0.90, 0.35))));

    [Fact]
    public void FaceWhereMirroredPanelWouldSit_IsDefault() =>
        Assert.Equal(OverlayLayout.Default, OverlayLayoutChooser.Choose(photo(1024, 600, new FaceBox(0.60, 0.72, 0.90, 0.95))));

    [Fact]
    public void CenteredFace_IsDefault() =>
        Assert.Equal(OverlayLayout.Default, OverlayLayoutChooser.Choose(photo(1024, 600, new FaceBox(0.45, 0.40, 0.55, 0.60))));

    [Fact]
    public void FaceCroppedOffScreen_IsIgnored()
    {
        // Portrait 600x1200 shown with object-fit: cover at 1024x600 keeps only the middle ~29% of
        // its height, so a face at the very top is cropped away and must not push the overlays aside.
        Assert.Equal(OverlayLayout.Default, OverlayLayoutChooser.Choose(photo(600, 1200, new FaceBox(0.75, 0.05, 0.90, 0.15))));
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test HomeSpeaker.Server2.Tests --filter "FullyQualifiedName~PhotoCaptionTests|FullyQualifiedName~OverlayLayoutChooserTests"`
Expected: a build failure, because `PhotoCaption` and `OverlayLayoutChooser` are not defined.

- [ ] **Step 3: Implement**

`HomeSpeaker.Server2/Services/Photos/PhotoCaption.cs`:

```csharp
using System.Globalization;

namespace HomeSpeaker.Server2.Services.Photos;

public static class PhotoCaption
{
    public static string? Place(PhotoEntry photo)
    {
        var city = clean(photo.City);
        var region = clean(photo.State) ?? clean(photo.Country);
        return (city, region) switch
        {
            (not null, not null) => $"{city}, {region}",
            (not null, null) => city,
            (null, not null) => region,
            _ => null,
        };
    }

    public static string When(DateTime takenAt, DateTime today)
    {
        var years = today.Year - takenAt.Year;
        if (today.Month < takenAt.Month || (today.Month == takenAt.Month && today.Day < takenAt.Day))
        {
            years--;
        }

        var month = takenAt.ToString("MMMM yyyy", CultureInfo.InvariantCulture);
        return years switch
        {
            < 1 => month,
            1 => $"{month} · 1 year ago",
            _ => string.Create(CultureInfo.InvariantCulture, $"{month} · {years} years ago"),
        };
    }

    private static string? clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
```

`HomeSpeaker.Server2/Services/Photos/OverlayLayoutChooser.cs`:

```csharp
namespace HomeSpeaker.Server2.Services.Photos;

public enum OverlayLayout
{
    Default,
    Mirrored,
}

/// <summary>
/// Picks the overlay arrangement that covers the fewest faces. The default arrangement has the
/// clock top-left, chips top-right and now-playing bottom-left; mirrored swaps left and right.
/// Face boxes are mapped through the object-fit: cover crop of a 1024x600 screen first.
/// </summary>
public static class OverlayLayoutChooser
{
    private const double screenAspect = 1024.0 / 600.0;

    // Normalized screen regions for the default layout: clock+date, chips, now-playing panel.
    private static readonly NormalizedRect[] defaultRegions =
    [
        new(0.03, 0.04, 0.42, 0.30),
        new(0.66, 0.04, 0.97, 0.40),
        new(0.03, 0.70, 0.55, 0.96),
    ];

    public static OverlayLayout Choose(PhotoEntry photo)
    {
        if (photo.Faces.Count == 0 || photo.ImageWidth <= 0 || photo.ImageHeight <= 0)
        {
            return OverlayLayout.Default;
        }

        var imageAspect = (double)photo.ImageWidth / photo.ImageHeight;
        var faces = photo.Faces.Select(f => toScreen(f, imageAspect)).ToList();
        var defaultOverlap = totalOverlap(defaultRegions, faces);
        var mirroredOverlap = totalOverlap(defaultRegions.Select(mirror), faces);
        return mirroredOverlap < defaultOverlap ? OverlayLayout.Mirrored : OverlayLayout.Default;
    }

    private static NormalizedRect toScreen(FaceBox face, double imageAspect)
    {
        if (imageAspect > screenAspect)
        {
            // Wider than the screen: full height visible, left and right edges cropped.
            var visibleWidth = screenAspect / imageAspect;
            var offset = (1 - visibleWidth) / 2;
            return new((face.X1 - offset) / visibleWidth, face.Y1, (face.X2 - offset) / visibleWidth, face.Y2);
        }

        // Taller than the screen: full width visible, top and bottom cropped.
        var visibleHeight = imageAspect / screenAspect;
        var offsetY = (1 - visibleHeight) / 2;
        return new(face.X1, (face.Y1 - offsetY) / visibleHeight, face.X2, (face.Y2 - offsetY) / visibleHeight);
    }

    private static NormalizedRect mirror(NormalizedRect r) => new(1 - r.X2, r.Y1, 1 - r.X1, r.Y2);

    private static double totalOverlap(IEnumerable<NormalizedRect> regions, List<NormalizedRect> faces) =>
        regions.Sum(region => faces.Sum(face => intersection(region, face)));

    private static double intersection(NormalizedRect a, NormalizedRect b)
    {
        var width = Math.Min(a.X2, b.X2) - Math.Max(a.X1, b.X1);
        var height = Math.Min(a.Y2, b.Y2) - Math.Max(a.Y1, b.Y1);
        return width > 0 && height > 0 ? width * height : 0;
    }

    private readonly record struct NormalizedRect(double X1, double Y1, double X2, double Y2);
}
```

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet test HomeSpeaker.Server2.Tests --filter "FullyQualifiedName~PhotoCaptionTests|FullyQualifiedName~OverlayLayoutChooserTests"`
Expected: 13 passed.

- [ ] **Step 5: Leave changes uncommitted.**

---

### Task 4: Photo cycle and photo library

**Files:**
- Create: `HomeSpeaker.Server2/Services/Photos/PhotoCycle.cs`
- Create: `HomeSpeaker.Server2/Services/Photos/PhotoLibrary.cs`
- Create: `HomeSpeaker.Server2.Tests/Photos/FixedTimeProvider.cs`
- Test: `HomeSpeaker.Server2.Tests/Photos/PhotoCycleTests.cs`
- Test: `HomeSpeaker.Server2.Tests/Photos/PhotoLibraryTests.cs`

**Interfaces:**
- Consumes: `PhotoCacheStore`, `PhotoEntry` (Task 2); `PhotoCaption`, `OverlayLayoutChooser`, `OverlayLayout` (Task 3).
- Produces:
  - `PhotoCycle(Random random)` with `void SetAvailable(IEnumerable<string> ids)` and `string? Next()`. It is not thread-safe; callers lock.
  - `record NextPhoto(string Id, string Url, string? Place, string When, string Layout)`. `Url` is `/api/photos/{id}.jpg` and `Layout` is `"default"` or `"mirrored"`.
  - `PhotoLibrary(PhotoCacheStore store, TimeProvider timeProvider)` plus a test constructor taking a `Random`. It has `int Count`, `void Reload()`, `NextPhoto? Next()` and `bool Hide(string assetId)`.

- [ ] **Step 1: Create the test helper**

`HomeSpeaker.Server2.Tests/Photos/FixedTimeProvider.cs`:

```csharp
namespace HomeSpeaker.Server2.Tests.Photos;

internal sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => now;

    public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;
}
```

- [ ] **Step 2: Write the failing tests**

`HomeSpeaker.Server2.Tests/Photos/PhotoCycleTests.cs`:

```csharp
using HomeSpeaker.Server2.Services.Photos;

namespace HomeSpeaker.Server2.Tests.Photos;

public sealed class PhotoCycleTests
{
    [Fact]
    public void Next_WhenEmpty_ReturnsNull() => Assert.Null(new PhotoCycle(new Random(1)).Next());

    [Fact]
    public void Next_ShowsEveryPhotoOnceBeforeRepeating()
    {
        var cycle = new PhotoCycle(new Random(42));
        string[] ids = ["a", "b", "c", "d", "e"];
        cycle.SetAvailable(ids);

        var round = Enumerable.Range(0, 5).Select(_ => cycle.Next()!).ToList();

        Assert.Equal(ids.Order(), round.Order());
    }

    [Fact]
    public void Next_NeverRepeatsBackToBackAcrossRounds()
    {
        var cycle = new PhotoCycle(new Random(7));
        cycle.SetAvailable(["a", "b", "c"]);
        string? previous = null;

        for (var i = 0; i < 300; i++)
        {
            var current = cycle.Next();
            Assert.NotEqual(previous, current);
            previous = current;
        }
    }

    [Fact]
    public void SetAvailable_MidRound_KeepsShownOutAndAddsNew()
    {
        var cycle = new PhotoCycle(new Random(3));
        cycle.SetAvailable(["a", "b", "c", "d"]);
        var shown = new[] { cycle.Next()!, cycle.Next()! };
        var unshown = new[] { "a", "b", "c", "d" }.Except(shown).ToList();

        // Remove one unshown photo, add a brand-new one.
        cycle.SetAvailable(shown.Concat([unshown[0], "z"]));
        var rest = new[] { cycle.Next()!, cycle.Next()! };

        Assert.Equal(new[] { unshown[0], "z" }.Order(), rest.Order());
    }

    [Fact]
    public void Next_SinglePhoto_RepeatsIt()
    {
        var cycle = new PhotoCycle(new Random(1));
        cycle.SetAvailable(["only"]);
        Assert.Equal("only", cycle.Next());
        Assert.Equal("only", cycle.Next());
    }
}
```

`HomeSpeaker.Server2.Tests/Photos/PhotoLibraryTests.cs`:

```csharp
using HomeSpeaker.Server2.Services.Photos;
using Microsoft.Extensions.Logging.Abstractions;

namespace HomeSpeaker.Server2.Tests.Photos;

public sealed class PhotoLibraryTests : IDisposable
{
    private const string idA = "11111111-1111-4111-8111-111111111111";
    private const string idB = "22222222-2222-4222-8222-222222222222";
    private static readonly FixedTimeProvider today = new(new DateTimeOffset(2026, 10, 13, 12, 0, 0, TimeSpan.Zero));
    private readonly TempDirectory temp = new();
    private readonly PhotoCacheStore store;

    public PhotoLibraryTests() => store = new PhotoCacheStore(temp.Path, NullLogger<PhotoCacheStore>.Instance);

    public void Dispose() => temp.Dispose();

    private async Task addAsync(string id, string? city = "Bear Lake", bool withImage = true, params FaceBox[] faces)
    {
        if (withImage)
        {
            await store.SaveImageAsync(id, s => s.WriteAsync(new byte[] { 1 }).AsTask());
        }

        var entries = store.LoadManifest().ToList();
        entries.Add(new PhotoEntry
        {
            AssetId = id,
            TakenAt = new DateTime(2021, 8, 14),
            City = city,
            State = "Utah",
            ImageWidth = faces.Length > 0 ? 1024 : 0,
            ImageHeight = faces.Length > 0 ? 600 : 0,
            Faces = faces,
        });
        store.SaveManifest(entries);
    }

    [Fact]
    public async Task Next_ReturnsUrlCaptionAndLayout()
    {
        await addAsync(idA, faces: [new FaceBox(0.75, 0.10, 0.90, 0.35)]);
        var library = new PhotoLibrary(store, today, new Random(1));

        var next = library.Next()!;

        Assert.Equal(idA, next.Id);
        Assert.Equal($"/api/photos/{idA}.jpg", next.Url);
        Assert.Equal("Bear Lake, Utah", next.Place);
        Assert.Equal("August 2021 · 5 years ago", next.When);
        Assert.Equal("mirrored", next.Layout);
    }

    [Fact]
    public async Task Reload_SkipsEntriesWhoseImageIsMissing()
    {
        await addAsync(idA);
        await addAsync(idB, withImage: false);

        var library = new PhotoLibrary(store, today, new Random(1));

        Assert.Equal(1, library.Count);
    }

    [Fact]
    public async Task Hide_RemovesPhoto_AndPersists()
    {
        await addAsync(idA);
        await addAsync(idB);
        var library = new PhotoLibrary(store, today, new Random(1));

        Assert.True(library.Hide(idA));

        Assert.Equal(1, library.Count);
        Assert.Equal(idB, new PhotoLibrary(store, today, new Random(1)).Next()!.Id);
    }

    [Fact]
    public void Hide_UnknownOrInvalidId_ReturnsFalse()
    {
        var library = new PhotoLibrary(store, today, new Random(1));
        Assert.False(library.Hide(idA));
        Assert.False(library.Hide("../x"));
    }

    [Fact]
    public async Task Next_WhenOnlyPhotoHidden_ReturnsNull()
    {
        await addAsync(idA);
        var library = new PhotoLibrary(store, today, new Random(1));

        library.Hide(idA);

        Assert.Null(library.Next());
    }
}
```

- [ ] **Step 3: Run the tests to verify they fail**

Run: `dotnet test HomeSpeaker.Server2.Tests --filter "FullyQualifiedName~PhotoCycleTests|FullyQualifiedName~PhotoLibraryTests"`
Expected: a build failure, because `PhotoCycle` and `PhotoLibrary` are not defined.

- [ ] **Step 4: Implement**

`HomeSpeaker.Server2/Services/Photos/PhotoCycle.cs`:

```csharp
namespace HomeSpeaker.Server2.Services.Photos;

/// <summary>
/// Shuffled rotation: every available photo is shown once per round, and a new round never starts
/// with the photo that ended the previous one. Not thread-safe; PhotoLibrary locks around it.
/// </summary>
public sealed class PhotoCycle(Random random)
{
    private readonly HashSet<string> shownThisRound = new(StringComparer.Ordinal);
    private HashSet<string> available = new(StringComparer.Ordinal);
    private List<string> pending = [];
    private string? last;

    public void SetAvailable(IEnumerable<string> ids)
    {
        available = new HashSet<string>(ids, StringComparer.Ordinal);
        shownThisRound.IntersectWith(available);
        pending = shuffle(available.Where(id => !shownThisRound.Contains(id)));
    }

    public string? Next()
    {
        if (available.Count == 0)
        {
            return null;
        }

        if (pending.Count == 0)
        {
            shownThisRound.Clear();
            pending = shuffle(available);
            if (pending.Count > 1 && string.Equals(pending[0], last, StringComparison.Ordinal))
            {
                (pending[0], pending[^1]) = (pending[^1], pending[0]);
            }
        }

        var id = pending[0];
        pending.RemoveAt(0);
        shownThisRound.Add(id);
        last = id;
        return id;
    }

    private List<string> shuffle(IEnumerable<string> ids)
    {
        var list = ids.ToList();
        for (var i = list.Count - 1; i > 0; i--)
        {
            var j = random.Next(i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }

        return list;
    }
}
```

`HomeSpeaker.Server2/Services/Photos/PhotoLibrary.cs`:

```csharp
namespace HomeSpeaker.Server2.Services.Photos;

public sealed record NextPhoto(string Id, string Url, string? Place, string When, string Layout);

/// <summary>In-memory view of the photo cache. Thread-safe; registered as a singleton.</summary>
public sealed class PhotoLibrary
{
    private readonly PhotoCacheStore store;
    private readonly TimeProvider timeProvider;
    private readonly PhotoCycle cycle;
    private readonly Lock gate = new();
    private Dictionary<string, PhotoEntry> photos = new(StringComparer.Ordinal);

    public PhotoLibrary(PhotoCacheStore store, TimeProvider timeProvider)
        : this(store, timeProvider, Random.Shared)
    {
    }

    public PhotoLibrary(PhotoCacheStore store, TimeProvider timeProvider, Random random)
    {
        this.store = store;
        this.timeProvider = timeProvider;
        cycle = new PhotoCycle(random);
        Reload();
    }

    public int Count
    {
        get
        {
            lock (gate)
            {
                return photos.Count;
            }
        }
    }

    public void Reload()
    {
        var hidden = store.LoadHidden();
        var visible = store.LoadManifest()
            .Where(p => !hidden.Contains(p.AssetId) && store.HasImage(p.AssetId))
            .GroupBy(p => p.AssetId, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);

        lock (gate)
        {
            photos = visible;
            cycle.SetAvailable(visible.Keys);
        }
    }

    public NextPhoto? Next()
    {
        PhotoEntry entry;
        lock (gate)
        {
            var id = cycle.Next();
            if (id is null)
            {
                return null;
            }

            entry = photos[id];
        }

        var today = timeProvider.GetLocalNow().DateTime;
        var layout = OverlayLayoutChooser.Choose(entry) == OverlayLayout.Mirrored ? "mirrored" : "default";
        return new NextPhoto(entry.AssetId, $"/api/photos/{entry.AssetId}.jpg", PhotoCaption.Place(entry), PhotoCaption.When(entry.TakenAt, today), layout);
    }

    public bool Hide(string assetId)
    {
        if (!PhotoCacheStore.IsValidAssetId(assetId))
        {
            return false;
        }

        lock (gate)
        {
            if (!photos.ContainsKey(assetId))
            {
                return false;
            }
        }

        store.AddHidden(assetId);
        Reload();
        return true;
    }
}
```

- [ ] **Step 5: Run the tests to verify they pass**

Run: `dotnet test HomeSpeaker.Server2.Tests --filter "FullyQualifiedName~PhotoCycleTests|FullyQualifiedName~PhotoLibraryTests"`
Expected: 10 passed.

- [ ] **Step 6: Leave changes uncommitted.**

---
### Task 5: Sync worker, photo endpoints, and wiring

**Files:**
- Create: `HomeSpeaker.Server2/Services/Photos/PhotoSyncer.cs`
- Create: `HomeSpeaker.Server2/Services/Photos/PhotoSyncWorker.cs`
- Create: `HomeSpeaker.Server2/Endpoints/PhotoEndpoints.cs`
- Create: `HomeSpeaker.Server2/FeatureFlags.cs`
- Create: `HomeSpeaker.Server2.Tests/Photos/FakeImmichClient.cs`
- Test: `HomeSpeaker.Server2.Tests/Photos/PhotoSyncerTests.cs`
- Modify: `HomeSpeaker.Server2/Program.cs`, in three places: service registrations near the `Configure<AiMusicOptions>` line (~line 99), the `/api/features` mapping (~line 314), and endpoint mapping
- Modify: `homespeaker-secrets.env.example`

**Interfaces:**
- Consumes:
  - `IImmichClient`, `ImmichAsset`, `ImmichFace` and `ImmichOptions` (Task 1)
  - `PhotoCacheStore`, `PhotoEntry` and `FaceBox` (Task 2)
  - `PhotoLibrary` (Task 4)
- Produces:
  - `PhotoSyncer(IImmichClient, PhotoCacheStore, PhotoLibrary, IOptions<ImmichOptions>, ILogger<PhotoSyncer>)` with `Task<bool> SyncAsync(CancellationToken)`. It returns false when the album fetch failed and the cache was left untouched.
  - `PhotoSyncTrigger` (singleton) with `void Request()` and `Task WaitAsync(TimeSpan timeout, CancellationToken)`
  - `PhotoSyncWorker` (hosted service)
  - `static class FeatureFlags` with `bool TemperatureEnabled(IConfiguration)` and `bool BloodSugarEnabled(IConfiguration)`
  - Endpoints:
    - `GET /api/photos/next` returns 200 `{id,url,place,when,layout}` or 204
    - `GET /api/photos/{id}.jpg`
    - `POST /api/photos/{id}/hide`
    - `POST /api/photos/sync`
  - `/api/features` gains `photosEnabled`.
  - DI registrations:
    - `IOptions<ImmichOptions>`
    - typed `IImmichClient`
    - singletons `PhotoCacheStore`, `PhotoLibrary` and `PhotoSyncTrigger`
    - scoped `PhotoSyncer`
    - hosted `PhotoSyncWorker`

- [ ] **Step 1: Create the fake client**

`HomeSpeaker.Server2.Tests/Photos/FakeImmichClient.cs`:

```csharp
using HomeSpeaker.Server2.Services.Photos;

namespace HomeSpeaker.Server2.Tests.Photos;

internal sealed class FakeImmichClient : IImmichClient
{
    public List<ImmichAsset> Assets { get; } = [];
    public Exception? AlbumError { get; set; }
    public HashSet<string> FailingDownloads { get; } = new(StringComparer.Ordinal);
    public Dictionary<string, List<ImmichFace>> Faces { get; } = new(StringComparer.Ordinal);
    public List<string> Downloaded { get; } = [];

    public Task<IReadOnlyList<ImmichAsset>> GetAlbumImagesAsync(string albumId, CancellationToken cancellationToken) =>
        AlbumError is not null
            ? Task.FromException<IReadOnlyList<ImmichAsset>>(AlbumError)
            : Task.FromResult<IReadOnlyList<ImmichAsset>>(Assets.ToList());

    public async Task DownloadPreviewAsync(string assetId, Stream destination, CancellationToken cancellationToken)
    {
        if (FailingDownloads.Contains(assetId))
        {
            throw new HttpRequestException("download failed");
        }

        Downloaded.Add(assetId);
        await destination.WriteAsync(new byte[] { 0xFF, 0xD8 }, cancellationToken);
    }

    public Task<IReadOnlyList<ImmichFace>> GetFacesAsync(string assetId, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<ImmichFace>>(Faces.TryGetValue(assetId, out var faces) ? faces : []);
}
```

- [ ] **Step 2: Write the failing tests**

`HomeSpeaker.Server2.Tests/Photos/PhotoSyncerTests.cs`:

```csharp
using HomeSpeaker.Server2.Services.Photos;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace HomeSpeaker.Server2.Tests.Photos;

public sealed class PhotoSyncerTests : IDisposable
{
    private const string idA = "11111111-1111-4111-8111-111111111111";
    private const string idB = "22222222-2222-4222-8222-222222222222";
    private readonly TempDirectory temp = new();
    private readonly FakeImmichClient immich = new();
    private readonly PhotoCacheStore store;
    private readonly PhotoLibrary library;

    public PhotoSyncerTests()
    {
        store = new PhotoCacheStore(temp.Path, NullLogger<PhotoCacheStore>.Instance);
        library = new PhotoLibrary(store, new FixedTimeProvider(new DateTimeOffset(2026, 10, 13, 12, 0, 0, TimeSpan.Zero)), new Random(1));
    }

    public void Dispose() => temp.Dispose();

    private PhotoSyncer createSyncer() => new(
        immich,
        store,
        library,
        Options.Create(new ImmichOptions { BaseUrl = "https://immich.test", ApiKey = "key", AlbumId = "album" }),
        NullLogger<PhotoSyncer>.Instance);

    private static ImmichAsset asset(string id, string? city = "Bear Lake") => new(id, new DateTime(2021, 8, 14), city, "Utah", null);

    [Fact]
    public async Task Sync_DownloadsNewAssets_AndNormalizesFaces()
    {
        immich.Assets.Add(asset(idA));
        immich.Faces[idA] = [new ImmichFace(100, 50, 300, 250, 1000, 500)];

        Assert.True(await createSyncer().SyncAsync(CancellationToken.None));

        var entry = Assert.Single(store.LoadManifest());
        Assert.True(store.HasImage(idA));
        Assert.Equal(1000, entry.ImageWidth);
        Assert.Equal(500, entry.ImageHeight);
        Assert.Equal(new FaceBox(0.1, 0.1, 0.3, 0.5), Assert.Single(entry.Faces));
        Assert.Equal(1, library.Count);
    }

    [Fact]
    public async Task Sync_RemovesAssetsNoLongerInAlbum()
    {
        immich.Assets.AddRange([asset(idA), asset(idB)]);
        await createSyncer().SyncAsync(CancellationToken.None);

        immich.Assets.RemoveAll(a => string.Equals(a.Id, idB, StringComparison.Ordinal));
        await createSyncer().SyncAsync(CancellationToken.None);

        Assert.Equal([idA], store.LoadManifest().Select(p => p.AssetId));
        Assert.False(store.HasImage(idB));
        Assert.Equal(1, library.Count);
    }

    [Fact]
    public async Task Sync_DoesNotRedownloadCachedAssets_ButRefreshesMetadata()
    {
        immich.Assets.Add(asset(idA));
        await createSyncer().SyncAsync(CancellationToken.None);
        immich.Downloaded.Clear();

        immich.Assets[0] = asset(idA, city: "Garden City");
        await createSyncer().SyncAsync(CancellationToken.None);

        Assert.Empty(immich.Downloaded);
        Assert.Equal("Garden City", Assert.Single(store.LoadManifest()).City);
    }

    [Fact]
    public async Task Sync_WhenAlbumFetchFails_KeepsExistingCache()
    {
        immich.Assets.Add(asset(idA));
        await createSyncer().SyncAsync(CancellationToken.None);

        immich.AlbumError = new HttpRequestException("401 Unauthorized");
        Assert.False(await createSyncer().SyncAsync(CancellationToken.None));

        Assert.Equal([idA], store.LoadManifest().Select(p => p.AssetId));
        Assert.True(store.HasImage(idA));
    }

    [Fact]
    public async Task Sync_SkipsAssetWhoseDownloadFails()
    {
        immich.Assets.AddRange([asset(idA), asset(idB)]);
        immich.FailingDownloads.Add(idB);

        Assert.True(await createSyncer().SyncAsync(CancellationToken.None));

        Assert.Equal([idA], store.LoadManifest().Select(p => p.AssetId));
        Assert.False(store.HasImage(idB));
        Assert.Empty(Directory.GetFiles(temp.Path, "*.tmp"));
    }

    [Fact]
    public async Task Sync_WhenAlbumEmptied_RemovesEverything()
    {
        immich.Assets.Add(asset(idA));
        await createSyncer().SyncAsync(CancellationToken.None);

        immich.Assets.Clear();
        await createSyncer().SyncAsync(CancellationToken.None);

        Assert.Empty(store.LoadManifest());
        Assert.Null(library.Next());
    }
}
```

- [ ] **Step 3: Run the tests to verify they fail**

Run: `dotnet test HomeSpeaker.Server2.Tests --filter "FullyQualifiedName~PhotoSyncerTests"`
Expected: a build failure, because `PhotoSyncer` is not defined.

- [ ] **Step 4: Implement the syncer**

`HomeSpeaker.Server2/Services/Photos/PhotoSyncer.cs`:

```csharp
using Microsoft.Extensions.Options;

namespace HomeSpeaker.Server2.Services.Photos;

/// <summary>One pass of mirroring the configured Immich album into the local photo cache.</summary>
public sealed class PhotoSyncer(
    IImmichClient immich,
    PhotoCacheStore store,
    PhotoLibrary library,
    IOptions<ImmichOptions> options,
    ILogger<PhotoSyncer> logger)
{
    public async Task<bool> SyncAsync(CancellationToken cancellationToken)
    {
        IReadOnlyList<ImmichAsset> assets;
        try
        {
            assets = await immich.GetAlbumImagesAsync(options.Value.AlbumId!, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Immich album fetch failed; keeping the existing photo cache");
            return false;
        }

        var existing = store.LoadManifest()
            .GroupBy(p => p.AssetId, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);
        var entries = new List<PhotoEntry>(assets.Count);
        var added = 0;

        foreach (var asset in assets)
        {
            if (existing.TryGetValue(asset.Id, out var cached) && store.HasImage(asset.Id))
            {
                entries.Add(cached with { TakenAt = asset.TakenAt, City = asset.City, State = asset.State, Country = asset.Country });
                continue;
            }

            try
            {
                await store.SaveImageAsync(asset.Id, stream => immich.DownloadPreviewAsync(asset.Id, stream, cancellationToken));
                entries.Add(await buildEntryAsync(asset, cancellationToken));
                added++;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogWarning(ex, "Skipping Immich asset {AssetId}", asset.Id);
            }
        }

        var albumIds = assets.Select(a => a.Id).ToHashSet(StringComparer.Ordinal);
        var removed = 0;
        foreach (var id in existing.Keys.Where(id => !albumIds.Contains(id)))
        {
            store.DeleteImage(id);
            removed++;
        }

        store.SaveManifest(entries);
        library.Reload();
        logger.LogInformation("Photo sync complete: {Total} photos, {Added} added, {Removed} removed", entries.Count, added, removed);
        return true;
    }

    private async Task<PhotoEntry> buildEntryAsync(ImmichAsset asset, CancellationToken cancellationToken)
    {
        IReadOnlyList<ImmichFace> faces;
        try
        {
            faces = await immich.GetFacesAsync(asset.Id, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogDebug(ex, "No face data for {AssetId}", asset.Id);
            faces = [];
        }

        var sized = faces.Where(f => f.ImageWidth > 0 && f.ImageHeight > 0).ToList();
        return new PhotoEntry
        {
            AssetId = asset.Id,
            TakenAt = asset.TakenAt,
            City = asset.City,
            State = asset.State,
            Country = asset.Country,
            ImageWidth = sized.Count > 0 ? sized[0].ImageWidth : 0,
            ImageHeight = sized.Count > 0 ? sized[0].ImageHeight : 0,
            Faces = sized
                .Select(f => new FaceBox(f.X1 / f.ImageWidth, f.Y1 / f.ImageHeight, f.X2 / f.ImageWidth, f.Y2 / f.ImageHeight))
                .ToList(),
        };
    }
}
```

- [ ] **Step 5: Run the syncer tests to verify they pass**

Run: `dotnet test HomeSpeaker.Server2.Tests --filter "FullyQualifiedName~PhotoSyncerTests"`
Expected: 6 passed.

- [ ] **Step 6: Implement the worker, trigger, endpoints and feature flags**

`HomeSpeaker.Server2/Services/Photos/PhotoSyncWorker.cs`:

```csharp
using Microsoft.Extensions.Options;

namespace HomeSpeaker.Server2.Services.Photos;

/// <summary>Lets the /api/photos/sync endpoint wake the worker early.</summary>
public sealed class PhotoSyncTrigger : IDisposable
{
    private readonly SemaphoreSlim signal = new(0, 1);

    public void Request()
    {
        try
        {
            signal.Release();
        }
        catch (SemaphoreFullException)
        {
            // A sync is already requested.
        }
    }

    public Task WaitAsync(TimeSpan timeout, CancellationToken cancellationToken) => signal.WaitAsync(timeout, cancellationToken);

    public void Dispose() => signal.Dispose();
}

public sealed class PhotoSyncWorker(
    IServiceScopeFactory scopeFactory,
    PhotoSyncTrigger trigger,
    IOptions<ImmichOptions> options,
    ILogger<PhotoSyncWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var immich = options.Value;
        if (!immich.IsConfigured)
        {
            logger.LogInformation("Immich is not configured; the photo slideshow is disabled");
            return;
        }

        var interval = TimeSpan.FromMinutes(Math.Max(1, immich.SyncIntervalMinutes));
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                await scope.ServiceProvider.GetRequiredService<PhotoSyncer>().SyncAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Photo sync failed unexpectedly");
            }

            try
            {
                await trigger.WaitAsync(interval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }
}
```

`HomeSpeaker.Server2/Endpoints/PhotoEndpoints.cs`:

```csharp
using HomeSpeaker.Server2.Services.Photos;

namespace HomeSpeaker.Server2.Endpoints;

public static class PhotoEndpoints
{
    public static IEndpointRouteBuilder MapPhotoApi(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/photos");

        group.MapGet("/next", (PhotoLibrary library) =>
            library.Next() is { } photo ? Results.Ok(photo) : Results.NoContent());

        group.MapGet("/{id}.jpg", (string id, PhotoCacheStore store, HttpContext context) =>
        {
            if (!store.TryGetImagePath(id, out var path))
            {
                return Results.NotFound();
            }

            context.Response.Headers.CacheControl = "public, max-age=31536000, immutable";
            return TypedResults.PhysicalFile(path, "image/jpeg");
        });

        group.MapPost("/{id}/hide", (string id, PhotoLibrary library) =>
            library.Hide(id) ? Results.NoContent() : Results.NotFound());

        group.MapPost("/sync", (PhotoSyncTrigger trigger) =>
        {
            trigger.Request();
            return Results.Accepted();
        });

        return app;
    }
}
```

`HomeSpeaker.Server2/FeatureFlags.cs`:

```csharp
namespace HomeSpeaker.Server2;

public static class FeatureFlags
{
    public static bool TemperatureEnabled(IConfiguration configuration) =>
        !string.IsNullOrEmpty(configuration["Temperature:ApiBaseUrl"]);

    public static bool BloodSugarEnabled(IConfiguration configuration) =>
        !string.IsNullOrEmpty(configuration["NIGHTSCOUT_URL"]);
}
```

- [ ] **Step 7: Wire up `Program.cs`**

Add `using HomeSpeaker.Server2.Services.Photos;` to the usings at the top.

Directly after `builder.Services.Configure<PushNotificationOptions>(builder.Configuration.GetSection("PushNotifications"));` add:

```csharp
builder.Services.Configure<ImmichOptions>(builder.Configuration.GetSection("Immich"));
builder.Services.AddHttpClient<IImmichClient, ImmichClient>((sp, client) =>
{
    var immich = sp.GetRequiredService<IOptions<ImmichOptions>>().Value;
    client.Timeout = TimeSpan.FromSeconds(30);
    if (immich.IsConfigured)
    {
        client.BaseAddress = new Uri(immich.BaseUrl!.TrimEnd('/') + "/");
        client.DefaultRequestHeaders.Add("x-api-key", immich.ApiKey);
    }
});
builder.Services.AddSingleton(sp => new PhotoCacheStore(
    Path.Combine(builder.Configuration[ConfigKeys.MediaFolder] ?? throw new MissingConfigException(ConfigKeys.MediaFolder), ".photo-cache"),
    sp.GetRequiredService<ILogger<PhotoCacheStore>>()));
builder.Services.AddSingleton(sp => new PhotoLibrary(sp.GetRequiredService<PhotoCacheStore>(), sp.GetRequiredService<TimeProvider>()));
builder.Services.AddSingleton<PhotoSyncTrigger>();
builder.Services.AddScoped<PhotoSyncer>();
builder.Services.AddHostedService<PhotoSyncWorker>();
```

Replace the existing `/api/features` mapping:

```csharp
app.MapGet("/api/features", (IConfiguration config) => new
{
    TemperatureEnabled = !string.IsNullOrEmpty(config["Temperature:ApiBaseUrl"]),
    BloodSugarEnabled = !string.IsNullOrEmpty(config["NIGHTSCOUT_URL"])
});
```

with:

```csharp
app.MapGet("/api/features", (IConfiguration config, PhotoLibrary photos) => new
{
    TemperatureEnabled = FeatureFlags.TemperatureEnabled(config),
    BloodSugarEnabled = FeatureFlags.BloodSugarEnabled(config),
    PhotosEnabled = photos.Count > 0
});
```

Find where `MapHomeSpeakerApi()` is called (`grep -n "MapHomeSpeakerApi" HomeSpeaker.Server2/Program.cs`) and add `app.MapPhotoApi();` on the next line.

- [ ] **Step 8: Document the secrets**

Append to `homespeaker-secrets.env.example`:

```
# --- Immich photo slideshow (ambient screen) ---
# Leave these blank to disable the slideshow; the ambient overlays still show on a dark background.
# ApiKey: Immich web > Account Settings > API Keys. It needs at least asset.read, asset.view,
# album.read and face.read (or "all").
# AlbumId: the GUID at the end of the album's URL.
Immich__BaseUrl=https://immich.example.ts.net
Immich__ApiKey=your-immich-api-key
Immich__AlbumId=00000000-0000-0000-0000-000000000000
# Optional:
# Immich__SyncIntervalMinutes=15
# Immich__SlideSeconds=30
# Ambient__IdleSeconds=120
```

- [ ] **Step 9: Build, test, and smoke-check the endpoints**

Run: `dotnet build HomeSpeaker.sln` and expect 0 warnings and 0 errors.
Run: `dotnet test HomeSpeaker.Server2.Tests` and expect all tests to pass (the 13 existing plus the new ones).
Start the app without any `Immich` config (`dotnet run --project HomeSpeaker.Server2 --launch-profile http`) and check:
- `curl -i http://localhost:5280/api/photos/next` returns `HTTP/1.1 204 No Content`.
- `curl -s http://localhost:5280/api/features` includes `"photosEnabled":false`.
- `curl -i http://localhost:5280/api/photos/..%2Fmanifest.jpg` returns 404.
- The log contains "Immich is not configured; the photo slideshow is disabled".

- [ ] **Step 10: Leave changes uncommitted.**

---

### Task 6: Kiosk detection and glucose tone

**Files:**
- Create: `HomeSpeaker.Server2/Kiosk.cs`
- Create: `HomeSpeaker.Server2/Services/Dashboard/GlucoseTone.cs`
- Test: `HomeSpeaker.Server2.Tests/KioskTests.cs`
- Test: `HomeSpeaker.Server2.Tests/Dashboard/GlucoseToneTests.cs`
- Modify: `HomeSpeaker.Server2/Program.cs`, adding the middleware before `app.UseRouting();`
- Modify: `HomeSpeaker.Server2/Components/App.razor`, adding `data-kiosk` on `<body>`

**Interfaces:**
- Consumes: nothing.
- Produces:
  - `static class Kiosk` with:
    - `const string CookieName = "hs-kiosk"`
    - `bool IsKiosk(HttpRequest)`
    - `void ApplyQueryOverride(HttpContext)`
  - `<body data-kiosk="true|false">`, which Task 8's JS reads.
  - `enum GlucoseTone { Unknown, Normal, Caution, Urgent }` and `static class GlucoseToneMap` with `GlucoseTone From(double? sgv)`.

- [ ] **Step 1: Write the failing tests**

`HomeSpeaker.Server2.Tests/KioskTests.cs`:

```csharp
using Microsoft.AspNetCore.Http;

namespace HomeSpeaker.Server2.Tests;

public sealed class KioskTests
{
    private static DefaultHttpContext context(string host, string? query = null, string? cookie = null)
    {
        var ctx = new DefaultHttpContext();
        ctx.Request.Host = new HostString(host);
        if (query is not null)
        {
            ctx.Request.QueryString = new QueryString(query);
        }

        if (cookie is not null)
        {
            ctx.Request.Headers.Cookie = cookie;
        }

        return ctx;
    }

    [Theory]
    [InlineData("localhost", true)]
    [InlineData("127.0.0.1", true)]
    [InlineData("[::1]", true)]
    [InlineData("kitchen.boston-cloud.ts.net", false)]
    [InlineData("192.168.1.50", false)]
    public void Host_DecidesWhenNoOverride(string host, bool expected) =>
        Assert.Equal(expected, Kiosk.IsKiosk(context(host).Request));

    [Fact]
    public void CookieOne_MakesRemoteHostKiosk() =>
        Assert.True(Kiosk.IsKiosk(context("kitchen.boston-cloud.ts.net", cookie: "hs-kiosk=1").Request));

    [Fact]
    public void CookieZero_OverridesLocalhost() =>
        Assert.False(Kiosk.IsKiosk(context("localhost", cookie: "hs-kiosk=0").Request));

    [Fact]
    public void Query_OverridesCookieAndHost()
    {
        Assert.True(Kiosk.IsKiosk(context("phone.ts.net", "?kiosk=1", "hs-kiosk=0").Request));
        Assert.False(Kiosk.IsKiosk(context("localhost", "?kiosk=0").Request));
    }

    [Fact]
    public void ApplyQueryOverride_SetsCookie()
    {
        var ctx = context("localhost", "?kiosk=0");
        Kiosk.ApplyQueryOverride(ctx);
        Assert.Contains("hs-kiosk=0", ctx.Response.Headers.SetCookie.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void ApplyQueryOverride_WithoutQuery_DoesNothing()
    {
        var ctx = context("localhost");
        Kiosk.ApplyQueryOverride(ctx);
        Assert.Empty(ctx.Response.Headers.SetCookie);
    }
}
```

`HomeSpeaker.Server2.Tests/Dashboard/GlucoseToneTests.cs`:

```csharp
using HomeSpeaker.Server2.Services.Dashboard;

namespace HomeSpeaker.Server2.Tests.Dashboard;

public sealed class GlucoseToneTests
{
    [Theory]
    [InlineData(null, GlucoseTone.Unknown)]
    [InlineData(55.0, GlucoseTone.Urgent)]
    [InlineData(69.0, GlucoseTone.Urgent)]
    [InlineData(70.0, GlucoseTone.Caution)]
    [InlineData(79.0, GlucoseTone.Caution)]
    [InlineData(80.0, GlucoseTone.Normal)]
    [InlineData(180.0, GlucoseTone.Normal)]
    [InlineData(181.0, GlucoseTone.Caution)]
    [InlineData(250.0, GlucoseTone.Caution)]
    [InlineData(251.0, GlucoseTone.Urgent)]
    public void From_MatchesBloodSugarStatusThresholds(double? sgv, GlucoseTone expected) =>
        Assert.Equal(expected, GlucoseToneMap.From(sgv));
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test HomeSpeaker.Server2.Tests --filter "FullyQualifiedName~KioskTests|FullyQualifiedName~GlucoseToneTests"`
Expected: a build failure, because `Kiosk` and `GlucoseToneMap` are not defined. If it instead reports that `DefaultHttpContext`/`Microsoft.AspNetCore.Http` is missing, add `<FrameworkReference Include="Microsoft.AspNetCore.App" />` inside a new `<ItemGroup>` in `HomeSpeaker.Server2.Tests/HomeSpeaker.Server2.Tests.csproj`.

- [ ] **Step 3: Implement**

`HomeSpeaker.Server2/Kiosk.cs`:

```csharp
using System.Net;

namespace HomeSpeaker.Server2;

/// <summary>
/// Decides whether the browser is a wall-mounted kiosk (gets the ambient and night screens).
/// Order of precedence: ?kiosk=1/0 query, then the hs-kiosk cookie, then "is the host loopback".
/// </summary>
public static class Kiosk
{
    public const string CookieName = "hs-kiosk";
    private const string queryKey = "kiosk";

    public static bool IsKiosk(HttpRequest request)
    {
        var query = request.Query[queryKey].ToString();
        if (string.Equals(query, "1", StringComparison.Ordinal))
        {
            return true;
        }

        if (string.Equals(query, "0", StringComparison.Ordinal))
        {
            return false;
        }

        if (request.Cookies.TryGetValue(CookieName, out var cookie))
        {
            return string.Equals(cookie, "1", StringComparison.Ordinal);
        }

        return isLoopbackHost(request.Host.Host);
    }

    public static void ApplyQueryOverride(HttpContext context)
    {
        var query = context.Request.Query[queryKey].ToString();
        if (!string.Equals(query, "1", StringComparison.Ordinal) && !string.Equals(query, "0", StringComparison.Ordinal))
        {
            return;
        }

        context.Response.Cookies.Append(CookieName, query, new CookieOptions
        {
            Path = "/",
            Expires = DateTimeOffset.UtcNow.AddYears(10),
            SameSite = SameSiteMode.Lax,
            IsEssential = true,
        });
    }

    private static bool isLoopbackHost(string host) =>
        string.Equals(host, "localhost", StringComparison.OrdinalIgnoreCase) ||
        (IPAddress.TryParse(host.Trim('[', ']'), out var address) && IPAddress.IsLoopback(address));
}
```

`HomeSpeaker.Server2/Services/Dashboard/GlucoseTone.cs`:

```csharp
namespace HomeSpeaker.Server2.Services.Dashboard;

public enum GlucoseTone
{
    Unknown,
    Normal,
    Caution,
    Urgent,
}

/// <summary>Same thresholds as BloodSugarStatus.StatusColor: red &lt;70 or &gt;250, orange 70-79 and 181-250.</summary>
public static class GlucoseToneMap
{
    public static GlucoseTone From(double? sgv) => sgv switch
    {
        null => GlucoseTone.Unknown,
        < 70 => GlucoseTone.Urgent,
        < 80 => GlucoseTone.Caution,
        <= 180 => GlucoseTone.Normal,
        <= 250 => GlucoseTone.Caution,
        _ => GlucoseTone.Urgent,
    };
}
```

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet test HomeSpeaker.Server2.Tests --filter "FullyQualifiedName~KioskTests|FullyQualifiedName~GlucoseToneTests"`
Expected: 19 passed.

- [ ] **Step 5: Wire the middleware and the body attribute**

In `HomeSpeaker.Server2/Program.cs`, directly before `app.UseRouting();`, add:

```csharp
app.Use(async (context, next) =>
{
    Kiosk.ApplyQueryOverride(context);
    await next(context);
});
```

In `HomeSpeaker.Server2/Components/App.razor`, change `<body>` to:

```razor
<body data-kiosk="@(isKiosk ? "true" : "false")">
```

and add this at the end of the file:

```razor
@code {
    [CascadingParameter]
    private HttpContext? HttpContext { get; set; }

    private bool isKiosk => HttpContext is not null && Kiosk.IsKiosk(HttpContext.Request);
}
```

- [ ] **Step 6: Build and smoke-check**

Run: `dotnet build HomeSpeaker.sln` and expect 0 warnings.
Then run the app with the `http` profile and check:
- `curl -s http://localhost:5280/ | grep -o 'data-kiosk="[a-z]*"'` prints `data-kiosk="true"`, because the host is localhost.
- `curl -s -H "Host: phone.example" http://localhost:5280/ | grep -o 'data-kiosk="[a-z]*"'` prints `data-kiosk="false"`.

- [ ] **Step 7: Leave changes uncommitted.**

---

### Task 7: Now-playing model, DashboardState, and Clock

**Files:**
- Create: `HomeSpeaker.Server2/Services/Dashboard/NowPlayingInfo.cs`
- Create: `HomeSpeaker.Server2/Services/Dashboard/DashboardState.cs`
- Create: `HomeSpeaker.Server2/Components/Glass/Clock.razor`
- Test: `HomeSpeaker.Server2.Tests/Dashboard/NowPlayingInfoTests.cs`
- Modify: `HomeSpeaker.Server2/Program.cs`, registering `DashboardState`
- Modify: `HomeSpeaker.Server2/Components/_Imports.razor` and `HomeSpeaker.Server2/Pages/_Imports.razor`, adding usings

**Interfaces:**
- Consumes:
  - `GlucoseTone` and `GlucoseToneMap` (Task 6)
  - `FeatureFlags` and `PhotoLibrary` (Tasks 4–5)
  - Existing services:
    - `HomeSpeakerService`: `GetStatusAsync`, `GetRepeatModeAsync`, `GetPlayQueueAsync`, `StatusChanged`, `QueueChanged`
    - `PlayerStateService`: `Status`, `RepeatMode`, `Volume`, `UpdateStatus`, `UpdateRepeatMode`, `UpdateVolume`, `StateChanged`
    - `IBloodSugarService.GetBloodSugarStatusAsync`
    - `ITemperatureService.GetTemperatureStatusAsync`
    - `IForecastService.GetForecastStatusAsync`
- Produces:
  - `record NowPlayingInfo(string Title, string? Subtitle, int? SongId, bool IsStream, double Percent, TimeSpan Elapsed, TimeSpan Remaining)` with `static NowPlayingInfo? From(PlayerStatus?)`. `Percent` runs from 0 to 1.
  - Scoped `DashboardState` with:
    - `event Action? Changed`
    - `Task StartAsync()` (idempotent) and `Task RefreshStatusAsync()`
    - `PlayerStatus? Status`, `bool RepeatMode`, `int Volume` and `SongViewModel? UpNext`
    - `BloodSugarStatus? BloodSugar`, `TemperatureStatus? Temperature` and `ForecastStatus? Forecast`
    - `bool BloodSugarEnabled`, `bool TemperatureEnabled` and `bool PhotosEnabled`
    - `GlucoseTone GlucoseTone`
  - `<Clock Format="h:mm" Class="..." />` component, re-rendered every 10s.

- [ ] **Step 1: Write the failing tests**

`HomeSpeaker.Server2.Tests/Dashboard/NowPlayingInfoTests.cs`:

```csharp
using HomeSpeaker.Server2.Services.Dashboard;
using HomeSpeaker.Shared;

namespace HomeSpeaker.Server2.Tests.Dashboard;

public sealed class NowPlayingInfoTests
{
    private static PlayerStatus song(string? artist, string? album) => new()
    {
        StillPlaying = true,
        PercentComplete = 0.38m,
        Elapsed = TimeSpan.FromSeconds(114),
        Remaining = TimeSpan.FromSeconds(188),
        CurrentSong = new Song { SongId = 42, Name = "Clair de Lune", Artist = artist, Album = album },
    };

    [Fact]
    public void From_Null_ReturnsNull() => Assert.Null(NowPlayingInfo.From(null));

    [Fact]
    public void From_NotPlaying_ReturnsNull() => Assert.Null(NowPlayingInfo.From(song("Debussy", null) with { StillPlaying = false }));

    [Fact]
    public void From_Song_JoinsArtistAndAlbum()
    {
        var info = NowPlayingInfo.From(song("Claude Debussy", "Suite bergamasque"))!;

        Assert.Equal("Clair de Lune", info.Title);
        Assert.Equal("Claude Debussy · Suite bergamasque", info.Subtitle);
        Assert.Equal(42, info.SongId);
        Assert.False(info.IsStream);
        Assert.Equal(0.38, info.Percent, 3);
    }

    [Fact]
    public void From_Song_DropsPlaceholderArtistAndAlbum() =>
        Assert.Null(NowPlayingInfo.From(song("[ No Artist ]", "[ No Album ]"))!.Subtitle);

    [Fact]
    public void From_StreamWithIcyTitle_ShowsTrackOverStation()
    {
        var info = NowPlayingInfo.From(new PlayerStatus
        {
            StillPlaying = true,
            IsStream = true,
            StreamName = "Classical KUSC",
            CurrentSong = new Song { Name = "Gymnopédie No. 1" },
        })!;

        Assert.Equal("Gymnopédie No. 1", info.Title);
        Assert.Equal("Classical KUSC", info.Subtitle);
        Assert.Null(info.SongId);
        Assert.True(info.IsStream);
    }

    [Fact]
    public void From_StreamWithoutIcy_ShowsStationAsLive()
    {
        var info = NowPlayingInfo.From(new PlayerStatus { StillPlaying = true, IsStream = true, StreamName = "Classical KUSC" })!;

        Assert.Equal("Classical KUSC", info.Title);
        Assert.Equal("Live radio", info.Subtitle);
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test HomeSpeaker.Server2.Tests --filter "FullyQualifiedName~NowPlayingInfoTests"`
Expected: a build failure, because `NowPlayingInfo` is not defined.

- [ ] **Step 3: Implement `NowPlayingInfo`**

`HomeSpeaker.Server2/Services/Dashboard/NowPlayingInfo.cs`:

```csharp
using HomeSpeaker.Shared;

namespace HomeSpeaker.Server2.Services.Dashboard;

/// <summary>What the overlay and Home page show for the current track or stream.</summary>
public sealed record NowPlayingInfo(string Title, string? Subtitle, int? SongId, bool IsStream, double Percent, TimeSpan Elapsed, TimeSpan Remaining)
{
    public static NowPlayingInfo? From(PlayerStatus? status)
    {
        if (status is not { StillPlaying: true })
        {
            return null;
        }

        var songName = status.CurrentSong?.Name ?? string.Empty;
        if (status.IsStream)
        {
            var station = string.IsNullOrWhiteSpace(status.StreamName) ? "Radio" : status.StreamName;
            var hasIcyTitle = songName.Length > 0 && !string.Equals(songName, station, StringComparison.Ordinal);
            return new NowPlayingInfo(hasIcyTitle ? songName : station, hasIcyTitle ? station : "Live radio", null, true, 0, status.Elapsed, TimeSpan.Zero);
        }

        if (status.CurrentSong is null)
        {
            return null;
        }

        var parts = new[] { clean(status.CurrentSong.Artist, "[ No Artist ]", "[Artist Unknown]"), clean(status.CurrentSong.Album, "[ No Album ]") }
            .Where(p => p is not null);
        var subtitle = string.Join(" · ", parts);
        return new NowPlayingInfo(
            songName,
            subtitle.Length > 0 ? subtitle : null,
            status.CurrentSong.SongId,
            false,
            (double)status.PercentComplete,
            status.Elapsed,
            status.Remaining);
    }

    private static string? clean(string? value, params string[] placeholders) =>
        string.IsNullOrWhiteSpace(value) || placeholders.Contains(value, StringComparer.Ordinal) ? null : value;
}
```

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet test HomeSpeaker.Server2.Tests --filter "FullyQualifiedName~NowPlayingInfoTests"`
Expected: 6 passed.

- [ ] **Step 5: Implement `DashboardState`**

`HomeSpeaker.Server2/Services/Dashboard/DashboardState.cs`:

```csharp
using HomeSpeaker.Server2.Models;
using HomeSpeaker.Server2.Services.Photos;
using HomeSpeaker.Shared;
using HomeSpeaker.Shared.BloodSugar;
using HomeSpeaker.Shared.Forecast;
using HomeSpeaker.Shared.Temperature;

namespace HomeSpeaker.Server2.Services.Dashboard;

/// <summary>
/// One per Blazor circuit: the live data behind the ambient overlay, the night clock and the Home page,
/// so they share one set of timers instead of each polling on its own.
/// </summary>
public sealed class DashboardState : IDisposable
{
    private static readonly TimeSpan statusInterval = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan healthInterval = TimeSpan.FromSeconds(60);

    private readonly HomeSpeakerService homeSpeaker;
    private readonly PlayerStateService playerState;
    private readonly IBloodSugarService bloodSugarService;
    private readonly ITemperatureService temperatureService;
    private readonly IForecastService forecastService;
    private readonly PhotoLibrary photoLibrary;
    private readonly ILogger<DashboardState> logger;
    private readonly SemaphoreSlim statusGate = new(1, 1);
    private Timer? statusTimer;
    private Timer? healthTimer;
    private bool started;
    private bool disposed;

    public DashboardState(
        HomeSpeakerService homeSpeaker,
        PlayerStateService playerState,
        IBloodSugarService bloodSugarService,
        ITemperatureService temperatureService,
        IForecastService forecastService,
        IConfiguration configuration,
        PhotoLibrary photoLibrary,
        ILogger<DashboardState> logger)
    {
        this.homeSpeaker = homeSpeaker;
        this.playerState = playerState;
        this.bloodSugarService = bloodSugarService;
        this.temperatureService = temperatureService;
        this.forecastService = forecastService;
        this.photoLibrary = photoLibrary;
        this.logger = logger;
        BloodSugarEnabled = FeatureFlags.BloodSugarEnabled(configuration);
        TemperatureEnabled = FeatureFlags.TemperatureEnabled(configuration);
    }

    public event Action? Changed;

    public PlayerStatus? Status => playerState.Status;
    public bool RepeatMode => playerState.RepeatMode;
    public int Volume => playerState.Volume;
    public SongViewModel? UpNext { get; private set; }
    public BloodSugarStatus? BloodSugar { get; private set; }
    public TemperatureStatus? Temperature { get; private set; }
    public ForecastStatus? Forecast { get; private set; }
    public bool BloodSugarEnabled { get; }
    public bool TemperatureEnabled { get; }
    public bool PhotosEnabled => photoLibrary.Count > 0;
    public GlucoseTone GlucoseTone => GlucoseToneMap.From(BloodSugar?.CurrentReading?.Sgv);

    public async Task StartAsync()
    {
        if (started || disposed)
        {
            return;
        }

        started = true;
        homeSpeaker.StatusChanged += onPlayerEvent;
        homeSpeaker.QueueChanged += onQueueChanged;
        playerState.StateChanged += onPlayerStateChanged;

        await Task.WhenAll(RefreshStatusAsync(), refreshHealthAsync());

        statusTimer = new Timer(state => _ = RefreshStatusAsync(), null, statusInterval, statusInterval);
        healthTimer = new Timer(state => _ = refreshHealthAsync(), null, healthInterval, healthInterval);
    }

    public async Task RefreshStatusAsync()
    {
        try
        {
            if (disposed || !await statusGate.WaitAsync(0))
            {
                return;
            }
        }
        catch (ObjectDisposedException)
        {
            return;
        }

        try
        {
            var status = await homeSpeaker.GetStatusAsync();
            var repeat = await homeSpeaker.GetRepeatModeAsync();
            UpNext = (await homeSpeaker.GetPlayQueueAsync()).FirstOrDefault();
            playerState.UpdateStatus(status);
            playerState.UpdateRepeatMode(repeat);
            playerState.UpdateVolume(status.Volume);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Dashboard status refresh failed");
        }
        finally
        {
            if (!disposed)
            {
                statusGate.Release();
            }
        }
    }

    public void Dispose()
    {
        disposed = true;
        homeSpeaker.StatusChanged -= onPlayerEvent;
        homeSpeaker.QueueChanged -= onQueueChanged;
        playerState.StateChanged -= onPlayerStateChanged;
        statusTimer?.Dispose();
        healthTimer?.Dispose();
        statusGate.Dispose();
    }

    private async Task refreshHealthAsync()
    {
        if (BloodSugarEnabled)
        {
            BloodSugar = await tryGetAsync(() => bloodSugarService.GetBloodSugarStatusAsync(), "blood sugar") ?? BloodSugar;
        }

        if (TemperatureEnabled)
        {
            Temperature = await tryGetAsync(() => temperatureService.GetTemperatureStatusAsync(), "temperature") ?? Temperature;
        }

        Forecast = await tryGetAsync(() => forecastService.GetForecastStatusAsync(), "forecast") ?? Forecast;

        if (!disposed)
        {
            Changed?.Invoke();
        }
    }

    private async Task<T?> tryGetAsync<T>(Func<Task<T>> fetch, string what)
        where T : class
    {
        try
        {
            return await fetch();
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Dashboard {What} refresh failed", what);
            return null;
        }
    }

    private void onPlayerEvent(string message) => _ = RefreshStatusAsync();

    private void onQueueChanged(object? sender, EventArgs e) => _ = RefreshStatusAsync();

    private void onPlayerStateChanged()
    {
        if (!disposed)
        {
            Changed?.Invoke();
        }
    }
}
```

Register it in `HomeSpeaker.Server2/Program.cs`, directly after `builder.Services.AddSingleton<HomeSpeaker.Server2.Services.PlayerStateService>();`:

```csharp
builder.Services.AddScoped<HomeSpeaker.Server2.Services.Dashboard.DashboardState>();
```

- [ ] **Step 6: Create the Clock component and the imports**

`HomeSpeaker.Server2/Components/Glass/Clock.razor`:

```razor
@using System.Globalization
@inject TimeProvider TimeProvider
@implements IDisposable

<span class="@Class">@TimeProvider.GetLocalNow().ToString(Format, CultureInfo.InvariantCulture)</span>

@code {
    [Parameter] public string Format { get; set; } = "h:mm";
    [Parameter] public string? Class { get; set; }

    private Timer? timer;

    protected override void OnInitialized() =>
        timer = new Timer(state => _ = InvokeAsync(StateHasChanged), null, TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(10));

    public void Dispose() => timer?.Dispose();
}
```

Add these lines to both `HomeSpeaker.Server2/Components/_Imports.razor` and `HomeSpeaker.Server2/Pages/_Imports.razor`, after `@using HomeSpeaker.Server2.Components.UI`:

```razor
@using HomeSpeaker.Server2.Components.Glass
@using HomeSpeaker.Server2.Services.Dashboard
@using HomeSpeaker.Server2.Services.Photos
@using Microsoft.Extensions.Options
```

- [ ] **Step 7: Build and test**

Run: `dotnet build HomeSpeaker.sln` and expect 0 warnings.
Run: `dotnet test HomeSpeaker.Server2.Tests` and expect all tests to pass.

- [ ] **Step 8: Leave changes uncommitted.**

---

### Task 8: Glass shell and idle state machine

**Files:**
- Create: `HomeSpeaker.Server2/wwwroot/css/glass.css`
- Create: `HomeSpeaker.Server2/wwwroot/js/idleController.js`
- Create: `HomeSpeaker.Server2/Components/Glass/AppMenu.razor`
- Rewrite: `HomeSpeaker.Server2/Components/Layout/MainLayout.razor`
- Modify: `HomeSpeaker.Server2/Components/App.razor` (font link, stylesheet, script)
- Delete: `HomeSpeaker.Server2/wwwroot/js/sleepyTime.js`
- Delete: `HomeSpeaker.Server2/Components/Layout/MainLayout.razor.css`

**Interfaces:**
- Consumes:
  - `Clock` and `DashboardState` (Task 7)
  - `QuietHours.IsQuietTime(TimeProvider)`
  - the `body[data-kiosk]` attribute (Task 6)
  - existing `circuitWatchdog.beat` and `homeSpeakerKeyboard.init`
- Produces:
  - JS global `window.idleController`. Its methods:
    - `init({ idleSeconds })`
    - `setQuiet(bool)`
    - `enter('ambient' | 'night')`
    - `mode()`, which returns `'active' | 'ambient' | 'night'`
    - `isKiosk()`
    - `onModeChange(callback)`
  - Root classes `html.hs-mode-ambient` and `html.hs-mode-night`.
  - Shell DOM and classes that later tasks fill in or style:
    - `.hs-backdrop`, which uses CSS var `--hs-photo`
    - `.hs-shell`, `.hs-topbar`, `.hs-main`
    - `.glass`, `.glass-pill`, `.glass-panel`, `.kiosk-only`
    - the empty layers `<div class="hs-ambient">` and `<div class="hs-night">`

- [ ] **Step 1: Write the stylesheet**

`HomeSpeaker.Server2/wwwroot/css/glass.css`:

```css
/* Glass design system for the HomeSpeaker shell (design "A · Glass").
   Sizes are px on purpose: app.css sets html font-size to 24px at 1024x600 for legacy pages. */

:root {
  --glass-bg: rgba(22, 22, 26, 0.5);
  --glass-border: rgba(255, 255, 255, 0.14);
  --glass-blur: blur(24px) saturate(150%);
  --glass-text: #ffffff;
  --glass-tone-caution: rgba(150, 84, 8, 0.92);
  --glass-tone-urgent: rgba(150, 24, 36, 0.94);
  --glass-radius-card: 28px;
  --glass-radius-tile: 20px;
  --glass-radius-pill: 22px;
  --glass-font: 'Manrope', system-ui, sans-serif;
  --hs-photo: none;
}

.glass {
  background: var(--glass-bg);
  -webkit-backdrop-filter: var(--glass-blur);
  backdrop-filter: var(--glass-blur);
  border: 1px solid var(--glass-border);
  color: var(--glass-text);
}

.glass-pill {
  display: inline-flex;
  align-items: center;
  gap: 8px;
  height: 44px;
  min-width: 44px;
  padding: 0 16px;
  box-sizing: border-box;
  border-radius: var(--glass-radius-pill);
  font: 700 15px var(--glass-font);
  text-decoration: none;
  cursor: pointer;
}

.glass-pill:hover,
.glass-pill:focus {
  color: var(--glass-text);
}

body[data-kiosk="false"] .kiosk-only {
  display: none !important;
}

/* ---------- Shell ---------- */

.hs-backdrop {
  position: fixed;
  inset: -60px;
  z-index: 0;
  background: #151515 var(--hs-photo) center / cover no-repeat;
  filter: blur(40px) brightness(0.6);
}

.hs-shell {
  position: relative;
  z-index: 1;
  display: flex;
  flex-direction: column;
  gap: 12px;
  height: 100dvh;
  padding: 12px 24px 16px;
  box-sizing: border-box;
  font-family: var(--glass-font);
  color: var(--glass-text);
}

.hs-topbar {
  flex: none;
  display: flex;
  align-items: center;
  gap: 12px;
}

.hs-topbar-spacer {
  flex: 1;
}

.hs-topbar-clock {
  min-width: 72px;
  text-align: right;
  font: 800 20px var(--glass-font);
}

.hs-main {
  flex: 1;
  min-height: 0;
  overflow: auto;
}

.hs-main.glass-panel {
  border-radius: var(--glass-radius-card);
  padding: 16px;
}

/* ---------- App menu ---------- */

.hs-menu-scrim {
  position: fixed;
  inset: 0;
  z-index: 50;
  display: flex;
  justify-content: flex-end;
  background: rgba(0, 0, 0, 0.45);
}

.hs-menu {
  width: min(360px, 100vw);
  height: 100%;
  overflow: auto;
  box-sizing: border-box;
  padding: 20px;
  display: flex;
  flex-direction: column;
  gap: 4px;
  border-radius: 28px 0 0 28px;
  font-family: var(--glass-font);
}

.hs-menu a {
  display: flex;
  align-items: center;
  gap: 14px;
  min-height: 52px;
  padding: 0 14px;
  border-radius: 16px;
  color: var(--glass-text);
  text-decoration: none;
  font-size: 17px;
  font-weight: 600;
}

.hs-menu a i {
  width: 22px;
  text-align: center;
  opacity: 0.85;
}

.hs-menu a.active,
.hs-menu a:hover {
  background: rgba(255, 255, 255, 0.12);
  color: var(--glass-text);
}

/* ---------- Ambient / night layers (content added in Task 9) ---------- */

.hs-ambient,
.hs-night {
  position: fixed;
  inset: 0;
  z-index: 100;
  display: none;
  overflow: hidden;
  font-family: var(--glass-font);
  color: #fff;
}

.hs-ambient {
  background: #111;
}

.hs-night {
  background: #000;
  align-items: center;
  justify-content: center;
}

html.hs-mode-ambient .hs-ambient {
  display: block;
}

html.hs-mode-night .hs-night {
  display: flex;
}

/* Don't paint the blurred backdrop underneath a full-screen layer (saves the Pi's GPU). */
html.hs-mode-ambient .hs-backdrop,
html.hs-mode-night .hs-backdrop {
  display: none;
}

@media (max-width: 700px) {
  .hs-shell {
    padding: 8px 12px 12px;
  }
}
```

- [ ] **Step 2: Write the idle controller**

`HomeSpeaker.Server2/wwwroot/js/idleController.js`:

```javascript
// idleController.js - kiosk screen modes: 'active' (normal UI), 'ambient' (photo slideshow),
// 'night' (dim clock). Pure DOM, so it keeps working even if the Blazor circuit drops.
// Only browsers marked <body data-kiosk="true"> ever leave 'active' on their own.
window.idleController = (function () {
    const nightIdleMs = 60000;
    let mode = 'active';
    let quiet = false;
    let idleMs = 120000;
    let timer = null;
    let swallowClick = false;
    const listeners = [];

    function isKiosk() {
        return document.body && document.body.dataset.kiosk === 'true';
    }

    function setMode(next) {
        if (next === mode) return;
        mode = next;
        const root = document.documentElement;
        root.classList.toggle('hs-mode-ambient', mode === 'ambient');
        root.classList.toggle('hs-mode-night', mode === 'night');
        listeners.forEach(function (callback) {
            try { callback(mode); } catch (e) { console.error('idleController listener failed', e); }
        });
    }

    function arm() {
        clearTimeout(timer);
        if (!isKiosk()) return;
        timer = setTimeout(function () {
            setMode(quiet ? 'night' : 'ambient');
        }, quiet ? nightIdleMs : idleMs);
    }

    // The tap that wakes the screen must not also press whatever control is underneath.
    function onActivity(e) {
        if (mode !== 'active') {
            e.preventDefault();
            e.stopPropagation();
            if (e.type === 'pointerdown') {
                swallowClick = true;
                setTimeout(function () { swallowClick = false; }, 700);
            }
            setMode('active');
        }
        arm();
    }

    function onClick(e) {
        if (swallowClick) {
            swallowClick = false;
            e.preventDefault();
            e.stopPropagation();
        }
    }

    document.addEventListener('pointerdown', onActivity, true);
    document.addEventListener('keydown', onActivity, true);
    document.addEventListener('click', onClick, true);

    return {
        init: function (options) {
            idleMs = Math.max(10, (options && options.idleSeconds) || 120) * 1000;
            arm();
        },
        setQuiet: function (isQuiet) {
            if (isQuiet === quiet) return;
            quiet = isQuiet;
            if (mode === 'ambient' && quiet) setMode('night');
            else if (mode === 'night' && !quiet) setMode('ambient');
            arm();
        },
        enter: function (requested) {
            setMode(requested === 'night' ? 'night' : 'ambient');
        },
        mode: function () { return mode; },
        isKiosk: isKiosk,
        onModeChange: function (callback) { listeners.push(callback); }
    };
})();
```

- [ ] **Step 3: Write the app menu**

`HomeSpeaker.Server2/Components/Glass/AppMenu.razor`:

```razor
@if (Open)
{
    <div class="hs-menu-scrim" @onclick="OnClose">
        <nav class="hs-menu glass" aria-label="All pages" @onclick:stopPropagation="true">
            @foreach (var item in items)
            {
                <NavLink href="@item.Href" Match="@(item.Href.Length == 0 ? NavLinkMatch.All : NavLinkMatch.Prefix)">
                    <i class="@item.Icon" aria-hidden="true"></i><span>@item.Label</span>
                </NavLink>
            }
        </nav>
    </div>
}

@code {
    [Parameter] public bool Open { get; set; }
    [Parameter] public EventCallback OnClose { get; set; }

    private sealed record MenuItem(string Label, string Href, string Icon);

    private static readonly MenuItem[] items =
    [
        new("Home", "", "fas fa-home"),
        new("Music", "music", "fas fa-music"),
        new("Queue", "queue", "fas fa-stream"),
        new("Playlists", "playlists", "fas fa-list-ul"),
        new("Folders", "folders", "fas fa-folder"),
        new("Recently played", "recently-played", "fas fa-history"),
        new("Streams", "streams", "fas fa-podcast"),
        new("YouTube", "youtube", "fab fa-youtube"),
        new("AI Playlists", "ai-playlists", "fas fa-brain"),
        new("AI Status", "ai-status", "fas fa-microchip"),
        new("Autoplay", "autoplay", "fas fa-clock"),
        new("Anchors", "anchors", "fas fa-anchor"),
        new("NightScout", "nightscout", "fas fa-heartbeat"),
        new("Climate", "climate", "fas fa-temperature-half"),
        new("Aspire", "aspire", "fas fa-chart-line"),
    ];
}
```

The menu closes on navigation because `MainLayout` resets `menuOpen` in its `LocationChanged` handler (Step 4).

- [ ] **Step 4: Rewrite `MainLayout.razor`**

Replace the whole of `HomeSpeaker.Server2/Components/Layout/MainLayout.razor` with the following. It keeps every `[JSInvokable]` keyboard-shortcut method and the watchdog heartbeat, drops the sidebar, bottom nav, mobile menu and sleepy overlay, and adds the glass shell.

```razor
@inherits LayoutComponentBase
@inject IJSRuntime JSRuntime
@inject HomeSpeakerService HomeSpeaker
@inject DashboardState Dashboard
@inject NavigationManager Navigation
@inject IConfiguration Configuration
@inject ILogger<MainLayout> Logger
@inject TimeProvider TimeProvider
@implements IAsyncDisposable

<div class="hs-backdrop" aria-hidden="true"></div>

<div class="hs-shell">
    <header class="hs-topbar">
        @if (isHome)
        {
            <button type="button" class="glass glass-pill kiosk-only" @onclick="ShowPhotosAsync">
                <svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.2" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><path d="M15 18l-6-6 6-6"></path></svg>
                Photos
            </button>
        }
        else
        {
            <a href="" class="glass glass-pill">
                <svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.2" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><path d="M15 18l-6-6 6-6"></path></svg>
                Home
            </a>
        }
        <div class="hs-topbar-spacer"></div>
        <button type="button" class="glass glass-pill" aria-label="All pages" @onclick="ToggleMenu">
            <svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.2" stroke-linecap="round" aria-hidden="true"><path d="M4 6h16M4 12h16M4 18h16"></path></svg>
        </button>
        <Clock Format="h:mm" Class="hs-topbar-clock" />
    </header>

    <main class="hs-main @(isHome ? "hs-main--home" : "glass glass-panel")">
        @Body
    </main>
</div>

<AppMenu Open="menuOpen" OnClose="CloseMenu" />

<div class="hs-ambient" aria-hidden="true"></div>
<div class="hs-night" aria-hidden="true"></div>

<MudThemeProvider />
<MudDialogProvider />
<MudSnackbarProvider />

@code {
    private DotNetObjectReference<MainLayout>? dotNetHelper;
    private Timer? windowTimer;
    private bool menuOpen;
    private bool isHome;

    protected override void OnInitialized()
    {
        isHome = computeIsHome(Navigation.Uri);
        Navigation.LocationChanged += onLocationChanged;
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender)
        {
            return;
        }

        try
        {
            dotNetHelper = DotNetObjectReference.Create(this);
            await JSRuntime.InvokeVoidAsync("homeSpeakerKeyboard.init", dotNetHelper);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error initializing keyboard shortcuts");
        }

        try
        {
            await JSRuntime.InvokeVoidAsync("idleController.init", new { idleSeconds = Configuration.GetValue("Ambient:IdleSeconds", 120) });
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error initializing the idle controller");
        }

        await Dashboard.StartAsync();

        // Every minute: heartbeat for the kiosk circuit watchdog in App.razor, and tell the
        // idle controller whether quiet hours apply (night clock instead of slideshow).
        windowTimer = new Timer(async state =>
        {
            try
            {
                await JSRuntime.InvokeVoidAsync("circuitWatchdog.beat");
                await JSRuntime.InvokeVoidAsync("idleController.setQuiet", QuietHours.IsQuietTime(TimeProvider));
            }
            catch (Exception ex) when (ex is JSDisconnectedException or JSException or TaskCanceledException or ObjectDisposedException)
            {
                // Circuit is going away; the watchdog will notice the missing beat.
            }
        }, null, TimeSpan.FromSeconds(1), TimeSpan.FromMinutes(1));
    }

    private void ToggleMenu() => menuOpen = !menuOpen;

    private void CloseMenu() => menuOpen = false;

    private async Task ShowPhotosAsync()
    {
        try
        {
            await JSRuntime.InvokeVoidAsync("idleController.enter", "ambient");
        }
        catch (JSException ex)
        {
            Logger.LogWarning(ex, "Could not enter ambient mode");
        }
    }

    private void onLocationChanged(object? sender, LocationChangedEventArgs e)
    {
        isHome = computeIsHome(e.Location);
        menuOpen = false;
        _ = InvokeAsync(StateHasChanged);
    }

    private bool computeIsHome(string uri) =>
        Navigation.ToBaseRelativePath(uri).Split('?', '#')[0].Length == 0;

    [JSInvokable]
    public async Task OnPlayPause()
    {
        try
        {
            var status = await HomeSpeaker.GetStatusAsync();
            if (status.StillPlaying)
            {
                await HomeSpeaker.StopPlayingAsync();
            }
            else
            {
                await HomeSpeaker.ResumePlayAsync();
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error toggling play/pause");
        }
    }

    [JSInvokable]
    public async Task OnSkipForward()
    {
        try
        {
            await HomeSpeaker.SkipToNextAsync();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error skipping forward");
        }
    }

    [JSInvokable]
    public Task OnSkipBack()
    {
        // Not implemented yet - could restart current song
        Logger.LogInformation("Skip back requested (not implemented)");
        return Task.CompletedTask;
    }

    [JSInvokable]
    public async Task OnStop()
    {
        try
        {
            await HomeSpeaker.StopPlayingAsync();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error stopping");
        }
    }

    [JSInvokable]
    public async Task OnToggleRepeat()
    {
        try
        {
            var current = await HomeSpeaker.GetRepeatModeAsync();
            await HomeSpeaker.SetRepeatModeAsync(!current);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error toggling repeat");
        }
    }

    [JSInvokable]
    public async Task OnVolumeUp()
    {
        try
        {
            var currentVolume = await HomeSpeaker.GetVolumeAsync();
            await HomeSpeaker.SetVolumeAsync(Math.Min(100, currentVolume + 5));
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error increasing volume");
        }
    }

    [JSInvokable]
    public async Task OnVolumeDown()
    {
        try
        {
            var currentVolume = await HomeSpeaker.GetVolumeAsync();
            await HomeSpeaker.SetVolumeAsync(Math.Max(0, currentVolume - 5));
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error decreasing volume");
        }
    }

    public ValueTask DisposeAsync()
    {
        Navigation.LocationChanged -= onLocationChanged;
        windowTimer?.Dispose();
        dotNetHelper?.Dispose();
        return ValueTask.CompletedTask;
    }
}
```

If the analyzers flag the former `async` methods without `await` (`OnSkipBack`), the version above already returns `Task.CompletedTask`. Fix any other analyzer warning in code.

- [ ] **Step 5: Update `App.razor` and delete the old files**

In `HomeSpeaker.Server2/Components/App.razor`:
1. Replace the Google Fonts `<link>` (the line containing `family=Inter`) with:
   ```html
   <link href="https://fonts.googleapis.com/css2?family=Inter:wght@300;400;500;600;700&family=Manrope:wght@400;500;600;700;800&family=Poppins:wght@400;500;600;700;800&display=swap" rel="stylesheet" />
   ```
2. Directly after `<link href="css/app.css" rel="stylesheet" />`, add `<link href="css/glass.css" rel="stylesheet" />`.
3. Replace `<script src="js/sleepyTime.js"></script>` with `<script src="js/idleController.js"></script>`.

Delete `HomeSpeaker.Server2/wwwroot/js/sleepyTime.js` and `HomeSpeaker.Server2/Components/Layout/MainLayout.razor.css`.

Leave `NavMenu.razor` alone: `NoTopLayout` still uses it, and it is restyled in sub-project 3.

- [ ] **Step 6: Build and test**

Run: `dotnet build HomeSpeaker.sln` and expect 0 warnings.
Run: `dotnet test HomeSpeaker.Server2.Tests` and expect all tests to pass.
Run: `grep -rn "sleepyTime\|sleepy-overlay" HomeSpeaker.Server2 --include=*.razor --include=*.js --include=*.css --include=*.cs` and expect no matches outside `bin/` and `obj/`.

- [ ] **Step 7: Verify in the browser**

Start the app with a short idle time:
- PowerShell: `$env:Ambient__IdleSeconds='15'; dotnet run --project HomeSpeaker.Server2 --launch-profile https`
- bash: `Ambient__IdleSeconds=15 dotnet run --project HomeSpeaker.Server2 --launch-profile https`

Open https://localhost:7238 at a 1024×600 viewport, and check:
- The glass top bar shows "‹ Photos" (on Home), the menu button and the clock. There is no sidebar and no bottom nav.
- The menu button opens the sheet, choosing "Queue" navigates and closes it, and the top-left now reads "‹ Home".
- After 15s idle, `document.documentElement.classList` contains `hs-mode-ambient` and the screen is near-black (`#111`).
- Tapping wakes it, and the tap does **not** activate the button underneath. Test this by idling with the pointer over the menu button and confirming the menu does not open.
- "‹ Photos" enters ambient immediately.
- `idleController.setQuiet(true)` in the console moves ambient to night (`#000`).
- Opened as `https://127.0.0.1:7238/?kiosk=0`, the page never goes ambient and the Photos button is hidden.
- Legacy pages (Music, Queue, Streams) still work inside the glass panel.

- [ ] **Step 8: Leave changes uncommitted.**

---

### Task 9: Ambient slideshow, overlays, and night clock

**Files:**
- Create: `HomeSpeaker.Server2/wwwroot/js/ambientSlideshow.js`
- Create: `HomeSpeaker.Server2/Components/Glass/AmbientOverlay.razor`
- Create: `HomeSpeaker.Server2/Components/Glass/NightClock.razor`
- Modify: `HomeSpeaker.Server2/wwwroot/css/glass.css` (append the ambient and night section)
- Modify: `HomeSpeaker.Server2/Components/Layout/MainLayout.razor` (fill the layers, init the slideshow)
- Modify: `HomeSpeaker.Server2/Components/App.razor` (script tag)

**Interfaces:**
- Consumes:
  - `GET /api/photos/next` (Task 5)
  - `DashboardState`, `NowPlayingInfo`, `GlucoseTone` and `Clock` (Tasks 6–7)
  - `window.idleController.onModeChange` (Task 8)
  - `IOptions<ImmichOptions>` (Task 1)
- Produces:
  - JS global `window.ambientSlideshow` with `init({ slideSeconds })`. On every page load it loads one photo for the backdrop. While in ambient mode it advances every `slideSeconds`.
  - It sets `--hs-photo`, the `data-layout` attribute on `.hs-ambient-overlay`, and the text of `.hs-caption-place` and `.hs-caption-when`.

- [ ] **Step 1: Write the slideshow script**

`HomeSpeaker.Server2/wwwroot/js/ambientSlideshow.js`:

```javascript
// ambientSlideshow.js - crossfades between two <img class="hs-slide"> layers inside .hs-ambient.
// Photos come from /api/photos/next. Advances only while idleController is in 'ambient' mode;
// on any page load it fetches one photo so the shell's blurred backdrop has something to show.
window.ambientSlideshow = (function () {
    let slideMs = 30000;
    let timer = null;
    let front = 0;
    let busy = false;

    function setText(selector, text) {
        const el = document.querySelector(selector);
        if (el) el.textContent = text;
    }

    function clearPhotos() {
        document.querySelectorAll('.hs-slide').forEach(function (img) { img.classList.remove('is-visible'); });
        document.documentElement.style.setProperty('--hs-photo', 'none');
        setText('.hs-caption-place', '');
        setText('.hs-caption-when', '');
    }

    async function advance() {
        if (busy) return;
        busy = true;
        try {
            const resp = await fetch('/api/photos/next', { cache: 'no-store' });
            if (resp.status === 204) { clearPhotos(); return; }
            if (!resp.ok) return; // keep the current photo; try again next tick
            const photo = await resp.json();

            const slides = document.querySelectorAll('.hs-slide');
            if (slides.length < 2) return;
            const incoming = slides[1 - front];
            incoming.src = photo.url;
            await incoming.decode();

            document.documentElement.style.setProperty('--hs-photo', 'url("' + photo.url + '")');
            const overlay = document.querySelector('.hs-ambient-overlay');
            if (overlay) overlay.dataset.layout = photo.layout || 'default';
            setText('.hs-caption-place', photo.place || '');
            setText('.hs-caption-when', photo.when || '');

            incoming.classList.add('is-visible');
            slides[front].classList.remove('is-visible');
            front = 1 - front;
        } catch (e) {
            console.debug('ambientSlideshow: keeping current photo', e);
        } finally {
            busy = false;
        }
    }

    function onModeChange(mode) {
        clearInterval(timer);
        timer = null;
        if (mode === 'ambient') {
            advance();
            timer = setInterval(advance, slideMs);
        }
    }

    return {
        init: function (options) {
            slideMs = Math.max(5, (options && options.slideSeconds) || 30) * 1000;
            advance();
            window.idleController.onModeChange(onModeChange);
        }
    };
})();
```

In `HomeSpeaker.Server2/Components/App.razor`, add `<script src="js/ambientSlideshow.js"></script>` directly after the `idleController.js` script tag.

- [ ] **Step 2: Write the overlay component**

`HomeSpeaker.Server2/Components/Glass/AmbientOverlay.razor`:

```razor
@using System.Globalization
@inject DashboardState Dashboard
@implements IDisposable

<div class="hs-ambient-overlay" data-layout="default">
    <div class="hs-ov-clock">
        <Clock Format="h:mm" Class="hs-ov-time" />
        <Clock Format="dddd, MMMM d" Class="hs-ov-date" />
    </div>

    <div class="hs-ov-chips">
        @if (Dashboard.BloodSugarEnabled && Dashboard.BloodSugar?.CurrentReading is { } reading)
        {
            <div class="glass hs-chip hs-tone-@toneClass">
                <svg width="26" height="26" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><path d="M12 3c3 4 6 7.5 6 11a6 6 0 0 1-12 0c0-3.5 3-7 6-11z"></path></svg>
                <span class="hs-chip-value @(Dashboard.BloodSugar.IsStale ? "is-stale" : "")">@reading.Sgv.ToString("0", CultureInfo.InvariantCulture)</span>
                <span class="hs-chip-arrow" title="@reading.DirectionDescription">@reading.DirectionIcon</span>
                <span class="hs-chip-meta"><span>mg/dL</span><span>@age(Dashboard.BloodSugar.TimeSinceLastReading)</span></span>
            </div>
        }
        @if (Dashboard.TemperatureEnabled && Dashboard.Temperature?.OutsideTemperature is { } outside)
        {
            <div class="glass hs-chip">
                <svg width="26" height="26" viewBox="0 0 24 24" fill="none" stroke="#ffd27a" stroke-width="2" stroke-linecap="round" aria-hidden="true"><circle cx="12" cy="12" r="4"></circle><path d="M12 2v2M12 20v2M4.9 4.9l1.4 1.4M17.7 17.7l1.4 1.4M2 12h2M20 12h2M4.9 19.1l1.4-1.4M17.7 6.3l1.4-1.4"></path></svg>
                <span class="hs-chip-value">@outside.ToString("0", CultureInfo.InvariantCulture)°</span>
                <span class="hs-chip-meta">
                    @if (highLow is not null)
                    {
                        <span>@highLow</span>
                    }
                    @if (conditions is not null)
                    {
                        <span>@conditions</span>
                    }
                </span>
            </div>
        }
    </div>

    @if (NowPlayingInfo.From(Dashboard.Status) is { } np)
    {
        <div class="glass hs-ov-playing">
            @if (np.SongId is { } songId)
            {
                <div class="hs-art" style="background-image:url('/api/homespeaker/songs/@songId/art')"></div>
            }
            else
            {
                <div class="hs-art hs-art--radio">
                    <svg width="28" height="28" viewBox="0 0 24 24" fill="none" stroke="#fff" stroke-width="2" stroke-linecap="round" aria-hidden="true"><circle cx="12" cy="12" r="2"></circle><path d="M16.2 7.8a6 6 0 0 1 0 8.4M7.8 16.2a6 6 0 0 1 0-8.4M19 5a10 10 0 0 1 0 14M5 19A10 10 0 0 1 5 5"></path></svg>
                </div>
            }
            <div class="hs-ov-playing-text">
                <div class="hs-ov-title">@np.Title</div>
                @if (np.Subtitle is not null)
                {
                    <div class="hs-ov-sub">@np.Subtitle</div>
                }
                @if (!np.IsStream)
                {
                    <div class="hs-progress"><div style="width:@((np.Percent * 100).ToString("F1", CultureInfo.InvariantCulture))%"></div></div>
                }
            </div>
        </div>
    }

    @* Filled in by ambientSlideshow.js; Blazor never binds these, so it never overwrites them. *@
    <div class="hs-ov-caption"><div class="hs-caption-place"></div><div class="hs-caption-when"></div></div>
</div>

@code {
    private string toneClass => Dashboard.GlucoseTone switch
    {
        GlucoseTone.Caution => "caution",
        GlucoseTone.Urgent => "urgent",
        _ => "normal",
    };

    private string? highLow =>
        Dashboard.Forecast is { Error: null, TodayHigh.Temperature: { } high, TonightLow.Temperature: { } low }
            ? string.Create(CultureInfo.InvariantCulture, $"H {high:0}° · L {low:0}°")
            : null;

    private string? conditions => Dashboard.Forecast is { Error: null } ? Dashboard.Forecast.TodayHigh?.Conditions : null;

    protected override void OnInitialized() => Dashboard.Changed += onChanged;

    private void onChanged() => _ = InvokeAsync(StateHasChanged);

    private static string age(TimeSpan sinceReading) =>
        sinceReading.TotalMinutes < 1 ? "just now" : string.Create(CultureInfo.InvariantCulture, $"{(int)sinceReading.TotalMinutes} min ago");

    public void Dispose() => Dashboard.Changed -= onChanged;
}
```

- [ ] **Step 3: Write the night clock**

`HomeSpeaker.Server2/Components/Glass/NightClock.razor`:

```razor
@using System.Globalization
@inject DashboardState Dashboard
@implements IDisposable

<div class="hs-night-inner">
    <Clock Format="h:mm" Class="hs-night-time" />
    @if (Dashboard.BloodSugarEnabled && Dashboard.BloodSugar?.CurrentReading is { } reading)
    {
        <div class="hs-night-glucose hs-night-tone-@toneClass">
            @reading.Sgv.ToString("0", CultureInfo.InvariantCulture) <span>@reading.DirectionIcon</span>
        </div>
    }
</div>

@code {
    private string toneClass => Dashboard.GlucoseTone switch
    {
        GlucoseTone.Caution => "caution",
        GlucoseTone.Urgent => "urgent",
        _ => "normal",
    };

    protected override void OnInitialized() => Dashboard.Changed += onChanged;

    private void onChanged() => _ = InvokeAsync(StateHasChanged);

    public void Dispose() => Dashboard.Changed -= onChanged;
}
```

- [ ] **Step 4: Fill the layers in `MainLayout.razor`**

Add `@inject IOptions<ImmichOptions> ImmichOptions` to the inject list at the top.

Replace:

```razor
<div class="hs-ambient" aria-hidden="true"></div>
<div class="hs-night" aria-hidden="true"></div>
```

with:

```razor
<div class="hs-ambient" aria-hidden="true">
    <img class="hs-slide" alt="" />
    <img class="hs-slide" alt="" />
    <div class="hs-ambient-scrim"></div>
    <AmbientOverlay />
</div>
<div class="hs-night" aria-hidden="true">
    <NightClock />
</div>
```

In `OnAfterRenderAsync`, inside the `try` that calls `idleController.init`, add this line after that call:

```csharp
await JSRuntime.InvokeVoidAsync("ambientSlideshow.init", new { slideSeconds = ImmichOptions.Value.SlideSeconds });
```

- [ ] **Step 5: Append the ambient and night styles**

Append to `HomeSpeaker.Server2/wwwroot/css/glass.css`:

```css
/* ---------- Ambient slideshow + overlays ---------- */

.hs-slide {
  position: absolute;
  inset: 0;
  width: 100%;
  height: 100%;
  object-fit: cover;
  opacity: 0;
  transition: opacity 1.5s ease;
}

.hs-slide.is-visible {
  opacity: 1;
}

.hs-ambient-scrim {
  position: absolute;
  inset: 0;
  pointer-events: none;
  background:
    linear-gradient(180deg, rgba(0, 0, 0, 0.45) 0, rgba(0, 0, 0, 0) 34%),
    linear-gradient(0deg, rgba(0, 0, 0, 0.55) 0, rgba(0, 0, 0, 0) 38%);
}

.hs-ambient-overlay {
  position: absolute;
  inset: 0;
}

.hs-ov-clock {
  position: absolute;
  left: 40px;
  top: 28px;
  display: flex;
  flex-direction: column;
  gap: 2px;
  text-shadow: 0 2px 12px rgba(0, 0, 0, 0.35);
}

.hs-ov-time {
  font-size: 104px;
  font-weight: 700;
  letter-spacing: -4px;
  line-height: 1;
}

.hs-ov-date {
  font-size: 22px;
  font-weight: 600;
  opacity: 0.92;
}

.hs-ov-chips {
  position: absolute;
  right: 32px;
  top: 32px;
  display: flex;
  flex-direction: column;
  gap: 12px;
  align-items: flex-end;
}

.hs-chip {
  border-radius: 22px;
  padding: 12px 18px;
  display: flex;
  align-items: center;
  gap: 12px;
}

.hs-chip-value {
  font-size: 40px;
  font-weight: 800;
  letter-spacing: -1px;
}

.hs-chip-value.is-stale {
  opacity: 0.55;
}

.hs-chip-arrow {
  font-size: 24px;
  font-weight: 700;
}

.hs-chip-meta {
  display: flex;
  flex-direction: column;
  font-size: 13px;
  font-weight: 600;
  line-height: 1.3;
  opacity: 0.85;
}

.hs-tone-caution {
  background: var(--glass-tone-caution) !important;
}

.hs-tone-urgent {
  background: var(--glass-tone-urgent) !important;
}

.hs-ov-playing {
  position: absolute;
  left: 32px;
  bottom: 32px;
  width: 520px;
  max-width: calc(100% - 64px);
  box-sizing: border-box;
  border-radius: 24px;
  padding: 12px;
  display: flex;
  align-items: center;
  gap: 16px;
}

.hs-art {
  width: 72px;
  height: 72px;
  flex: none;
  border-radius: 14px;
  background-color: #2b3d6b;
  background-size: cover;
  background-position: center;
}

.hs-art--radio {
  display: grid;
  place-items: center;
  background-image: linear-gradient(135deg, #2b3d6b, #a75f8a);
}

.hs-ov-playing-text {
  flex: 1;
  min-width: 0;
  display: flex;
  flex-direction: column;
  gap: 6px;
}

.hs-ov-title,
.hs-ov-sub {
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
}

.hs-ov-title {
  font-size: 20px;
  font-weight: 700;
}

.hs-ov-sub {
  font-size: 15px;
  font-weight: 500;
  opacity: 0.85;
}

.hs-progress {
  height: 4px;
  border-radius: 2px;
  background: rgba(255, 255, 255, 0.25);
  overflow: hidden;
}

.hs-progress > div {
  height: 100%;
  border-radius: 2px;
  background: #fff;
}

.hs-ov-caption {
  position: absolute;
  right: 32px;
  bottom: 36px;
  text-align: right;
  font-size: 14px;
  font-weight: 600;
  line-height: 1.4;
  text-shadow: 0 1px 6px rgba(0, 0, 0, 0.6);
}

.hs-caption-when {
  opacity: 0.85;
}

/* Mirrored layout: chosen per photo so the overlays don't cover faces. */
.hs-ambient-overlay[data-layout="mirrored"] .hs-ov-clock {
  left: auto;
  right: 40px;
  align-items: flex-end;
}

.hs-ambient-overlay[data-layout="mirrored"] .hs-ov-chips {
  right: auto;
  left: 32px;
  align-items: flex-start;
}

.hs-ambient-overlay[data-layout="mirrored"] .hs-ov-playing {
  left: auto;
  right: 32px;
}

.hs-ambient-overlay[data-layout="mirrored"] .hs-ov-caption {
  right: auto;
  left: 32px;
  text-align: left;
}

/* ---------- Night clock: dim, warm, red/orange only ---------- */

.hs-night-inner {
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: 8px;
}

.hs-night-time {
  font-size: 180px;
  font-weight: 700;
  letter-spacing: -6px;
  line-height: 1;
  color: #5a1a10;
}

.hs-night-glucose {
  font-size: 56px;
  font-weight: 800;
  color: #4a1c0e;
}

.hs-night-tone-caution {
  color: #8a4a0a;
}

.hs-night-tone-urgent {
  color: #c0392b;
}
```

- [ ] **Step 6: Build and test**

Run: `dotnet build HomeSpeaker.sln` and expect 0 warnings.
Run: `dotnet test HomeSpeaker.Server2.Tests` and expect all tests to pass.

- [ ] **Step 7: Verify in the browser without Immich**

Start with `Ambient__IdleSeconds=15` (as in Task 8), play a song, and wait 15s:
- Ambient shows a dark background with the large clock and date top-left, the now-playing panel bottom-left (with art or a gradient, title, artist and progress), and the glucose and temperature chips if those features are configured.
- No 4xx/5xx appears in the console apart from `/api/photos/next` returning 204, which is expected.
- With playback stopped, the now-playing panel disappears within ~5s.
- `idleController.setQuiet(true)` shows the dim red night clock.

- [ ] **Step 8: Verify the photo flow with a fake cache (no Immich needed)**

With the app stopped, create `{MediaFolder}/.photo-cache/` (MediaFolder is in `HomeSpeaker.Server2/appsettings.Development.json` or user secrets) containing:
- two JPEGs named `11111111-1111-4111-8111-111111111111.jpg` and `22222222-2222-4222-8222-222222222222.jpg`. Any photos will do.
- `manifest.json`:
  ```json
  [{"assetId":"11111111-1111-4111-8111-111111111111","takenAt":"2021-08-14T00:00:00","city":"Bear Lake","state":"Utah","imageWidth":1024,"imageHeight":600,"faces":[{"x1":0.75,"y1":0.1,"x2":0.9,"y2":0.35}]},
   {"assetId":"22222222-2222-4222-8222-222222222222","takenAt":"2019-06-01T00:00:00","country":"Mexico","imageWidth":0,"imageHeight":0,"faces":[]}]
  ```

Start the app again with `Ambient__IdleSeconds=15` and `Immich__SlideSeconds=8`. Check:
- The blurred backdrop on the Home page shows a photo.
- Ambient crossfades between the two photos.
- The first photo's caption reads "Bear Lake, Utah / August 2021 · 5 years ago" and its overlays are **mirrored** (clock top-right).
- The second photo's caption reads "Mexico / June 2019 · 7 years ago" with the default layout.
- `curl -X POST https://localhost:7238/api/photos/22222222-2222-4222-8222-222222222222/hide -k` returns 204, and only the first photo shows from then on.

Delete the fake `.photo-cache` folder afterwards.

- [ ] **Step 9: Leave changes uncommitted.**

---

### Task 10: Home page, home-automation slot, and Climate page

**Files:**
- Create: `HomeSpeaker.Server2/Services/Home/IHomeControl.cs`
- Create: `HomeSpeaker.Server2/Services/Home/NullHomeControl.cs`
- Create: `HomeSpeaker.Server2/Components/Glass/HomeTileRow.razor`
- Create: `HomeSpeaker.Server2/Pages/Health/Climate.razor`
- Rewrite: `HomeSpeaker.Server2/Pages/Index.razor`
- Delete: `HomeSpeaker.Server2/Pages/Index.razor.css`
- Modify: `HomeSpeaker.Server2/wwwroot/css/glass.css` (append the Home section)
- Modify: `HomeSpeaker.Server2/Program.cs` (register `NullHomeControl`)
- Modify: `HomeSpeaker.Server2/Components/_Imports.razor` and `HomeSpeaker.Server2/Pages/_Imports.razor` (add `@using HomeSpeaker.Server2.Services.Home`)

**Interfaces:**
- Consumes:
  - `DashboardState`, `NowPlayingInfo` and `GlucoseTone` (Tasks 6–7)
  - existing `HomeSpeakerService` methods: `ShuffleQueueAsync`, `StopPlayingAsync`, `ResumePlayAsync`, `SkipToNextAsync`, `SetRepeatModeAsync`, `SetVolumeAsync`
  - existing components `TemperatureMonitor` and `ForecastMonitor`
- Produces:
  - `IHomeControl`, `HomeTile`, `HomeTileKind`, `HomeAction` and `NullHomeControl`, the slot that sub-project 4 implements
  - the `/climate` route

- [ ] **Step 1: Write the home-control contract**

`HomeSpeaker.Server2/Services/Home/IHomeControl.cs`:

```csharp
namespace HomeSpeaker.Server2.Services.Home;

public enum HomeTileKind
{
    Light,
    Scene,
    Climate,
}

public enum HomeAction
{
    Toggle,
    TurnOn,
    TurnOff,
    Activate,
}

/// <summary>A device or scene shown on the Home page's Home row. State is display text such as "on", "off" or "68°".</summary>
public sealed record HomeTile(string Id, string Name, HomeTileKind Kind, string? State);

/// <summary>
/// Slot for a home-automation provider (e.g. Home Assistant). The UI renders whatever tiles the
/// registered implementation returns; with NullHomeControl the Home row is hidden entirely.
/// </summary>
public interface IHomeControl
{
    bool IsAvailable { get; }

    event Action? TilesChanged;

    Task<IReadOnlyList<HomeTile>> GetTilesAsync(CancellationToken cancellationToken = default);

    Task InvokeAsync(string tileId, HomeAction action, CancellationToken cancellationToken = default);
}
```

`HomeSpeaker.Server2/Services/Home/NullHomeControl.cs`:

```csharp
namespace HomeSpeaker.Server2.Services.Home;

public sealed class NullHomeControl : IHomeControl
{
    public bool IsAvailable => false;

    public event Action? TilesChanged
    {
        add { }
        remove { }
    }

    public Task<IReadOnlyList<HomeTile>> GetTilesAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<HomeTile>>([]);

    public Task InvokeAsync(string tileId, HomeAction action, CancellationToken cancellationToken = default) => Task.CompletedTask;
}
```

Add `@using HomeSpeaker.Server2.Services.Home` to both `HomeSpeaker.Server2/Components/_Imports.razor` and `HomeSpeaker.Server2/Pages/_Imports.razor`, after the `@using HomeSpeaker.Server2.Services.Dashboard` line.

In `HomeSpeaker.Server2/Program.cs`, next to the `DashboardState` registration, add:

```csharp
builder.Services.AddSingleton<HomeSpeaker.Server2.Services.Home.IHomeControl, HomeSpeaker.Server2.Services.Home.NullHomeControl>();
```

- [ ] **Step 2: Write the Home row component**

`HomeSpeaker.Server2/Components/Glass/HomeTileRow.razor`:

```razor
@inject IHomeControl HomeControl
@implements IDisposable

@if (HomeControl.IsAvailable && tiles.Count > 0)
{
    <section class="glass hs-home-row" aria-label="Home">
        <div class="hs-tile-label">Home</div>
        <div class="hs-home-tiles">
            @foreach (var tile in tiles)
            {
                <button type="button" class="hs-home-tile @(string.Equals(tile.State, "on", StringComparison.OrdinalIgnoreCase) ? "is-on" : "")" @onclick="() => activateAsync(tile)">
                    <span>@tile.Name</span>
                    @if (tile.State is not null)
                    {
                        <span class="hs-home-tile-state">@tile.State</span>
                    }
                </button>
            }
        </div>
    </section>
}

@code {
    private IReadOnlyList<HomeTile> tiles = [];

    protected override async Task OnInitializedAsync()
    {
        HomeControl.TilesChanged += onTilesChanged;
        await loadAsync();
    }

    private async Task loadAsync()
    {
        if (HomeControl.IsAvailable)
        {
            tiles = await HomeControl.GetTilesAsync();
        }
    }

    private void onTilesChanged() => _ = InvokeAsync(async () =>
    {
        await loadAsync();
        StateHasChanged();
    });

    private Task activateAsync(HomeTile tile) =>
        HomeControl.InvokeAsync(tile.Id, tile.Kind == HomeTileKind.Scene ? HomeAction.Activate : HomeAction.Toggle);

    public void Dispose() => HomeControl.TilesChanged -= onTilesChanged;
}
```

- [ ] **Step 3: Rewrite the Home page**

Delete `HomeSpeaker.Server2/Pages/Index.razor.css`. Replace the whole of `HomeSpeaker.Server2/Pages/Index.razor` with:

```razor
@page "/"
@using System.Globalization
@inject DashboardState Dashboard
@inject HomeSpeakerService Svc
@inject IHomeControl HomeControl
@inject ILogger<Index> Logger
@implements IDisposable

<PageTitle>Home Speaker</PageTitle>

<div class="hs-home @(HomeControl.IsAvailable ? "" : "hs-home--no-home-row")">
    <section class="glass hs-np-card" aria-label="Now playing">
        @if (NowPlayingInfo.From(Dashboard.Status) is { } np)
        {
            <div class="hs-np-head">
                @if (np.SongId is { } songId)
                {
                    <div class="hs-art hs-art--lg" style="background-image:url('/api/homespeaker/songs/@songId/art')"></div>
                }
                else
                {
                    <div class="hs-art hs-art--lg hs-art--radio">
                        <svg width="44" height="44" viewBox="0 0 24 24" fill="none" stroke="#fff" stroke-width="2" stroke-linecap="round" aria-hidden="true"><circle cx="12" cy="12" r="2"></circle><path d="M16.2 7.8a6 6 0 0 1 0 8.4M7.8 16.2a6 6 0 0 1 0-8.4M19 5a10 10 0 0 1 0 14M5 19A10 10 0 0 1 5 5"></path></svg>
                    </div>
                }
                <div class="hs-np-text">
                    <div class="hs-np-title">@np.Title</div>
                    @if (np.Subtitle is not null)
                    {
                        <div class="hs-np-sub">@np.Subtitle</div>
                    }
                    @if (Dashboard.UpNext is { } next)
                    {
                        <div class="hs-np-next">Up next: @next.Name</div>
                    }
                </div>
            </div>
            @if (!np.IsStream)
            {
                <div>
                    <div class="hs-progress hs-progress--lg"><div style="width:@((np.Percent * 100).ToString("F1", CultureInfo.InvariantCulture))%"></div></div>
                    <div class="hs-np-times"><span>@formatTime(np.Elapsed)</span><span>-@formatTime(np.Remaining)</span></div>
                </div>
            }
        }
        else
        {
            <div class="hs-np-text">
                <div class="hs-np-title">Nothing playing</div>
                <div class="hs-np-sub">Pick a playlist, a station, or an AI mix.</div>
            </div>
        }

        <div class="hs-np-controls">
            <button type="button" class="hs-icon-btn" aria-label="Shuffle queue" @onclick="ShuffleAsync">
                <svg width="24" height="24" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><path d="M16 3h5v5M4 20L21 3M21 16v5h-5M15 15l6 6M4 4l5 5"></path></svg>
            </button>
            <button type="button" class="hs-play-btn" aria-label="@(isPlaying ? "Stop" : "Play")" @onclick="PlayOrStopAsync">
                @if (isPlaying)
                {
                    <svg width="30" height="30" viewBox="0 0 24 24" fill="currentColor" aria-hidden="true"><rect x="6" y="6" width="12" height="12" rx="1.5"></rect></svg>
                }
                else
                {
                    <svg width="32" height="32" viewBox="0 0 24 24" fill="currentColor" aria-hidden="true"><path d="M7 4l13 8-13 8z"></path></svg>
                }
            </button>
            <button type="button" class="hs-icon-btn" aria-label="Next" @onclick="NextAsync">
                <svg width="32" height="32" viewBox="0 0 24 24" fill="currentColor" aria-hidden="true"><path d="M16 5h2v14h-2zM4 5v14l11-7z"></path></svg>
            </button>
            <button type="button" class="hs-icon-btn @(Dashboard.RepeatMode ? "is-on" : "")" aria-label="Repeat" aria-pressed="@(Dashboard.RepeatMode ? "true" : "false")" @onclick="ToggleRepeatAsync">
                <svg width="24" height="24" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><path d="M17 2l4 4-4 4"></path><path d="M3 11V9a3 3 0 0 1 3-3h15"></path><path d="M7 22l-4-4 4-4"></path><path d="M21 13v2a3 3 0 0 1-3 3H3"></path></svg>
            </button>
            <a class="hs-icon-btn" href="queue" aria-label="Queue">
                <svg width="24" height="24" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" aria-hidden="true"><path d="M4 6h16M4 12h16M4 18h10"></path></svg>
            </a>
        </div>

        <label class="hs-volume">
            <span class="visually-hidden">Volume</span>
            <svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><path d="M11 5L6 9H2v6h4l5 4z"></path></svg>
            <input type="range" min="0" max="100" value="@volumeValue" @oninput="OnVolumeInput" @onchange="OnVolumeChangedAsync" />
            <svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><path d="M11 5L6 9H2v6h4l5 4zM15.5 8.5a5 5 0 0 1 0 7M19 5a10 10 0 0 1 0 14"></path></svg>
        </label>
    </section>

    <div class="hs-home-side">
        <nav class="hs-source-tiles" aria-label="Music sources">
            <a class="glass hs-tile" href="playlists">
                <svg width="24" height="24" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" aria-hidden="true"><path d="M3 6h13M3 12h13M3 18h8M19 15V5l3 1"></path><circle cx="17" cy="16" r="2"></circle></svg>
                <span>Playlists</span>
            </a>
            <a class="glass hs-tile" href="streams">
                <svg width="24" height="24" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" aria-hidden="true"><circle cx="12" cy="12" r="2"></circle><path d="M16.2 7.8a6 6 0 0 1 0 8.4M7.8 16.2a6 6 0 0 1 0-8.4M19 5a10 10 0 0 1 0 14M5 19A10 10 0 0 1 5 5"></path></svg>
                <span>Radio</span>
            </a>
            <a class="glass hs-tile" href="ai-playlists">
                <svg width="24" height="24" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><path d="M12 3l1.8 5.2L19 10l-5.2 1.8L12 17l-1.8-5.2L5 10l5.2-1.8z"></path></svg>
                <span>AI Mix</span>
            </a>
            <a class="glass hs-tile" href="music">
                <svg width="24" height="24" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" aria-hidden="true"><path d="M4 4v16M9 4v16M14 4l5 16"></path></svg>
                <span>Library</span>
            </a>
        </nav>

        <div class="hs-info-tiles">
            @if (Dashboard.BloodSugarEnabled)
            {
                <a class="glass hs-tile hs-tile--info hs-tone-@toneClass" href="nightscout">
                    <span class="hs-tile-label">Glucose</span>
                    @if (Dashboard.BloodSugar?.CurrentReading is { } reading)
                    {
                        <span class="hs-tile-value-row">
                            <span class="hs-tile-value">@reading.Sgv.ToString("0", CultureInfo.InvariantCulture)</span>
                            <span class="hs-tile-meta">mg/dL @reading.DirectionIcon · @Dashboard.BloodSugar.StatusText</span>
                        </span>
                    }
                    else
                    {
                        <span class="hs-tile-meta">No reading yet</span>
                    }
                </a>
            }
            @if (Dashboard.TemperatureEnabled)
            {
                <a class="glass hs-tile hs-tile--info" href="climate">
                    <span class="hs-tile-label">Outside@(conditions is null ? "" : " · " + conditions)</span>
                    <span class="hs-tile-value-row">
                        <span class="hs-tile-value">@(Dashboard.Temperature?.OutsideTemperature is { } t ? t.ToString("0", CultureInfo.InvariantCulture) + "°" : "--")</span>
                        @if (highLow is not null)
                        {
                            <span class="hs-tile-meta">@highLow</span>
                        }
                    </span>
                </a>
            }
        </div>

        <HomeTileRow />
    </div>
</div>

@code {
    private int volumeValue;
    private bool draggingVolume;

    private bool isPlaying => Dashboard.Status?.StillPlaying == true;

    private string toneClass => Dashboard.GlucoseTone switch
    {
        GlucoseTone.Caution => "caution",
        GlucoseTone.Urgent => "urgent",
        _ => "normal",
    };

    private string? highLow =>
        Dashboard.Forecast is { Error: null, TodayHigh.Temperature: { } high, TonightLow.Temperature: { } low }
            ? string.Create(CultureInfo.InvariantCulture, $"H {high:0}° · L {low:0}°")
            : null;

    private string? conditions => Dashboard.Forecast is { Error: null } ? Dashboard.Forecast.TodayHigh?.Conditions : null;

    protected override void OnInitialized()
    {
        volumeValue = Dashboard.Volume;
        Dashboard.Changed += onChanged;
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            await Dashboard.StartAsync();
        }
    }

    private void onChanged() => _ = InvokeAsync(() =>
    {
        if (!draggingVolume)
        {
            volumeValue = Dashboard.Volume;
        }

        StateHasChanged();
    });

    private Task PlayOrStopAsync() => runAsync(() => isPlaying ? Svc.StopPlayingAsync() : Svc.ResumePlayAsync(), "play/stop");

    private Task NextAsync() => runAsync(Svc.SkipToNextAsync, "skip");

    private Task ShuffleAsync() => runAsync(Svc.ShuffleQueueAsync, "shuffle");

    private Task ToggleRepeatAsync() => runAsync(() => Svc.SetRepeatModeAsync(!Dashboard.RepeatMode), "repeat");

    private void OnVolumeInput(ChangeEventArgs e)
    {
        draggingVolume = true;
        if (int.TryParse(e.Value?.ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var value))
        {
            volumeValue = value;
        }
    }

    private async Task OnVolumeChangedAsync(ChangeEventArgs e)
    {
        draggingVolume = false;
        if (int.TryParse(e.Value?.ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var value))
        {
            await runAsync(() => Svc.SetVolumeAsync(value), "volume");
        }
    }

    private async Task runAsync(Func<Task> action, string what)
    {
        try
        {
            await action();
            await Dashboard.RefreshStatusAsync();
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Home page {Action} failed", what);
        }
    }

    private static string formatTime(TimeSpan time) =>
        time.ToString(time.TotalHours >= 1 ? @"h\:mm\:ss" : @"m\:ss", CultureInfo.InvariantCulture);

    public void Dispose() => Dashboard.Changed -= onChanged;
}
```

- [ ] **Step 4: Add the Climate page**

`HomeSpeaker.Server2/Pages/Health/Climate.razor`:

```razor
@page "/climate"

<PageTitle>Climate</PageTitle>

<TemperatureMonitor />
<ForecastMonitor />
```

- [ ] **Step 5: Append the Home styles**

Append to `HomeSpeaker.Server2/wwwroot/css/glass.css`:

```css
/* ---------- Home page ---------- */

.hs-home {
  display: flex;
  gap: 16px;
  height: 100%;
}

.hs-np-card {
  width: 420px;
  flex: none;
  box-sizing: border-box;
  border-radius: var(--glass-radius-card);
  padding: 22px;
  display: flex;
  flex-direction: column;
  gap: 16px;
}

.hs-np-head {
  display: flex;
  gap: 16px;
  align-items: center;
}

.hs-art--lg {
  width: 132px;
  height: 132px;
  border-radius: 16px;
}

.hs-np-text {
  min-width: 0;
  display: flex;
  flex-direction: column;
  gap: 4px;
}

.hs-np-title {
  font-size: 22px;
  font-weight: 800;
  line-height: 1.15;
  display: -webkit-box;
  -webkit-line-clamp: 2;
  -webkit-box-orient: vertical;
  overflow: hidden;
}

.hs-np-sub,
.hs-np-next {
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
}

.hs-np-sub {
  font-size: 16px;
  opacity: 0.85;
}

.hs-np-next {
  font-size: 14px;
  opacity: 0.7;
}

.hs-progress--lg {
  height: 6px;
  border-radius: 3px;
}

.hs-np-times {
  display: flex;
  justify-content: space-between;
  margin-top: 6px;
  font-size: 13px;
  font-weight: 600;
  opacity: 0.75;
}

.hs-np-controls {
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 0 4px;
}

.hs-icon-btn {
  width: 52px;
  height: 52px;
  border: 0;
  border-radius: 26px;
  background: transparent;
  color: #fff;
  display: grid;
  place-items: center;
  opacity: 0.85;
  cursor: pointer;
}

.hs-icon-btn:hover,
.hs-icon-btn:focus {
  color: #fff;
  opacity: 1;
}

.hs-icon-btn.is-on {
  background: rgba(255, 255, 255, 0.18);
  opacity: 1;
}

.hs-play-btn {
  width: 76px;
  height: 76px;
  border: 0;
  border-radius: 38px;
  background: #fff;
  color: #111;
  display: grid;
  place-items: center;
  cursor: pointer;
}

.hs-volume {
  margin-top: auto;
  display: flex;
  align-items: center;
  gap: 12px;
}

.hs-volume input[type="range"] {
  flex: 1;
  height: 32px;
  accent-color: #fff;
}

.hs-home-side {
  flex: 1;
  min-width: 0;
  display: flex;
  flex-direction: column;
  gap: 12px;
}

.hs-source-tiles {
  display: grid;
  grid-template-columns: repeat(4, minmax(0, 1fr));
  gap: 12px;
}

.hs-tile {
  min-height: 100px;
  box-sizing: border-box;
  border-radius: var(--glass-radius-tile);
  padding: 14px 16px;
  display: flex;
  flex-direction: column;
  justify-content: space-between;
  color: #fff;
  text-decoration: none;
  font-size: 15px;
  font-weight: 700;
}

.hs-tile:hover,
.hs-tile:focus {
  color: #fff;
}

.hs-info-tiles {
  display: grid;
  grid-template-columns: repeat(2, minmax(0, 1fr));
  gap: 12px;
}

.hs-tile--info {
  min-height: 112px;
}

/* No Home row: let the tiles fill the column. */
.hs-home--no-home-row .hs-source-tiles,
.hs-home--no-home-row .hs-info-tiles {
  flex: 1;
}

.hs-tile-label {
  font-size: 13px;
  font-weight: 700;
  letter-spacing: 1px;
  text-transform: uppercase;
  opacity: 0.75;
}

.hs-tile-value-row {
  display: flex;
  align-items: baseline;
  gap: 8px;
  flex-wrap: wrap;
}

.hs-tile-value {
  font-size: 38px;
  font-weight: 800;
  letter-spacing: -1px;
}

.hs-tile-meta {
  font-size: 14px;
  font-weight: 500;
  opacity: 0.85;
}

.hs-home-row {
  flex: 1;
  min-height: 0;
  border-radius: var(--glass-radius-tile);
  padding: 14px 16px;
  display: flex;
  flex-direction: column;
  gap: 10px;
}

.hs-home-tiles {
  flex: 1;
  display: grid;
  grid-template-columns: repeat(4, minmax(0, 1fr));
  gap: 10px;
}

.hs-home-tile {
  border: 0;
  border-radius: 16px;
  padding: 10px 12px;
  background: rgba(255, 255, 255, 0.1);
  color: #fff;
  text-align: left;
  display: flex;
  flex-direction: column;
  justify-content: flex-end;
  gap: 2px;
  font: 700 14px var(--glass-font);
  cursor: pointer;
}

.hs-home-tile.is-on {
  background: rgba(255, 210, 122, 0.35);
}

.hs-home-tile-state {
  font-weight: 500;
  opacity: 0.8;
}

@media (max-width: 700px) {
  .hs-home {
    flex-direction: column;
    height: auto;
  }

  .hs-np-card {
    width: auto;
  }

  .hs-source-tiles {
    grid-template-columns: repeat(2, minmax(0, 1fr));
  }

  .hs-info-tiles {
    grid-template-columns: 1fr;
  }
}
```

- [ ] **Step 6: Build and test**

Run: `dotnet build HomeSpeaker.sln` and expect 0 warnings.
Run: `dotnet test HomeSpeaker.Server2.Tests` and expect all tests to pass.
Run: `grep -rn "Index.razor.css\|home-primary-panels" HomeSpeaker.Server2 --include=*.razor --include=*.css` and expect no matches outside `bin/` and `obj/`.

- [ ] **Step 7: Verify in the browser**

Check at a 1024×600 viewport:
- With nothing playing, the card shows "Nothing playing" and the controls still render.
- Play a song from Music and return Home. Art, title, artist · album, "Up next", the progress bar and the times appear within ~5s.
- Stop, Play, Next, Shuffle and Repeat all work. Repeat highlights when on, and Queue navigates to `/queue`.
- Dragging the volume slider changes the volume, and the slider doesn't jump back while you drag.
- Play a radio stream: a radio glyph replaces the art and there is no progress bar.
- The tiles navigate to `/playlists`, `/streams`, `/ai-playlists`, `/music`, `/nightscout` and `/climate`. `/climate` shows the room temperatures and the forecast.
- There is no Home row, and the tiles fill the right column.
- At a 390×844 viewport the layout stacks into one column, with nothing clipped and no horizontal scroll.

- [ ] **Step 8: Leave changes uncommitted.**

---

### Task 11: Docs and end-to-end verification

**Files:**
- Modify: `CLAUDE.md`

**Interfaces:**
- Consumes: everything above.
- Produces: up-to-date project docs and a verified feature.

- [ ] **Step 1: Update `CLAUDE.md`**

1. In "Build / run / test commands", replace the paragraph that begins "There is **no test project**" with:
   ```markdown
   Unit tests live in `HomeSpeaker.Server2.Tests` (xUnit): `dotnet test HomeSpeaker.Server2.Tests`. They cover pure logic (push notifications, the Immich photo pipeline, kiosk detection, dashboard models); UI is verified by building and running.
   ```
2. In "Solution layout", add a third bullet: `- **HomeSpeaker.Server2.Tests** — xUnit tests for Server2.`
3. Under "Architecture", add a new subsection after "UI and the service facade":
   ```markdown
   ### Ambient shell and photos

   `MainLayout` is a glass-styled shell (`wwwroot/css/glass.css`, components in `Components/Glass`). On kiosk browsers (`body[data-kiosk=true]`: localhost, or `?kiosk=1` cookie — see `Kiosk.cs`) `wwwroot/js/idleController.js` switches to an ambient photo slideshow after `Ambient:IdleSeconds` idle, or a dim night clock during `QuietHours`. `PhotoSyncWorker` mirrors one Immich album (`Immich:*` config) into `{MediaFolder}/.photo-cache`; `PhotoLibrary` serves it via `/api/photos/*` and `wwwroot/js/ambientSlideshow.js` crossfades it. Overlays and the Home page read live data from the scoped `DashboardState`. Home automation plugs in through `IHomeControl` (default `NullHomeControl`, which hides the Home row).
   ```
4. In "Configuration keys", add `Immich:*` and `Ambient:IdleSeconds` to the list of optional keys.

- [ ] **Step 2: Run the full build and tests**

Run: `dotnet build HomeSpeaker.sln` and expect 0 warnings and 0 errors.
Run: `dotnet build HomeSpeaker.sln -c Release` and expect success.
Run: `dotnet test HomeSpeaker.Server2.Tests` and expect all tests to pass. Report the counts.

- [ ] **Step 3: Verify against real Immich (needs the user)**

Ask the user to set the secrets locally. Never write the API key into any file in the repo:

```powershell
dotnet user-secrets set "Immich:BaseUrl" "https://immich.boston-cloud.ts.net" --project HomeSpeaker.Server2
dotnet user-secrets set "Immich:AlbumId" "c7fcba71-bdc5-4de0-9511-aa45ea22959f" --project HomeSpeaker.Server2
dotnet user-secrets set "Immich:ApiKey" "<their key>" --project HomeSpeaker.Server2
```

Then run the app and work through spec §5, items 1–8:
- the log shows "Photo sync complete: N photos" and the slideshow cycles real photos with the right captions
- a bad `Immich:BaseUrl`, set via an environment variable override, logs a warning while the slideshow keeps running from the cache
- removing a photo from the album and calling `POST /api/photos/sync` drops it
- idle, wake, night and kiosk behave as described
- the layout works at both 1024×600 and phone width

Report each item as pass or fail, with evidence.

- [ ] **Step 4: Leave changes uncommitted.** Summarize the changed files for the user.
