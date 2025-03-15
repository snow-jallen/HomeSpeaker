using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Google.Protobuf.WellKnownTypes;
using HomeSpeaker.Maui.Models;
using HomeSpeaker.Maui.Services;
using HomeSpeaker.Shared;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HomeSpeaker.Maui.ViewModels
{
    public partial class MusicControllerViewModel : ObservableObject, IQueryAttributable
    {
        private HomeSpeakerClientService Client { get; set; }

        [ObservableProperty]
        private DeviceModel device;

        [ObservableProperty]
        private ObservableCollection<SongViewModel> songs;

        public async Task Initialize()
        {
            // Page is updating, but the server isn't. We initialize everytime we navigate to this page.
            // Song may take awhile to upload
            Songs = new ObservableCollection<SongViewModel>();
            var _songs = await Client.GetAllSongsAsync();
            foreach (SongViewModel song in _songs)
                Songs.Add(song);
            Volume = await Client.GetVolumeAsync();
        }

        [RelayCommand]
        private async Task StopPlaying()
        {
            await Client.StopPlayingAsync();
        }
        [RelayCommand]
        private async Task CreateNew()
        {
            await Shell.Current.GoToAsync("///Custom", new Dictionary<string, object> { { "device", Device } });
        }
        // volume functionality 
        [ObservableProperty]
        private int volume;

        [ObservableProperty]
        private int volumeInput;

        [ObservableProperty]
        private string filterInput;

        [RelayCommand]
        private async Task LoadFilteredSongs()
        {
            // use FilterInput
            // filter by name and/or artist?

            var allSongs = await Client.GetAllSongsAsync();
            var filteredSongs = allSongs.Where(s => s.Name.Contains(FilterInput, StringComparison.OrdinalIgnoreCase) || s.Artist.Contains(FilterInput, StringComparison.OrdinalIgnoreCase)).ToList();

            Songs.Clear();

            foreach (SongViewModel song in filteredSongs)
            {
                Songs.Add(song);
            }
        }


        [RelayCommand]
        public async Task SetVolumeAsync()
        {
            await Client.SetVolumeAsync(VolumeInput);
            Volume = VolumeInput;
        }

        public async void ApplyQueryAttributes(IDictionary<string, object> query)
        {
            Device = (DeviceModel)query["device"];
            Client = Device._grpcClient;
            await Initialize();
        }
        [RelayCommand]
        private async void NavigateToYoutube()
        {
            await Shell.Current.GoToAsync("///YouTube", new Dictionary<string, object> { { "device", Device } });
        }
    }
}
