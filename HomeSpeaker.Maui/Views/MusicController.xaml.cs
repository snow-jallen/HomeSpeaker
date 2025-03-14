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

    private CancellationTokenSource _cts;

    private void OnSliderValueChanged(object sender, ValueChangedEventArgs e)
    {
        // dispose cts and create a new one
        if (_cts != null)
        {
            _cts.Cancel();
            _cts.Dispose();
        }

        _cts = new CancellationTokenSource();

        Task.Delay(300, _cts.Token)
            .ContinueWith(async t =>
            {
                // if the token in the cts doesn't have a cancellation request, call SetVolumeAsync
                if (!t.IsCanceled) 
                {
                    _vm.VolumeInput = (int)e.NewValue;
                    await _vm.SetVolumeAsync();
                }
            }, TaskScheduler.FromCurrentSynchronizationContext()); // tells the task to continue on the same thread after the delay
    }


}