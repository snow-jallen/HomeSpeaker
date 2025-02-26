using Grpc.Net.Client;
using Grpc.Net.Client.Web;
using Microsoft.Extensions.Logging;

namespace HomeSpeaker.MAUI;

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

#if DEBUG
		builder.Logging.AddDebug();
#endif
        var httpHandler = new GrpcWebHandler(new HttpClientHandler());
        var channel = GrpcChannel.ForAddress("http://192.168.144.1", new GrpcChannelOptions
        {
            HttpHandler = httpHandler
        });

        var homeSpeakerClient = new HomeSpeaker.HomeSpeakerClient(channel);

        builder.Services.AddSingleton(homeSpeakerClient);
        return builder.Build();
	}
}
