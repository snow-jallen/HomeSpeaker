using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HomeSpeaker.Maui.Services;
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
        }

        [RelayCommand]
        private async Task StopPlaying()
        {
            await client.StopPlayingAsync();
        }
    }
}
