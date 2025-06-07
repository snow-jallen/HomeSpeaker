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
    [ObservableProperty]
    string errors = null;
    DeviceViewerService dvs { set; get; }
    PersistanceService persistanceService;
    public ManageDevicesViewModel(DeviceViewerService dvs, HomeSpeakerClientFactory factory, PersistanceService persistanceService)
    {
        Devices = new ObservableCollection<DeviceModel>();
        Path = "";
        Name = "";
        //this.dvs = dvs;
        //Servers = new(dvs.Servers);
        this.persistanceService = persistanceService;
        _factory = factory;
    }
    public async Task Initialize()
    {
        foreach(var kvpair in persistanceService.DeviceNames)
        {
            if (kvpair.Value.Length>1&&kvpair.Value[1]!="")
                Devices.Add(new(kvpair.Value[0], kvpair.Value[1], _factory.Create(kvpair.Value[1])));
        }    
        
    }
    [RelayCommand]
    async Task AddServer()
    {
        //var ser = new Server() { Name = this.Name, Path = this.Path };
        //dvs.Servers.Add(ser);
        //servers.Add(ser);
        Errors = null;
        try
        {
            var client = _factory.Create(Path);

            try
            {
                await client.GetPlayerStatusAsync();
            }
            catch (Exception ex)
            {
                Errors = "Unable to connect to server check validity";
            }
            finally
            {
                if (Errors == null)
                {
                    Devices.Add(new DeviceModel(Name, Path, client));
                    persistanceService.AddDevice(Name, Path);
                }
            }
        }
        catch (Exception ex)
        {
            Errors = "Invalid Url syntax";
        }
    }
    //[ObservableProperty]
    //ObservableCollection<Server> servers;

    [ObservableProperty]
    private ObservableCollection<DeviceModel> _devices;
}
