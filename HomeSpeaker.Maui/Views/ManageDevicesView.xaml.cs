using HomeSpeaker.Maui.ViewModels;

namespace HomeSpeaker.Maui.Views;

public partial class ManageDevicesView : ContentPage
{
    ManageDevicesViewModel vm;

    public ManageDevicesView(ManageDevicesViewModel vm)
	{
		InitializeComponent();
        BindingContext = vm;
        this.vm = vm;
    }
    protected override async void OnAppearing()
    {
        await vm.Initialize();

    }
    
}