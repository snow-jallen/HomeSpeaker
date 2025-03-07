using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Google.Protobuf.WellKnownTypes;
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
    public partial class MusicControllerViewModel(HomeSpeakerClientService client) : ObservableObject
    {

        [ObservableProperty]
        private ObservableCollection<SongViewModel> songs;

        public async Task Initialize()
        {
            Songs = new ObservableCollection<SongViewModel>();
            var _songs = await client.GetAllSongsAsync();
            foreach(SongViewModel song in _songs)
                Songs.Add(song);
            Volume = await client.GetVolumeAsync();
            
        }

        [RelayCommand]
        private async Task StopPlaying()
        {
            await client.StopPlayingAsync();
        }
        [RelayCommand]
        private async Task CreateNew()
        {
            await Shell.Current.GoToAsync("///Custom");
        }
        // volume functionality 
        [ObservableProperty]
        private int volume;

        [ObservableProperty]
        private int volumeInput;


        [RelayCommand]
        public async Task SetVolumeAsync()
        {
            await client.SetVolumeAsync(VolumeInput);
            Volume = VolumeInput;
        }


      
    }
}
