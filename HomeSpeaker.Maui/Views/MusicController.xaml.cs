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
	protected override async void OnAppearing()
    {
        base.OnAppearing();
		await _vm.Initialize();
	}

	private async void OnSliderValueChanged(object sender, ValueChangedEventArgs e)
    {
        _vm.VolumeInput = (int)e.NewValue;
        await _vm.SetVolumeAsync();
    }

}