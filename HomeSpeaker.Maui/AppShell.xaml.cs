using HomeSpeaker.Maui.Views;

namespace HomeSpeaker.Maui;

public partial class AppShell : Shell
{
    public AppShell()
    {
        InitializeComponent();
        Routing.RegisterRoute("Playlists", typeof(PlaylistView));
    }
}
