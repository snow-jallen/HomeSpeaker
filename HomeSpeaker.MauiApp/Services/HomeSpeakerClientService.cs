using Grpc.Net.Client;
using HomeSpeaker.Shared;
using static HomeSpeaker.Shared.HomeSpeaker;

namespace HomeSpeaker.MauiApp.Services;

public interface IHomeSpeakerClientService
{
    Task<List<PlaylistMessage>> GetPlaylistsAsync(string serverUrl);

    Task PlayPlaylistAsync(string serverUrl, string playlistName);

    Task<GetStatusReply?> GetPlayerStatusAsync(string serverUrl);
}

public class HomeSpeakerClientService : IHomeSpeakerClientService
{
    public async Task<List<PlaylistMessage>> GetPlaylistsAsync(string serverUrl)
    {
        try
        {
            using var channel = CreateChannel(serverUrl);
            var client = new HomeSpeakerClient(channel);
            var response = await client.GetPlaylistsAsync(new GetPlaylistsRequest());
            return response.Playlists.ToList();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error getting playlists: {ex.Message}");
            return new List<PlaylistMessage>();
        }
    }

    public async Task PlayPlaylistAsync(string serverUrl, string playlistName)
    {
        try
        {
            using var channel = CreateChannel(serverUrl);
            var client = new HomeSpeakerClient(channel);
            await client.PlayPlaylistAsync(new PlayPlaylistRequest { PlaylistName = playlistName });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error playing playlist: {ex.Message}");
            throw;
        }
    }

    public async Task<GetStatusReply?> GetPlayerStatusAsync(string serverUrl)
    {
        try
        {
            using var channel = CreateChannel(serverUrl);
            var client = new HomeSpeakerClient(channel);
            return await client.GetPlayerStatusAsync(new GetStatusRequest());
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error getting player status: {ex.Message}");
            return null;
        }
    }

    private GrpcChannel CreateChannel(string serverUrl)
    {
        // WARNING: This certificate validation callback is for DEVELOPMENT ONLY
        // In production, implement proper certificate validation
        var httpHandler = new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback = (message, cert, chain, errors) =>
            {
                // TODO: Implement proper certificate validation for production
                // For now, accepting all certificates for development purposes
                System.Diagnostics.Debug.WriteLine($"Certificate validation bypassed for {serverUrl}");
                return true;
            }
        };

        return GrpcChannel.ForAddress(serverUrl, new GrpcChannelOptions
        {
            HttpHandler = httpHandler
        });
    }
}
