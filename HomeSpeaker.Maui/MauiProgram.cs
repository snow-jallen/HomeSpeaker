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
            .UseMauiCommunityToolkit()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
            })
            .RegisterViewModels()
            .RegisterViews();

        builder.Services.AddSingleton<HomeSpeakerService>();

#if DEBUG
		builder.Logging.AddDebug();
#endif

        return builder.Build();
    }

    public static MauiAppBuilder RegisterViews(this MauiAppBuilder builder)
    {
        builder.Services.AddTransient<ManageDevicesView>();
        builder.Services.AddTransient<QueueView>();
        return builder;
    }

    public static MauiAppBuilder RegisterViewModels(this MauiAppBuilder builder)
    {
        builder.Services.AddTransient<ManageDevicesViewModel>();
        builder.Services.AddTransient<QueueViewModel>();
        return builder;
    }
}
