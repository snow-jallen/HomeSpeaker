using HomeSpeaker.WebAssembly;
using HomeSpeaker.WebAssembly.Services;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.Fast.Components.FluentUI;
using MudBlazor.Services;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// Configure logging to show in browser console
builder.Logging.SetMinimumLevel(LogLevel.Information);
builder.Logging.AddFilter("Microsoft.AspNetCore.Components.WebAssembly", LogLevel.Warning);

builder.Services.AddScoped((_) => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });
builder.Services.AddSingleton<HomeSpeakerService>();
builder.Services.AddSingleton<PlayerStateService>();
builder.Services.AddScoped<ITemperatureService, TemperatureService>();
builder.Services.AddScoped<IBloodSugarService, BloodSugarService>();
builder.Services.AddScoped<IForecastService, ForecastService>();
builder.Services.AddHttpClient<IAnchorService, AnchorService>((serviceProvider, client) =>
{
    var configuration = serviceProvider.GetRequiredService<IConfiguration>();
    var anchorsApiAddress = configuration["AnchorsApiAddress"] ?? builder.HostEnvironment.BaseAddress;
    client.BaseAddress = new Uri(anchorsApiAddress);
});
builder.Services.AddScoped<IAnchorSyncService, AnchorSyncService>();
builder.Services.AddScoped<IBrowserAudioService, BrowserAudioService>();
builder.Services.AddScoped<ILocalQueueService, LocalQueueService>();
builder.Services.AddScoped<IPlaybackModeService, PlaybackModeService>();
builder.Services.AddScoped<ImagePickerService>();

builder.Services.AddFluentUIComponents();
builder.Services.AddMudServices();

try
{
    var endpoint = builder.Configuration["OtlpExporter"] ?? "http://localhost:4318";
    Console.WriteLine($"Trying to setup otel tracing @ {endpoint}");
    builder.Services.AddOpenTelemetry();
    builder.Services.ConfigureOpenTelemetryTracerProvider(tracerProviderBuilder =>
    {
        tracerProviderBuilder
            .SetResourceBuilder(ResourceBuilder.CreateDefault()
                .AddService("BlazorWasmApp"))
            .AddOtlpExporter(options =>
            {
                options.Endpoint = new Uri(endpoint); // Aspire container OTLP endpoint
                options.Protocol = OpenTelemetry.Exporter.OtlpExportProtocol.HttpProtobuf;
            })
            ;
    });
}
catch (Exception ex)
{
    Console.WriteLine("!!! Trouble contacting jaeger: " + ex.ToString());
}

var app = builder.Build();

// Start anchor sync service for real-time updates
try 
{
    var anchorSync = app.Services.GetRequiredService<IAnchorSyncService>();
    await anchorSync.StartAsync();
}
catch (Exception ex)
{
    Console.WriteLine($"Failed to start anchor sync: {ex.Message}");
}

await app.RunAsync();
