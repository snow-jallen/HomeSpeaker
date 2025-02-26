using Grpc.Net.Client.Web;
using Grpc.Net.Client;
using HomeSpeaker.Shared;
using static HomeSpeaker.Shared.HomeSpeaker;
using Grpc.Core;

namespace HomeSpeaker.MAUI.Services;
public class HomeSpeakerMauiService
{
    private HomeSpeakerClient client;

    public HomeSpeakerMauiService(string baseUrl)
    {
        var httpHandler = new GrpcWebHandler(new HttpClientHandler());
        var channel = GrpcChannel.ForAddress(baseUrl, new GrpcChannelOptions
        {
            HttpHandler = httpHandler
        });

        client = new HomeSpeakerClient(channel);
    }

    public async Task<List<Song>> GetAllSongsAsync()
    {
        var result = new List<Song>();
        using var call = client.GetSongs(new GetSongsRequest());
        while (await call.ResponseStream.MoveNext())
        {
            var reply = call.ResponseStream.Current;
            foreach (var songMsg in reply.Songs)
            {
                result.Add(songMsg.ToSong());
            }
        }
        return result;
    }

    public async Task<PlaySongReply> PlaySongAsync(int songId)
    {
        return await client.PlaySongAsync(new PlaySongRequest { SongId = songId });
    }

    public async Task<GetStatusReply> GetStatusAsync()
    {
        return await client.GetPlayerStatusAsync(new GetStatusRequest());
    }
}