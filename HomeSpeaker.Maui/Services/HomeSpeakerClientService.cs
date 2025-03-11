using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.Intrinsics.Arm;
using System.Text;
using System.Threading.Tasks;
using Grpc.Core;
using Grpc.Net.Client;
using Grpc.Net.Client.Web;
using HomeSpeaker.Maui.ViewModels;
using HomeSpeaker.Shared;
using Microsoft.Extensions.Logging;
using static HomeSpeaker.Shared.HomeSpeaker;

namespace HomeSpeaker.Maui.Services;
public class HomeSpeakerClientService
{
    private readonly HomeSpeakerClient _client; // HomeSpeakerClient is generated from the proto file
    private DeviceViewerService deviceViewerService;
    public HomeSpeakerClientService(string path)
    {
        var channel = GrpcChannel.ForAddress(path);
        _client = new HomeSpeakerClient(channel);
        this.deviceViewerService = deviceViewerService;
    }

    public async Task SetVolumeAsync(int volume0to100)
    {
        var request = new PlayerControlRequest { SetVolume = true, VolumeLevel = volume0to100 };
        await _client.PlayerControlAsync(request);
    }

    public async Task<int> GetVolumeAsync()
    {
        var status = await GetPlayerStatusAsync();
        return status.Volume;
    }

    public async Task<List<PlaylistMessage>> GetPlaylistsAsync()
    {
        var request = new GetPlaylistsRequest();
        var response = await _client.GetPlaylistsAsync(request);
        return response.Playlists.ToList();
    }

    public async Task<IEnumerable<SongViewModel>> GetAllSongsAsync()
    {
        var songs = new List<SongViewModel>();
        var getSongsReply = _client.GetSongs(new GetSongsRequest { });
        await foreach (var reply in getSongsReply.ResponseStream.ReadAllAsync())
        {
            songs.AddRange(reply.Songs.Select(s => s.ToSongViewModel(this)));
        }

        return songs;
    }

    public async Task StopPlayingAsync() => await _client.PlayerControlAsync(new PlayerControlRequest { Stop = true });

    public async Task<bool> PlaySongAsync(int songId)
    {
        var request = new PlaySongRequest { SongId = songId };
        var response = await _client.PlaySongAsync(request);
        return response.Ok;
    }

    public async Task<GetStatusReply> GetPlayerStatusAsync()
    {
        var request = new GetStatusRequest();
        var reply = await _client.GetPlayerStatusAsync(request);
        return reply;
    }


    public async Task<bool> UpdateSongMetadataAsync(int songId, string songName, string album, string artist)
    {
            var request = new UpdateSongMetadataRequest
            {
                SongName = songName,
                SongId = songId,
                Album = album,
                Artist = artist
            };
            var reply = await _client.UpdateSongMetadataAsync(request);
            return reply.Success;
    }
    public async Task<IEnumerable<Video>> SearchAsync(string searchTerm)
    { 
        var response = await _client.SearchViedoAsync(new SearchVideoRequest { SearchTerm = searchTerm });
        var videos = response.Results;
        return videos;
    }
    public AsyncServerStreamingCall<CacheVideoReply> CacheVideo(Video video)
    {
        return _client.CacheVideo(new CacheVideoRequest() {Video=video});
    }

    public async Task AddToPlaylistAsync(string playlistName, string songPath)
    {
        await _client.AddSongToPlaylistAsync(new AddSongToPlaylistRequest { PlaylistName = playlistName, SongPath = songPath });
    }

    public async Task RemoveFromPlaylistAsync(string playlistName, string songPath)
    {
        await _client.RemoveSongFromPlaylistAsync(new RemoveSongFromPlaylistRequest { PlaylistName = playlistName, SongPath = songPath });
    }

    public async Task PlayPlaylistAsync(string playlistName)
    {
        await _client.PlayPlaylistAsync(new PlayPlaylistRequest { PlaylistName = playlistName });
    }
}

