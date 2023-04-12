using HomeSpeaker.Server;
using HomeSpeaker.Server.Data;
using HomeSpeaker.Server2.Data;
using HomeSpeaker.Server2.Services;
using Microsoft.EntityFrameworkCore;
using OpenTelemetry.Trace;
using Serilog;
using Serilog.Exceptions;
using System.Runtime.InteropServices;

var builder = WebApplication.CreateBuilder(args);
//builder.Logging.ClearProviders();
//builder.Logging.AddJsonConsole();
//builder.Logging.AddDebug();
builder.Services.AddApplicationInsightsTelemetry();

// Additional configuration is required to successfully run gRPC on macOS.
// For instructions on how to configure Kestrel and gRPC clients on macOS, visit https://go.microsoft.com/fwlink/?linkid=2099682

// Add services to the container.
try
{
    var ping = new System.Net.NetworkInformation.Ping();
    var response = ping.Send("http://jaeger");
    if (response.Status == System.Net.NetworkInformation.IPStatus.Success)
    {
        builder.Services.AddOpenTelemetry()
            .WithTracing(b =>
            {
                b.AddConsoleExporter()
                .AddAspNetCoreInstrumentation()
                .AddJaegerExporter(options => options.AgentHost = "jaeger");
            })
            .WithMetrics(b =>
            {

            });
    }
    else
    {
        Console.WriteLine("!!! Cannot contact jaeger ?!? ***********");
    }
}
catch (Exception ex)
{
    Console.WriteLine("!!! Trouble contacting jaeger: " + ex.ToString());
}

builder.Host.UseSerilog((context, loggerConfig) =>
{
    loggerConfig
        .WriteTo.Console()
        .Enrich.WithExceptionDetails()
        .WriteTo.Seq("http://localhost:5341");
});

builder.Services.AddRazorPages();
builder.Services.AddGrpc();

builder.Services.AddHostedService<MigrationApplier>();
builder.Services.AddDbContext<MusicContext>(options => options.UseSqlite(builder.Configuration.GetConnectionString("data source=homespeaker.db")));
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

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseWebAssemblyDebugging();
}
else
{
    app.UseExceptionHandler("/Error");
}

app.UseGrpcWeb(new GrpcWebOptions { DefaultEnabled = true });
app.UseHttpsRedirection();
app.UseBlazorFrameworkFiles();
app.UseStaticFiles();
app.UseRouting();
app.MapRazorPages();

// Configure the HTTP request pipeline.
app.MapGrpcService<GreeterService>();
app.MapGrpcService<HomeSpeakerService>();

app.MapFallbackToFile("index.html");

app.Run();
