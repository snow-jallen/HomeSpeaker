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

// Register temperature service
builder.Services.AddScoped<ITemperatureService, TemperatureService>();

// Register blood sugar service
builder.Services.AddScoped<IBloodSugarService, BloodSugarService>();

// Register anchor service
builder.Services.AddScoped<IAnchorService, AnchorService>();

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

await builder.Build().RunAsync();
