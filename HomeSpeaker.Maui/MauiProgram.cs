using CommunityToolkit.Maui;
using Microsoft.Extensions.Logging;
using HomeSpeaker.Maui.ViewModels;
using HomeSpeaker.Maui.Views;
using HomeSpeaker.Maui.Services;

namespace HomeSpeaker.Maui;
public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .UseMauiCommunityToolkit(options => options.SetShouldEnableSnackbarOnWindows(true))
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
                fonts.AddFont("fa-solid.otf", "FontAwesomeSolid");
            })
            .RegisterViewModels()
            .RegisterViews();

        builder.Services.AddSingleton<HomeSpeakerClientFactory>();
        builder.Services.AddSingleton<DeviceViewerService>();
        builder.Services.AddScoped<PlaylistServiceFactory>();

#if DEBUG
		builder.Logging.AddDebug();
#endif

        return builder.Build();
    }

    public static MauiAppBuilder RegisterViews(this MauiAppBuilder builder)
    {
        builder.Services.AddTransient<ManageDevicesView>();
        builder.Services.AddTransient<MusicController>();
        return builder;
    }

    public static MauiAppBuilder RegisterViewModels(this MauiAppBuilder builder)
    {
        builder.Services.AddScoped<YouTubeViewModel>();
        builder.Services.AddSingleton<ManageDevicesViewModel>();
        builder.Services.AddTransient<MusicControllerViewModel>();
        builder.Services.AddScoped<PlaylistViewModel>();
        return builder;
    }
}
