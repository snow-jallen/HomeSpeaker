using HomeSpeaker.Server2;
using HomeSpeaker.Server2.Data;
using HomeSpeaker.Server2.Services;
using HomeSpeaker.Shared;
using Microsoft.EntityFrameworkCore;
using System.Runtime.InteropServices;

const string LocalCorsPolicy = nameof(LocalCorsPolicy);

var builder = WebApplication.CreateBuilder(args);

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
builder.Services.AddRazorPages();
builder.Services.AddGrpc();
builder.Services.AddHostedService<MigrationApplier>();
builder.Services.AddHostedService<DailyAnchorWorker>();
builder.Services.AddHostedService<AirPlayReceiverService>();
builder.Services.AddScoped<PlaylistService>();
builder.Services.AddScoped<AnchorService>();
builder.Services.AddScoped<AmazonMusicService>();
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

// Add memory cache for caching services
builder.Services.AddMemoryCache();

// Add temperature service with caching
builder.Services.AddHttpClient<TemperatureService>();
builder.Services.AddSingleton<TemperatureService>();

// Add blood sugar service with smart caching
builder.Services.AddHttpClient<BloodSugarService>();
builder.Services.AddSingleton<BloodSugarService>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseWebAssemblyDebugging();
}
else
{
    app.UseExceptionHandler("/Error");
}

app.Logger.LogInformation("Starting HomeSpeaker.Server2");

app.UseResponseCompression();
app.UseGrpcWeb(new GrpcWebOptions { DefaultEnabled = true });
//app.UseHttpsRedirection();
app.UseBlazorFrameworkFiles();
app.UseStaticFiles();
app.UseRouting();
app.UseCors(LocalCorsPolicy);
app.MapRazorPages();
app.MapHub<HomeSpeaker.Server2.Hubs.AnchorHub>("/anchorHub");

// Configure the HTTP request pipeline.
app.MapGrpcService<GreeterService>();
app.MapGrpcService<HomeSpeakerService>();
app.MapGet("/ns", (IConfiguration config) => config["NIGHTSCOUT_URL"] ?? string.Empty);

// Temperature API endpoint
app.MapGet("/api/temperature", async (TemperatureService temperatureService, CancellationToken cancellationToken) =>
{
    try
    {
        var temperatureStatus = await temperatureService.GetTemperatureStatusAsync(cancellationToken);
        return Results.Ok(temperatureStatus);
    }
    catch (Exception ex)
    {
        return Results.Problem($"Failed to get temperature data: {ex.Message}");
    }
});

// Blood Sugar API endpoint
app.MapGet("/api/bloodsugar", async (BloodSugarService bloodSugarService, CancellationToken cancellationToken) =>
{
    try
    {
        var bloodSugarStatus = await bloodSugarService.GetBloodSugarStatusAsync(cancellationToken);
        return Results.Ok(bloodSugarStatus);
    }
    catch (Exception ex)
    {
        return Results.Problem($"Failed to get blood sugar data: {ex.Message}");
    }
});

// Temperature cache management endpoints
app.MapDelete("/api/temperature/cache", (TemperatureService temperatureService) =>
{
    try
    {
        temperatureService.ClearCache();
        return Results.Ok(new { message = "Temperature cache cleared successfully" });
    }
    catch (Exception ex)
    {
        return Results.Problem($"Failed to clear temperature cache: {ex.Message}");
    }
});

app.MapPost("/api/temperature/refresh", async (TemperatureService temperatureService, CancellationToken cancellationToken) =>
{
    try
    {
        var temperatureStatus = await temperatureService.RefreshAsync(cancellationToken);
        return Results.Ok(temperatureStatus);
    }
    catch (Exception ex)
    {
        return Results.Problem($"Failed to refresh temperature data: {ex.Message}");
    }
});

// Blood Sugar cache management endpoints
app.MapDelete("/api/bloodsugar/cache", (BloodSugarService bloodSugarService) =>
{
    try
    {
        bloodSugarService.ClearCache();
        return Results.Ok(new { message = "Blood sugar cache cleared successfully" });
    }
    catch (Exception ex)
    {
        return Results.Problem($"Failed to clear blood sugar cache: {ex.Message}");
    }
});

app.MapPost("/api/bloodsugar/refresh", async (BloodSugarService bloodSugarService, CancellationToken cancellationToken) =>
{
    try
    {
        var bloodSugarStatus = await bloodSugarService.RefreshAsync(cancellationToken);
        return Results.Ok(bloodSugarStatus);
    }
    catch (Exception ex)
    {
        return Results.Problem($"Failed to refresh blood sugar data: {ex.Message}");
    }
});

// Anchor API endpoints
app.MapGet("/api/anchors/definitions", async (AnchorService anchorService) =>
{
    try
    {
        var definitions = await anchorService.GetActiveAnchorDefinitionsAsync();
        return Results.Ok(definitions);
    }
    catch (Exception ex)
    {
        return Results.Problem($"Failed to get anchor definitions: {ex.Message}");
    }
});

app.MapPost("/api/anchors/definitions", async (AnchorService anchorService, CreateAnchorDefinitionRequest request) =>
{
    try
    {
        var definition = await anchorService.CreateAnchorDefinitionAsync(request);
        return Results.Created($"/api/anchors/definitions/{definition.Id}", definition);
    }
    catch (Exception ex)
    {
        return Results.Problem($"Failed to create anchor definition: {ex.Message}");
    }
});

app.MapPut("/api/anchors/definitions/{id:int}", async (AnchorService anchorService, int id, CreateAnchorDefinitionRequest request) =>
{
    try
    {
        var definition = await anchorService.UpdateAnchorDefinitionAsync(id, request);
        return definition != null ? Results.Ok(definition) : Results.NotFound();
    }
    catch (Exception ex)
    {
        return Results.Problem($"Failed to update anchor definition: {ex.Message}");
    }
});

app.MapDelete("/api/anchors/definitions/{id:int}", async (AnchorService anchorService, int id) =>
{
    try
    {
        var success = await anchorService.DeactivateAnchorDefinitionAsync(id);
        return success ? Results.NoContent() : Results.NotFound();
    }
    catch (Exception ex)
    {
        return Results.Problem($"Failed to deactivate anchor definition: {ex.Message}");
    }
});

app.MapGet("/api/anchors/users/{userId}", async (AnchorService anchorService, string userId) =>
{
    try
    {
        var userAnchors = await anchorService.GetUserAnchorsAsync(userId);
        return Results.Ok(userAnchors);
    }
    catch (Exception ex)
    {
        return Results.Problem($"Failed to get user anchors: {ex.Message}");
    }
});

app.MapPost("/api/anchors/users", async (AnchorService anchorService, AssignAnchorToUserRequest request) =>
{
    try
    {
        var userAnchor = await anchorService.AssignAnchorToUserAsync(request);
        return Results.Created($"/api/anchors/users/{userAnchor.UserId}", userAnchor);
    }
    catch (Exception ex)
    {
        return Results.Problem($"Failed to assign anchor to user: {ex.Message}");
    }
});

app.MapDelete("/api/anchors/users/{userId}/{anchorDefinitionId:int}", async (AnchorService anchorService, string userId, int anchorDefinitionId) =>
{
    try
    {
        var success = await anchorService.RemoveAnchorFromUserAsync(userId, anchorDefinitionId);
        return success ? Results.NoContent() : Results.NotFound();
    }
    catch (Exception ex)
    {
        return Results.Problem($"Failed to remove anchor from user: {ex.Message}");
    }
});

app.MapGet("/api/anchors/daily/{userId}/{date}", async (AnchorService anchorService, string userId, DateOnly date) =>
{
    try
    {
        var dailyAnchors = await anchorService.GetDailyAnchorsAsync(userId, date);
        return Results.Ok(dailyAnchors);
    }
    catch (Exception ex)
    {
        return Results.Problem($"Failed to get daily anchors: {ex.Message}");
    }
});

app.MapGet("/api/anchors/daily/{userId}", async (AnchorService anchorService, string userId, DateOnly? startDate, DateOnly? endDate) =>
{
    try
    {
        var start = startDate ?? DateOnly.FromDateTime(DateTime.Today.AddDays(-30));
        var end = endDate ?? DateOnly.FromDateTime(DateTime.Today);
        var dailyAnchors = await anchorService.GetDailyAnchorsRangeAsync(userId, start, end);
        return Results.Ok(dailyAnchors);
    }
    catch (Exception ex)
    {
        return Results.Problem($"Failed to get daily anchors range: {ex.Message}");
    }
});

app.MapPost("/api/anchors/daily/create/{userId}/{date}", async (AnchorService anchorService, string userId, DateOnly date) =>
{
    try
    {
        await anchorService.CreateDailyAnchorsForUserAsync(userId, date);
        return Results.Ok(new { message = "Daily anchors created successfully" });
    }
    catch (Exception ex)
    {
        return Results.Problem($"Failed to create daily anchors: {ex.Message}");
    }
});

app.MapPut("/api/anchors/daily/completion", async (AnchorService anchorService, UpdateAnchorCompletionRequest request) =>
{
    try
    {
        var success = await anchorService.UpdateAnchorCompletionAsync(request);
        return success ? Results.Ok() : Results.NotFound();
    }
    catch (Exception ex)
    {
        return Results.Problem($"Failed to update anchor completion: {ex.Message}");
    }
});

app.MapPost("/api/anchors/daily/ensure-today", async (AnchorService anchorService) =>
{
    try
    {
        await anchorService.EnsureTodayAnchorsForAllUsersAsync();
        return Results.Ok(new { message = "Today's anchors ensured for all users" });
    }
    catch (Exception ex)
    {
        return Results.Problem($"Failed to ensure today's anchors: {ex.Message}");    }
});

// Get all users who have anchors
app.MapGet("/api/anchors/users", async (AnchorService anchorService) =>
{
    try
    {
        var users = await anchorService.GetUsersWithAnchorsAsync();
        return Results.Ok(users);
    }
    catch (Exception ex)
    {
        return Results.Problem($"Failed to get users with anchors: {ex.Message}");
    }
});

// Get daily anchors for all users in a date range
app.MapGet("/api/anchors/daily", async (AnchorService anchorService, DateOnly? startDate, DateOnly? endDate) =>
{
    try
    {
        var allUserAnchors = await anchorService.GetAllUsersDailyAnchorsAsync(startDate, endDate);
        return Results.Ok(allUserAnchors);
    }
    catch (Exception ex)
    {
        return Results.Problem($"Failed to get all users' daily anchors: {ex.Message}");
    }
});

// Music streaming endpoint for browser playback
app.MapGet("/api/music/{songId:int}", async (int songId, Mp3Library library, HttpContext context, ILogger<Program> logger) =>
{
    logger.LogInformation("Streaming endpoint called for song ID: {songId}", songId);
    var song = library.Songs.FirstOrDefault(s => s.SongId == songId);
    if (song == null)
    {
        logger.LogWarning("Song with ID {songId} not found in library", songId);
        return Results.NotFound($"Song with ID {songId} not found");
    }

    logger.LogInformation("Found song: {songName} at path: {path}", song.Name, song.Path);

    if (!File.Exists(song.Path))
    {
        logger.LogWarning("Music file not found on disk: {path}", song.Path);
        return Results.NotFound($"Music file not found: {song.Path}");
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

    // Set headers for audio streaming
    context.Response.Headers.Append("Accept-Ranges", "bytes");
    context.Response.Headers.Append("Content-Type", mimeType);
    context.Response.Headers.Append("Content-Length", fileInfo.Length.ToString());

    // Handle range requests for audio seeking
    var rangeHeader = context.Request.Headers["Range"].FirstOrDefault();
    if (!string.IsNullOrEmpty(rangeHeader) && rangeHeader.StartsWith("bytes="))
    {
        var range = rangeHeader.Substring(6).Split('-');
        if (long.TryParse(range[0], out var start))
        {
            var end = range.Length > 1 && long.TryParse(range[1], out var endValue) 
                ? endValue 
                : fileInfo.Length - 1;

            context.Response.StatusCode = 206; // Partial Content
            context.Response.Headers.Append("Content-Range", $"bytes {start}-{end}/{fileInfo.Length}");
            context.Response.Headers["Content-Length"] = (end - start + 1).ToString();

            using var fileStream = new FileStream(song.Path, FileMode.Open, FileAccess.Read);
            fileStream.Seek(start, SeekOrigin.Begin);
            
            var buffer = new byte[8192];
            long remaining = end - start + 1;
            
            while (remaining > 0)
            {
                var bytesToRead = (int)Math.Min(buffer.Length, remaining);
                var bytesRead = await fileStream.ReadAsync(buffer, 0, bytesToRead);
                if (bytesRead == 0) break;
                
                await context.Response.Body.WriteAsync(buffer, 0, bytesRead);
                remaining -= bytesRead;
            }
        }
    }
    else
    {
        // Serve entire file
        return Results.File(song.Path, mimeType, enableRangeProcessing: true);
    }

    return Results.Empty;
});

app.MapFallbackToFile("index.html");

app.Run();
