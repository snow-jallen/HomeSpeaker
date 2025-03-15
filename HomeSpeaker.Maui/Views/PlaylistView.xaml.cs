using HomeSpeaker.Maui.ViewModels;

namespace HomeSpeaker.Maui.Views;

public partial class PlaylistView : ContentPage
{
	public PlaylistView(PlaylistViewModel vm)
	{
		InitializeComponent();
		BindingContext = vm;
	}
}