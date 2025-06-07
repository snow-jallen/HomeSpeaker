using HomeSpeaker.Maui.ViewModels;

namespace HomeSpeaker.Maui.Views;

public partial class QueueView : ContentPage
{
	public QueueView(QueueViewModel vm)
	{
		InitializeComponent();
		BindingContext = vm;
	}
}