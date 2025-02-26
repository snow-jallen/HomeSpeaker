//using AndroidX.Lifecycle;
using HomeSpeaker.Maui.ViewModels;

namespace HomeSpeaker.Maui.Views;


public partial class SpeakerControllerView : ContentPage
{
    private readonly SpeakerControllerViewModel vm;
	public SpeakerControllerView(SpeakerControllerViewModel vm)
	{
		InitializeComponent();
        this.vm = vm;
        BindingContext = vm;
	}

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await vm.InitializeAsync();
    }
}