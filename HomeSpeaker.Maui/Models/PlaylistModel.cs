//using Android.OS;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HomeSpeaker.Maui.Services;
using HomeSpeaker.Maui.ViewModels;
using HomeSpeaker.Shared;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HomeSpeaker.Maui.Models
{
    public partial class PlaylistModel : ObservableObject
    {
        [ObservableProperty]
        public string playlistName;

        [ObservableProperty]
        public ObservableCollection<SongViewModel> songs;

        [ObservableProperty]
        private bool songsAreVisible;

        [ObservableProperty]
        private bool songsAreNotVisible;

        public Task Shuffle;

        public HomeSpeakerClientService _client;

        public PlaylistModel(PlaylistMessage playlist, HomeSpeakerClientService client)
        {
            _client = client;
            SongsAreVisible = false;
            SongsAreNotVisible = true;
            PlaylistName = playlist.PlaylistName;
            Songs = new ObservableCollection<SongViewModel>(playlist.Songs.Select(s => s.ToSongViewModel(_client)));
        }
        [RelayCommand]
        private async Task  ShuffleSongs()
        {
            Random r = new();
            Songs = new ObservableCollection<SongViewModel>(Songs.ToList().OrderBy<SongViewModel, int>((p)=>r.Next()));
            await Shuffle;
        }
        [RelayCommand]
        private void SeeSongs()
        {
            SongsAreVisible = !SongsAreVisible;
            SongsAreNotVisible = !SongsAreVisible;
        }

        [RelayCommand]
        private async Task PlaySongs()
        {
            await _client.PlayPlaylistAsync(PlaylistName);
        }
    }
}
