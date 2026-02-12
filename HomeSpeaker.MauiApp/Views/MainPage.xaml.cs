using HomeSpeaker.MauiApp.ViewModels;

namespace HomeSpeaker.MauiApp.Views;

public partial class MainPage : ContentPage
{
    public MainPage(MainViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        
        if (BindingContext is MainViewModel vm)
        {
            await vm.LoadServersCommand.ExecuteAsync(null);
        }
    }
}
