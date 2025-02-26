using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Grpc.Net.Client;
using Grpc.Net.Client.Web;
using HomeSpeaker.Shared;
using static HomeSpeaker.Shared.HomeSpeaker;

namespace HomeSpeaker.Maui.Services;
public class HomeSpeakerClientService
{
    private readonly HomeSpeakerClient _client; // HomeSpeakerClient is generated from the proto file

    //public HomeSpeakerClientService()
    //{
    //    var channel = GrpcChannel.ForAddress("http://localhost:5280"); // hard-coding this for now
    //    _client = new HomeSpeakerClient(channel);
    //}

    public HomeSpeakerClientService()
    {
        var httpHandler = new GrpcWebHandler(GrpcWebMode.GrpcWeb, new HttpClientHandler());

        var channel = GrpcChannel.ForAddress("http://localhost:5280", new GrpcChannelOptions
        {
            HttpHandler = httpHandler
        });

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
}

