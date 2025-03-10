 using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using HomeSpeaker.Maui.Services;
using CommunityToolkit.Mvvm.Input;
using HomeSpeaker.Maui.Models;

namespace HomeSpeaker.Maui.ViewModels;
public partial class ManageDevicesViewModel : ObservableObject
{
    private readonly HomeSpeakerClientFactory _factory;

    [ObservableProperty]
    string path;
    [ObservableProperty]
    string name;
    DeviceViewerService dvs { set; get; }
    public ManageDevicesViewModel(DeviceViewerService dvs, HomeSpeakerClientFactory factory)
    {
        Devices = new ObservableCollection<DeviceModel>();
        Path = "";
        Name = "";
        //this.dvs = dvs;
        //Servers = new(dvs.Servers);
        _factory = factory;
    }
    [RelayCommand]
    void AddServer()
    {
        //var ser = new Server() { Name = this.Name, Path = this.Path };
        //dvs.Servers.Add(ser);
        //servers.Add(ser);

        var client = _factory.Create(Path);
        Devices.Add(new DeviceModel(Name, Path, client));
    }
    //[ObservableProperty]
    //ObservableCollection<Server> servers;

    [ObservableProperty]
    private ObservableCollection<DeviceModel> _devices;
}
