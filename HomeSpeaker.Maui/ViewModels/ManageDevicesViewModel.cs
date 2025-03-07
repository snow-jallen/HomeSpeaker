using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using HomeSpeaker.Maui.Services;
using CommunityToolkit.Mvvm.Input;

namespace HomeSpeaker.Maui.ViewModels;
public partial class ManageDevicesViewModel : ObservableObject
{
    [ObservableProperty]
    string path;
    [ObservableProperty]
    string name;
    DeviceViewerService dvs { set; get; }
    public ManageDevicesViewModel(DeviceViewerService dvs)
    {
        this.dvs = dvs;
        Servers = new(dvs.Servers);
    }
    [RelayCommand]
    void AddServer()
    {
        var ser = new Server() { Name = this.Name, Path=this.Path };
        dvs.Servers.Add(ser);
        servers.Add(ser);
    }
    [ObservableProperty]
    ObservableCollection<Server> servers;
}
