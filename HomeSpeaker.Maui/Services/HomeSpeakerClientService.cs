using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Grpc.Core;
using Grpc.Net.Client;
using Grpc.Net.Client.Web;
using HomeSpeaker.Maui.ViewModels;
using HomeSpeaker.Shared;
using static HomeSpeaker.Shared.HomeSpeaker;

namespace HomeSpeaker.Maui.Services;
public class HomeSpeakerClientService
{
    private readonly HomeSpeakerClient _client; // HomeSpeakerClient is generated from the proto file

    public HomeSpeakerClientService(string path)
    {
        var channel = GrpcChannel.ForAddress(path);
        _client = new HomeSpeakerClient(channel);
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
    public async Task<GetStatusReply> PausePlayingAsync()
    {
        var result = await _client.GetPlayerStatusAsync(new GetStatusRequest() { });
        await StopPlayingAsync();
        return result;
    }

    public async Task<bool> PlaySongAsync(int songId, Google.Protobuf.WellKnownTypes.Duration? start = null)
    {
        var request = new PlaySongRequest { SongId = songId, StartTime=start };
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
}

