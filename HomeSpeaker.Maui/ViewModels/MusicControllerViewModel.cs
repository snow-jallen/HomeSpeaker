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
        private ObservableCollection<SongViewModel> _songs;


        public async Task Initialize()
        {
            var songs = await client.GetAllSongsAsync();
            Songs = new ObservableCollection<SongViewModel>(songs);

            foreach(SongViewModel songVm in songs)
            {
                songVm.MetadataUpdated += OnSongMetadataUpdated;
            }

            Volume = await client.GetVolumeAsync();

        }

        private async void OnSongMetadataUpdated(SongViewModel updatedSong)
        {
            var songs = await client.GetAllSongsAsync();
        }


        [RelayCommand]
        private async Task StopPlaying()
        {
            await client.StopPlayingAsync();
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
