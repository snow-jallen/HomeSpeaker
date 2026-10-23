using System.ClientModel;
using System.ClientModel.Primitives;
using System.Net;
using System.Runtime.InteropServices;
using Azure.AI.OpenAI;
using HomeSpeaker.Server2;
using HomeSpeaker.Server2.Data;
using HomeSpeaker.Server2.Endpoints;
using HomeSpeaker.Server2.Services;
using HomeSpeaker.Server2.Services.LiveActivities;
using HomeSpeaker.Server2.Services.MediaSearch;
using HomeSpeaker.Server2.Services.Photos;
using HomeSpeaker.Server2.Settings;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;
using Microsoft.Fast.Components.FluentUI;
using MudBlazor.Services;
using OpenAI;
using OpenAI.Chat;

#pragma warning disable IDE1006 // Naming Styles
const string LocalCorsPolicy = nameof(LocalCorsPolicy);
#pragma warning restore IDE1006 // Naming Styles

var builder = WebApplication.CreateBuilder(args);

// Database-backed settings sit between appsettings.json and environment variables.
builder.Configuration.AddDatabaseSettings(builder.Configuration["SqliteConnectionString"]);
var settingsSource = builder.Configuration.Sources.OfType<DatabaseSettingsConfigurationSource>().SingleOrDefault();
builder.Services.AddSingleton<ISettingsWriter>(new SettingsWriter(settingsSource?.Store, settingsSource?.Provider));
if (settingsSource?.Store is { } settingsStore)
{
    builder.Services.AddSingleton(settingsStore);
}

if (settingsSource?.Provider is { } settingsProvider)
{
    builder.Services.AddSingleton(settingsProvider);
}

// Configure host shutdown timeout
builder.Host.ConfigureHostOptions(options =>
{
    options.ShutdownTimeout = TimeSpan.FromSeconds(5); // 5 second timeout for graceful shutdown
});

builder.AddServiceDefaults();

builder.Services.AddResponseCompression(o => o.EnableForHttps = true);
builder.Services.AddCors(options =>
{
    options.AddPolicy(name: LocalCorsPolicy,
                      policy =>
                      {
                          policy.WithOrigins("http://example.com",
                                              "http://www.contoso.com");
                      });
});
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents(options =>
    {
        options.DisconnectedCircuitRetentionPeriod = TimeSpan.FromHours(4);
    });
builder.Services.AddScoped(sp =>
{
    var navigationManager = sp.GetRequiredService<Microsoft.AspNetCore.Components.NavigationManager>();
    return new HttpClient
    {
        BaseAddress = new Uri(navigationManager.BaseUri)
    };
});
// Persist data-protection keys on the media volume so redeploys keep admin cookies and antiforgery tokens valid.
builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo(Path.Combine(builder.Configuration[ConfigKeys.MediaFolder] ?? throw new MissingConfigException(ConfigKeys.MediaFolder), ".keys")))
    .SetApplicationName("HomeSpeaker");
builder.Services.AddAuthentication()
    .AddCookie(AdminPinHandler.SchemeName, options =>
    {
        options.Cookie.Name = "hs-admin";
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.Cookie.SecurePolicy = CookieSecurePolicy.None; // must work over plain HTTP on the LAN
        options.Cookie.HttpOnly = true;
        options.ExpireTimeSpan = TimeSpan.FromDays(30);
        options.SlidingExpiration = true;
        options.LoginPath = "/unlock";
    });
builder.Services.AddAuthorization(options =>
    options.AddPolicy("Admin", policy =>
    {
        policy.AddAuthenticationSchemes(AdminPinHandler.SchemeName);
        policy.AddRequirements(new AdminPinRequirement());
    }));
builder.Services.AddCascadingAuthenticationState();
builder.Services.AddSingleton<IAuthorizationHandler, AdminPinHandler>();
builder.Services.AddSingleton(sp => new PinAttemptLimiter(TimeProvider.System));
builder.Services.AddHostedService<MigrationApplier>();
if (FeatureFlags.AnchorsEnabled(builder.Configuration))
{
    builder.Services.AddHostedService<DailyAnchorWorker>();
}

builder.Services.AddHostedService<AirPlayReceiverService>();
builder.Services.AddScoped<HomeSpeakerService>(); // Scoped for Blazor components
builder.Services.AddScoped<PlaylistService>();
builder.Services.AddScoped<AutoPlayService>();
builder.Services.AddScoped<AnchorService>();
builder.Services.AddScoped<OfflineDownloadService>();
builder.Services.AddScoped<IAnchorNotificationService, AnchorNotificationService>();
builder.Services.AddSignalR();
builder.Services.AddDbContext<MusicContext>(options => options.UseSqlite(builder.Configuration["SqliteConnectionString"]));
builder.Services.AddSingleton<IDataStore, OnDiskDataStore>();
builder.Services.AddSingleton<IFileSource>(_ => new DefaultFileSource(builder.Configuration[ConfigKeys.MediaFolder] ?? throw new MissingConfigException(ConfigKeys.MediaFolder)));
builder.Services.AddSingleton<ITagParser, DefaultTagParser>();
builder.Services.AddSingleton<YoutubeService>();
if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
{
    builder.Services.AddSingleton<WindowsMusicPlayer>();
}
else
{
    builder.Services.AddSingleton<AudioDeviceDetector>();
    builder.Services.AddSingleton<LinuxSoxMusicPlayer>();
}

builder.Services.AddSingleton<IMusicPlayer>(services =>
{
    IMusicPlayer actualPlayer = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
        ? services.GetRequiredService<WindowsMusicPlayer>()
        : services.GetRequiredService<LinuxSoxMusicPlayer>();

    return new ChattyMusicPlayer(actualPlayer);
});
builder.Services.AddSingleton<Mp3Library>();
builder.Services.AddHostedService<LifecycleEvents>();
builder.Services.AddHostedService<AutoPlayMonitorService>();

// Add memory cache for caching services
builder.Services.AddMemoryCache();
builder.Services.AddSingleton(TimeProvider.System);

builder.Services.Configure<AiMusicOptions>(builder.Configuration.GetSection("AI"));
builder.Services.Configure<PushNotificationOptions>(builder.Configuration.GetSection("PushNotifications"));
builder.Services.Configure<ImmichOptions>(builder.Configuration.GetSection("Immich"));
builder.Services.PostConfigure<ImmichOptions>(options => ImmichOptionsFallback.Apply(options, builder.Configuration));
void configureImmichClient(IServiceProvider sp, HttpClient client, TimeSpan timeout)
{
    var immich = sp.GetRequiredService<IOptions<ImmichOptions>>().Value;
    client.Timeout = timeout;
    if (immich.IsConfigured)
    {
        client.BaseAddress = new Uri(immich.BaseUrl!.TrimEnd('/') + "/");
        client.DefaultRequestHeaders.Add("x-api-key", immich.ApiKey);
    }
}

builder.Services.AddHttpClient<IImmichClient, ImmichClient>((sp, client) => configureImmichClient(sp, client, TimeSpan.FromSeconds(30)));
// Video streams can run for minutes, so they use their own client with no overall timeout.
builder.Services.AddHttpClient(ImmichClient.VideoClientName, (sp, client) => configureImmichClient(sp, client, Timeout.InfiniteTimeSpan));
builder.Services.AddSingleton(sp => new PhotoCacheStore(
    Path.Combine(builder.Configuration[ConfigKeys.MediaFolder] ?? throw new MissingConfigException(ConfigKeys.MediaFolder), ".photo-cache"),
    sp.GetRequiredService<ILogger<PhotoCacheStore>>()));
builder.Services.AddSingleton(sp => new PhotoLibrary(sp.GetRequiredService<PhotoCacheStore>(), sp.GetRequiredService<TimeProvider>(), sp.GetRequiredService<IOptions<ImmichOptions>>()));
builder.Services.AddSingleton<PhotoSyncTrigger>();
builder.Services.AddScoped(sp => new PhotoSyncer(
    sp.GetRequiredService<IImmichClient>(),
    sp.GetRequiredService<PhotoCacheStore>(),
    sp.GetRequiredService<PhotoLibrary>(),
    sp.GetRequiredService<IOptions<ImmichOptions>>(),
    sp.GetRequiredService<ILogger<PhotoSyncer>>(),
    Random.Shared));
builder.Services.AddHostedService<PhotoSyncWorker>();
builder.Services.AddSingleton<VideoStreamCoordinator>();
builder.Services.AddHostedService(sp => new VideoPrefetcher(
    sp.GetRequiredService<PhotoLibrary>(),
    sp.GetRequiredService<PhotoCacheStore>(),
    sp.GetRequiredService<IImmichClient>(),
    sp.GetRequiredService<IOptions<ImmichOptions>>(),
    sp.GetRequiredService<TimeProvider>(),
    sp.GetRequiredService<VideoStreamCoordinator>(),
    sp.GetRequiredService<ILogger<VideoPrefetcher>>())
{
    FreeSpaceBytes = sp.GetRequiredService<PhotoCacheStore>().GetAvailableFreeSpace,
});
builder.Services.AddSingleton<AiProcessingSignal>();
builder.Services.AddSingleton<AiMusicAnalyzer>();
builder.Services.AddScoped<AiMusicCatalogService>();
builder.Services.AddScoped<AiPlaybackService>();
builder.Services.AddScoped<MediaSearchService>();
builder.Services.AddScoped<PushNotificationService>();
builder.Services.AddHttpClient("ApnsClient", client =>
{
    client.DefaultRequestVersion = HttpVersion.Version20;
    client.DefaultVersionPolicy = HttpVersionPolicy.RequestVersionOrHigher;
    client.Timeout = TimeSpan.FromSeconds(15);
});
builder.Services.AddSingleton<ApplePushNotificationSender>();
builder.Services.AddSingleton<IPushNotificationSender>(sp => sp.GetRequiredService<ApplePushNotificationSender>());
builder.Services.AddSingleton<ILiveActivityPushSender>(sp => sp.GetRequiredService<ApplePushNotificationSender>());
builder.Services.AddSingleton<IPlayerStatusProvider, PlayerStatusProvider>();
builder.Services.AddSingleton<PlayerStatusBroadcaster>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<PlayerStatusBroadcaster>());
builder.Services.AddSingleton<LiveActivityRegistry>();
builder.Services.AddSingleton<LiveActivityPushDispatcher>();
builder.Services.AddHostedService<LiveActivityPushWorker>();
if (FeatureFlags.PushEnabled(builder.Configuration) &&
    (FeatureFlags.TemperatureEnabled(builder.Configuration) || FeatureFlags.BloodSugarEnabled(builder.Configuration)))
{
    builder.Services.AddHostedService<HealthAlertWorker>();
}

builder.Services.AddHostedService<AiMusicAnalysisWorker>();
builder.Services.AddHostedService<HomeSpeaker.Server2.Services.VolumeMonitorService>();
builder.Services.AddChatClient(sp =>
{
    var aiOptions = sp.GetRequiredService<IOptions<AiMusicOptions>>().Value;
    var loggerFactory = sp.GetRequiredService<ILoggerFactory>();
    var logger = loggerFactory.CreateLogger("AiChatClient");
    var requestTimeout = getAiModelRequestTimeout(aiOptions);
    IChatClient chatClient;

    if (aiOptions.UseAzureOpenAI)
    {
        aiOptions.AzureOpenAI.TryGetEndpointUri(out var endpointUri);
        var azureOpenAiClient = new AzureOpenAIClient(
            endpointUri!,
            new ApiKeyCredential(aiOptions.AzureOpenAI.ApiKey!),
            createAzureOpenAiClientOptions(requestTimeout));
        chatClient = azureOpenAiClient
            .GetChatClient(aiOptions.AzureOpenAI.DeploymentName!)
            .AsIChatClient();
    }
    else if (aiOptions.HasOpenAIConfiguration)
    {
        var openAiClient = new ChatClient(
            aiOptions.OpenAI.ChatModel,
            new ApiKeyCredential(aiOptions.OpenAI.ApiKey!),
            createOpenAiClientOptions(requestTimeout));
        chatClient = openAiClient.AsIChatClient();
    }
    else
    {
        logger.LogWarning("AI analysis is disabled: {Reason}", aiOptions.ConfigurationIssue);
        return new NullChatClient();
    }

    chatClient = new LoggingChatClient(chatClient, logger);
    chatClient = new OpenTelemetryChatClient(chatClient, logger, "HomeSpeaker.AI");
    return chatClient;
}, ServiceLifetime.Singleton);

TimeSpan getAiModelRequestTimeout(AiMusicOptions options) =>
    TimeSpan.FromSeconds(Math.Max(30, options.ModelRequestTimeoutSeconds));

AzureOpenAIClientOptions createAzureOpenAiClientOptions(TimeSpan requestTimeout)
{
    var options = new AzureOpenAIClientOptions();
    applyAiTransportTimeouts(options, requestTimeout);
    return options;
}

OpenAIClientOptions createOpenAiClientOptions(TimeSpan requestTimeout)
{
    var options = new OpenAIClientOptions();
    applyAiTransportTimeouts(options, requestTimeout);
    return options;
}

void applyAiTransportTimeouts(ClientPipelineOptions options, TimeSpan requestTimeout)
{
    var transportTimeout = requestTimeout + TimeSpan.FromSeconds(15);
    options.NetworkTimeout = transportTimeout;
    options.Transport = new HttpClientPipelineTransport(new HttpClient
    {
        Timeout = transportTimeout
    });
}

// Add temperature and health-monitor services with caching
builder.Services.AddHttpClient<TemperatureService>();
builder.Services.AddScoped<ITemperatureService>(sp => sp.GetRequiredService<TemperatureService>());
builder.Services.AddHttpClient<BloodSugarService>();
builder.Services.AddScoped<IBloodSugarService>(sp => sp.GetRequiredService<BloodSugarService>());
builder.Services.AddHttpClient<ForecastService>(client => client.Timeout = TimeSpan.FromSeconds(10));
builder.Services.AddScoped<IForecastService>(sp => sp.GetRequiredService<ForecastService>());

// Add HttpClient for RadioStreamService (favicon downloads)
builder.Services.AddHttpClient<RadioStreamService>()
    .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
    {
        AutomaticDecompression = System.Net.DecompressionMethods.All
    });

// Add HttpClient for ImageSearchService (DDG + Wikipedia image search)
builder.Services.AddHttpClient<ImageSearchService>(client =>
{
    client.DefaultRequestHeaders.UserAgent.ParseAdd(
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
    client.Timeout = TimeSpan.FromSeconds(10);
});

// A "self" check tagged "live" is already registered by AddServiceDefaults()
// (Extensions.cs AddDefaultHealthChecks) — the Docker HEALTHCHECK hits /alive, which
// runs only "live"-tagged checks (no DB). Here we add just the DB-backed readiness
// check, tagged "ready" so it is excluded from the liveness probe.
builder.Services.AddHealthChecks()
    .AddDbContextCheck<MusicContext>("database", tags: ["ready"]);

// Add browser-specific services for Blazor components
builder.Services.AddSingleton<HomeSpeaker.Server2.Services.PlayerStateService>();
builder.Services.AddScoped<HomeSpeaker.Server2.Services.Dashboard.DashboardState>();
builder.Services.AddSingleton<HomeSpeaker.Server2.Services.Home.IHomeControl, HomeSpeaker.Server2.Services.Home.NullHomeControl>();
builder.Services.AddScoped<HomeSpeaker.Server2.Services.IBrowserAudioService, HomeSpeaker.Server2.Services.BrowserAudioService>();
builder.Services.AddScoped<HomeSpeaker.Server2.Services.ILocalQueueService, HomeSpeaker.Server2.Services.LocalQueueService>();
builder.Services.AddScoped<HomeSpeaker.Server2.Services.IPlaybackModeService, HomeSpeaker.Server2.Services.PlaybackModeService>();
builder.Services.AddScoped<HomeSpeaker.Server2.Services.ImagePickerService>();
builder.Services.AddScoped<HomeSpeaker.Server2.Services.YouTubeStateService>();

// Add FluentUI and MudBlazor
builder.Services.AddFluentUIComponents();
builder.Services.AddMudServices();

var app = builder.Build();

// Configure SQLite for optimal performance
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<MusicContext>();
    try
    {
        // WAL mode: Better concurrency, faster writes
        db.Database.ExecuteSqlRaw("PRAGMA journal_mode=WAL;");
        // NORMAL synchronous mode: Faster, still safe
        db.Database.ExecuteSqlRaw("PRAGMA synchronous=NORMAL;");
        // 64MB cache for better performance
        db.Database.ExecuteSqlRaw("PRAGMA cache_size=-64000;");
        // Store temp tables in memory
        db.Database.ExecuteSqlRaw("PRAGMA temp_store=MEMORY;");
        // 256MB memory-mapped I/O for faster reads
        db.Database.ExecuteSqlRaw("PRAGMA mmap_size=268435456;");

        app.Logger.LogInformation("SQLite performance optimizations applied");
    }
    catch (Exception ex)
    {
        app.Logger.LogWarning(ex, "Failed to apply SQLite optimizations, continuing with defaults");
    }
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
}

app.Logger.LogInformation("Starting HomeSpeaker.Server2");

app.UseResponseCompression();
//app.UseHttpsRedirection();

// Stop browsers from sniffing uploaded files as another type, and from framing the app on other sites.
app.Use(async (context, next) =>
{
    context.Response.Headers.XContentTypeOptions = "nosniff";
    context.Response.Headers.ContentSecurityPolicy = "frame-ancestors 'self'";
    await next(context);
});
app.UseStaticFiles();

// Serve favicons from the media folder (writable volume) at /favicons
var faviconsPath = Path.GetFullPath(Path.Combine(app.Configuration[ConfigKeys.MediaFolder] ?? "/music", "favicons"));
Directory.CreateDirectory(faviconsPath);
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new Microsoft.Extensions.FileProviders.PhysicalFileProvider(faviconsPath),
    RequestPath = "/favicons"
});

app.MapHealthChecks("/health", new HealthCheckOptions
{
    ResponseWriter = async (context, report) =>
    {
        context.Response.ContentType = "application/json";
        var response = new
        {
            status = report.Status.ToString(),
            checks = report.Entries.Select(x => new
            {
                name = x.Key,
                status = x.Value.Status.ToString(),
                description = x.Value.Description,
                duration = x.Value.Duration.TotalMilliseconds
            }),
            totalDuration = report.TotalDuration.TotalMilliseconds,
            timestamp = DateTime.UtcNow
        };
        await context.Response.WriteAsJsonAsync(response, context.RequestAborted);
    }
});

// Liveness probe: only "live"-tagged checks run (no DB), so the Docker HEALTHCHECK
// can never be wedged by a slow/contended SQLite database.
app.MapHealthChecks("/alive", new HealthCheckOptions
{
    Predicate = r => r.Tags.Contains("live")
});
app.Use(async (context, next) =>
{
    Kiosk.ApplyQueryOverride(context);
    await next(context);
});
app.UseRouting();
app.UseCors(LocalCorsPolicy);
app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();
app.MapGet("/api/features", (IConfiguration config, PhotoLibrary photos, IChatClient chatClient) => new
{
    TemperatureEnabled = FeatureFlags.TemperatureEnabled(config),
    BloodSugarEnabled = FeatureFlags.BloodSugarEnabled(config),
    ForecastEnabled = FeatureFlags.ForecastEnabled(config),
    AnchorsEnabled = FeatureFlags.AnchorsEnabled(config),
    PushEnabled = FeatureFlags.PushEnabled(config),
    TemperatureUnit = TemperatureDisplay.UnitFrom(config) == TemperatureUnit.Celsius ? "C" : "F",
    PhotosEnabled = photos.Count > 0,
    // AddChatClient falls back to NullChatClient when no AI provider is configured.
    AiEnabled = chatClient is not NullChatClient
});

app.MapFeatureModules();

// Recently played endpoint
app.MapGet("/api/music/recently-played", async (MusicContext db, Mp3Library library, int limit = 20) =>
{
    try
    {
        var recentImpressions = await db.Impressions
            .OrderByDescending(i => i.Timestamp)
            .Take(limit)
            .ToListAsync();

        var songs = recentImpressions
            .Select(i => library.Songs.FirstOrDefault(s => s.Path == i.SongPath))
            .Where(s => s != null)
            .Select(s => new
            {
                s!.SongId,
                s!.Name,
                s!.Path,
                s!.Album,
                s!.Artist
            })
            .ToList();

        return Results.Ok(songs);
    }
    catch (Exception ex)
    {
        return Results.Problem($"Failed to get recently played: {ex.Message}");
    }
});

// Music streaming endpoint for browser playback
app.MapGet("/api/music/{songId:int}", (int songId, Mp3Library library, ILogger<Program> logger) =>
{
    logger.LogInformation("Streaming endpoint called for song ID: {SongId}", songId);
    var song = library.Songs.FirstOrDefault(s => s.SongId == songId);
    if (song == null)
    {
        logger.LogWarning("Song with ID {SongId} not found in library", songId);
        return Results.NotFound($"Song with ID {songId} not found");
    }

    logger.LogInformation("Found song: {SongName} at path: {Path}", song.Name, song.Path);
    if (!File.Exists(song.Path))
    {
        logger.LogWarning("Music file not found on disk: {Path}", song.Path);
        return Results.NotFound($"Music file for song {songId} not found");
    }

    var fileInfo = new FileInfo(song.Path);
    var mimeType = fileInfo.Extension.ToLower() switch
    {
        ".mp3" => "audio/mpeg",
        ".wav" => "audio/wav",
        ".flac" => "audio/flac",
        ".m4a" => "audio/mp4",
        _ => "application/octet-stream"
    };

    // Range requests (audio seeking) are handled, and validated, by the framework.
    return Results.File(fileInfo.FullName, mimeType, enableRangeProcessing: true);
});

// Map HomeSpeaker REST API endpoints
app.MapHomeSpeakerApi();
app.MapMediaSearchApi();
app.MapPhotoApi();
app.MapAdminApi();
app.MapAiApi();

// Stream image search endpoint
app.MapGet("/api/streams/image-search", async (string q, ImageSearchService imageSearch) =>
{
    if (string.IsNullOrWhiteSpace(q))
    {
        return Results.BadRequest(new { Error = "Query is required" });
    }

    var results = await imageSearch.SearchAsync(q);
    return Results.Ok(results);
});

// Stream image upload endpoint
app.MapPost("/api/streams/upload-image", async (IFormFile file, RadioStreamService radioStreamService) =>
{
    if (!RadioStreamService.AllowedImageTypes.Contains(file.ContentType))
    {
        return Results.BadRequest(new { Error = "File must be an image (PNG, JPG, GIF, ICO, WebP)" });
    }

    if (file.Length > RadioStreamService.MaxFaviconBytes)
    {
        return Results.BadRequest(new { Error = "File must be under 2MB" });
    }

    var filename = await radioStreamService.UploadFaviconAsync(file);
    if (filename == null)
    {
        return Results.Problem("Failed to save image");
    }

    return Results.Ok(new { Filename = filename });
}).DisableAntiforgery();

// Map Blazor components for SSR and Interactive Server
// Fingerprinted URLs (via @Assets in App.razor) so a deploy never leaves a browser on stale CSS/JS
app.MapStaticAssets();

app.MapRazorComponents<HomeSpeaker.Server2.Components.App>()
    .AddInteractiveServerRenderMode();

app.Run();

public partial class Program
{
}
