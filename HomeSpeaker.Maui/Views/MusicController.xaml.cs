using HomeSpeaker.Maui.ViewModels;

namespace HomeSpeaker.Maui.Views;

public partial class MusicController : ContentPage
{

    private readonly MusicControllerViewModel _vm;
    public MusicController(MusicControllerViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        BindingContext = _vm;
    }
    protected async override void OnNavigatedTo(NavigatedToEventArgs e)
    {
        //await _vm.Initialize();
    }
    protected override async void OnAppearing()
    {
        base.OnAppearing();
        //await _vm.Initialize();
    }

    private void OnSearchBarTextChanged(object sender, TextChangedEventArgs e)
    {
        _vm.LoadFilteredSongsCommand.Execute(null);
    }

}