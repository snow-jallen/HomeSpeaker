using HomeSpeaker.Maui.ViewModels;

namespace HomeSpeaker.Maui.Views;

public partial class MusicController : ContentPage
{
	public MusicController(MusicControllerViewModel vm)
	{
		InitializeComponent();
		BindingContext = vm;
	}
}