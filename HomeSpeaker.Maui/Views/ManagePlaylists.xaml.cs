using HomeSpeaker.Maui.ViewModels;

namespace HomeSpeaker.Maui.Views;

public partial class ManagePlaylists : ContentPage
{
	public ManagePlaylists(ManagePlaylistsViewModel vm)
	{
		InitializeComponent();
		BindingContext = vm;
	}
}