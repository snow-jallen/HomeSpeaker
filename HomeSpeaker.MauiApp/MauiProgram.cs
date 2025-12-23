using HomeSpeaker.MauiApp.Services;
using HomeSpeaker.MauiApp.ViewModels;
using HomeSpeaker.MauiApp.Views;
using Microsoft.Extensions.Logging;

namespace HomeSpeaker.MauiApp;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
            });

        // Register services
        builder.Services.AddSingleton<IServerConfigurationService, ServerConfigurationService>();
        builder.Services.AddSingleton<IHomeSpeakerClientService, HomeSpeakerClientService>();
        
        // Register ViewModels
        builder.Services.AddSingleton<MainViewModel>();
        builder.Services.AddTransient<ServerConfigViewModel>();
        
        // Register Views
        builder.Services.AddSingleton<MainPage>();
        builder.Services.AddTransient<ServerConfigPage>();

#if DEBUG
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }
}
