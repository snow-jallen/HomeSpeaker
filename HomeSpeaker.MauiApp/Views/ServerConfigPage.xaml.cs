using HomeSpeaker.MauiApp.ViewModels;

namespace HomeSpeaker.MauiApp.Views;

public partial class ServerConfigPage : ContentPage
{
    public ServerConfigPage(ServerConfigViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
