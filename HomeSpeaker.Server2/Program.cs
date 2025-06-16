using HomeSpeaker.Server;
using HomeSpeaker.Server.Data;
using HomeSpeaker.Server2;
using HomeSpeaker.Server2.Data;
using HomeSpeaker.Server2.Services;
using Microsoft.EntityFrameworkCore;
using System.Runtime.InteropServices;

const string LocalCorsPolicy = nameof(LocalCorsPolicy);

var builder = WebApplication.CreateBuilder(args);

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
builder.Services.AddScoped<PlaylistService>();
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

app.MapFallbackToFile("index.html");

app.Run();
