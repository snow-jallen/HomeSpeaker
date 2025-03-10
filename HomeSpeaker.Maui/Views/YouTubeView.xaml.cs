using HomeSpeaker.Maui.ViewModels;

namespace HomeSpeaker.Maui.Views;

public partial class YouTubeView : ContentPage
{
	public YouTubeView(YouTubeViewModel vm)
	{
		InitializeComponent();
		BindingContext = vm;
	}
}