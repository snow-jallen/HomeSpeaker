using CommunityToolkit.Mvvm.ComponentModel;
using HomeSpeaker.Maui.Models;
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
    public class PlaylistService(HomeSpeakerClientService _client)
    {
        public Dictionary<string, PlaylistModel> _playlists = new();

        public async Task Sync()
        {
            var playlists = await _client.GetPlaylistsAsync();
            foreach (var playlist in playlists)
                _playlists.Add(playlist.PlaylistName, new PlaylistModel(playlist, _client));
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
}
