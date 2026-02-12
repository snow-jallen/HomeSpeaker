using HomeSpeaker.MauiApp.Views;

namespace HomeSpeaker.MauiApp;

public partial class AppShell : Shell
{
    public AppShell()
    {
        InitializeComponent();
        
        Routing.RegisterRoute(nameof(ServerConfigPage), typeof(ServerConfigPage));
    }
}
