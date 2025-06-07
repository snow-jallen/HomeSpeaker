using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HomeSpeaker.Maui.Models;
using HomeSpeaker.Maui.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HomeSpeaker.Maui.ViewModels
{
    public partial class QueueViewModel : ObservableObject, IQueryAttributable
    {
        private DeviceModel _device;
        private HomeSpeakerClientService _client;

        [ObservableProperty]
        public ObservableCollection<SongViewModel> _songs;

        public void Sync()
        {
            Songs = new ObservableCollection<SongViewModel>(_client.Queue);
        }

        [RelayCommand]
        private async Task Shuffle()
        {
            await _client.ShuffleQueueAsync();
            Sync();
        }

        [RelayCommand]
        private async Task Clear()
        {
            await _client.ClearQueueAsync();
            Sync();
        }

        public void ApplyQueryAttributes(IDictionary<string, object> query)
        {
            _device = (DeviceModel)query["device"];
            _client = _device._grpcClient;
            Sync();
        }


    }
}
