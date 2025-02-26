using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HomeSpeaker.Maui.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HomeSpeaker.Maui.ViewModels;
public partial class SpeakerControllerViewModel : ObservableObject
{
    private readonly HomeSpeakerClientService _clientService;

    [ObservableProperty]
    private int volume;

    [ObservableProperty]
    private int volumeInput;


    public SpeakerControllerViewModel(HomeSpeakerClientService clientService)
    {
        _clientService = clientService;
        
    }

    // this method will be called in the OnAppearing method of the view's code-behind
    // there is probably a better way to do this, but for now...
    public async Task InitializeAsync()
    {
        Volume = await _clientService.GetVolumeAsync();
    }

    [RelayCommand]
    public async Task SetVolumeAsync()
    {
        await _clientService.SetVolumeAsync(VolumeInput);
        Volume = VolumeInput;
    }


}
