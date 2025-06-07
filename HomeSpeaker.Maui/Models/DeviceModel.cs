using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HomeSpeaker.Maui.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HomeSpeaker.Maui.Models;

public partial class DeviceModel : ObservableObject
{
    public readonly HomeSpeakerClientService _grpcClient;
    public readonly HttpClient _httpClient;

    [ObservableProperty]
    private string _name;

    [ObservableProperty]
    private string _url;

    [RelayCommand]
    private async Task Control()
    {
        await Shell.Current.GoToAsync("///Music", new Dictionary<string, object> { { "device", this } });
    }

    public DeviceModel(string name, string url, HomeSpeakerClientService grpc)
    {
        _grpcClient = grpc;
        Name = name;
        Url = url;
        _httpClient = new HttpClient { BaseAddress = new Uri(uriString: url) };
    }

}
