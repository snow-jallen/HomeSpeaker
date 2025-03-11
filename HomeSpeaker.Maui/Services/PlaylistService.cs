using CommunityToolkit.Mvvm.ComponentModel;
using HomeSpeaker.Maui.ViewModels;
using HomeSpeaker.Shared;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HomeSpeaker.Maui.Services
{
    public class PlaylistService
    {
        public Dictionary<string, PlaylistViewModel> _playlists =new();
        public HomeSpeakerClientService _client;

        public async Task Sync()
        {
            var playlists = await _client.GetPlaylistsAsync();
            foreach (var playlist in playlists)
                _playlists.Add(playlist.PlaylistName, new PlaylistViewModel(playlist, _client));
        }
        
        public async Task AddSongToPlaylist(string playlistName, SongViewModel song)
        {
            await _client.AddToPlaylistAsync(playlistName, song.Path);
            await Sync();
        }

        public async Task RemoveSongToPlaylist(string playListName, SongViewModel song)
        {
            await _client.RemoveFromPlaylistAsync(playListName, song.Path);
            await Sync();
        }
    }

    public partial class PlaylistViewModel : ObservableObject
    {
        [ObservableProperty]
        public string playlistName;

        [ObservableProperty]
        public ObservableCollection<SongViewModel> songs;

        public HomeSpeakerClientService _client;

        public PlaylistViewModel(PlaylistMessage playlist, HomeSpeakerClientService client)
        {
            _client = client;
            PlaylistName = playlist.PlaylistName;
            Songs = new ObservableCollection<SongViewModel>(playlist.Songs.Select(s => s.ToSongViewModel(_client)));
        }
    }
}
