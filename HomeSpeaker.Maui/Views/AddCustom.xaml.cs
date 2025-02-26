using HomeSpeaker.Maui.ViewModels;
namespace HomeSpeaker.Maui.Views;

public partial class AddCustom : ContentPage
{
	public AddCustom()
	{
		InitializeComponent();
		BindingContext = new FilePickerViewModel();
	}
}