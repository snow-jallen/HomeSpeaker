using Foundation;
using Intents;
using HomeSpeaker.MauiApp.Services;

namespace HomeSpeaker.MauiApp.Platforms.iOS.Intents;

public class PlayPlaylistIntentHandler : INExtension, IINPlayPlaylistIntentHandling
{
    protected PlayPlaylistIntentHandler(IntPtr handle) : base(handle)
    {
    }

    public void HandlePlayPlaylist(INPlayPlaylistIntent intent, Action<INPlayPlaylistIntentResponse> completion)
    {
        var playlistName = intent.PlaylistName;
        var serverNickname = intent.ServerNickname;

        if (string.IsNullOrEmpty(playlistName) || string.IsNullOrEmpty(serverNickname))
        {
            var response = new INPlayPlaylistIntentResponse(INPlayPlaylistIntentResponseCode.Failure, null);
            completion(response);
            return;
        }

        // Execute the intent with proper error handling
        ExecuteIntentAsync(playlistName, serverNickname, completion).ContinueWith(task =>
        {
            if (task.IsFaulted)
            {
                System.Diagnostics.Debug.WriteLine($"Intent execution failed: {task.Exception?.GetBaseException().Message}");
                var response = new INPlayPlaylistIntentResponse(INPlayPlaylistIntentResponseCode.Failure, null);
                completion(response);
            }
        });
    }

    private async Task ExecuteIntentAsync(string playlistName, string serverNickname, Action<INPlayPlaylistIntentResponse> completion)
    {
        try
        {
            var serverConfigService = new ServerConfigurationService();
            var clientService = new HomeSpeakerClientService();

            var servers = await serverConfigService.GetServersAsync();
            var server = servers.FirstOrDefault(s => 
                s.Nickname.Equals(serverNickname, StringComparison.OrdinalIgnoreCase));

            if (server == null)
            {
                var response = new INPlayPlaylistIntentResponse(INPlayPlaylistIntentResponseCode.Failure, null);
                completion(response);
                return;
            }

            await clientService.PlayPlaylistAsync(server.ServerUrl, playlistName);

            var successResponse = new INPlayPlaylistIntentResponse(INPlayPlaylistIntentResponseCode.Success, null);
            completion(successResponse);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error executing intent: {ex.Message}");
            var response = new INPlayPlaylistIntentResponse(INPlayPlaylistIntentResponseCode.Failure, null);
            completion(response);
        }
    }

    public void ConfirmPlayPlaylist(INPlayPlaylistIntent intent, Action<INPlayPlaylistIntentResponse> completion)
    {
        var response = new INPlayPlaylistIntentResponse(INPlayPlaylistIntentResponseCode.Ready, null);
        completion(response);
    }

    public void ResolvePlaylistName(INPlayPlaylistIntent intent, Action<INStringResolutionResult> completion)
    {
        if (!string.IsNullOrEmpty(intent.PlaylistName))
        {
            completion(INStringResolutionResult.GetSuccess(intent.PlaylistName));
        }
        else
        {
            completion(INStringResolutionResult.NeedsValue);
        }
    }

    public void ResolveServerNickname(INPlayPlaylistIntent intent, Action<INStringResolutionResult> completion)
    {
        if (!string.IsNullOrEmpty(intent.ServerNickname))
        {
            completion(INStringResolutionResult.GetSuccess(intent.ServerNickname));
        }
        else
        {
            completion(INStringResolutionResult.NeedsValue);
        }
    }

    public void ProvidePlaylistNameOptions(INPlayPlaylistIntent intent, Action<INObjectCollection<NSString>, NSError> completion)
    {
        ProvidePlaylistNamesAsync(intent, completion).ContinueWith(task =>
        {
            if (task.IsFaulted)
            {
                System.Diagnostics.Debug.WriteLine($"Error providing playlist options: {task.Exception?.GetBaseException().Message}");
            }
        });
    }

    private async Task ProvidePlaylistNamesAsync(INPlayPlaylistIntent intent, Action<INObjectCollection<NSString>, NSError> completion)
    {
        try
        {
            var serverConfigService = new ServerConfigurationService();
            var clientService = new HomeSpeakerClientService();

            var server = await serverConfigService.GetDefaultServerAsync();
            if (server == null)
            {
                completion(null, null);
                return;
            }

            var playlists = await clientService.GetPlaylistsAsync(server.ServerUrl);
            var playlistNames = playlists.Select(p => new NSString(p.PlaylistName)).ToArray();
            var collection = new INObjectCollection<NSString>(playlistNames);
            
            completion(collection, null);
        }
        catch
        {
            completion(null, null);
        }
    }

    public void ProvideServerNicknameOptions(INPlayPlaylistIntent intent, Action<INObjectCollection<NSString>, NSError> completion)
    {
        ProvideServerNicknamesAsync(intent, completion).ContinueWith(task =>
        {
            if (task.IsFaulted)
            {
                System.Diagnostics.Debug.WriteLine($"Error providing server options: {task.Exception?.GetBaseException().Message}");
            }
        });
    }

    private async Task ProvideServerNicknamesAsync(INPlayPlaylistIntent intent, Action<INObjectCollection<NSString>, NSError> completion)
    {
        try
        {
            var serverConfigService = new ServerConfigurationService();
            var servers = await serverConfigService.GetServersAsync();
            var serverNicknames = servers.Select(s => new NSString(s.Nickname)).ToArray();
            var collection = new INObjectCollection<NSString>(serverNicknames);
            
            completion(collection, null);
        }
        catch
        {
            completion(null, null);
        }
    }
}

// Intent and IntentResponse definitions would normally be auto-generated
// from the .intentdefinition file. For this example, we define them manually.

[Register("INPlayPlaylistIntent")]
public class INPlayPlaylistIntent : INIntent
{
    protected INPlayPlaylistIntent(IntPtr handle) : base(handle) { }

    [Export("playlistName")]
    public string PlaylistName
    {
        get => (NSString)ValueForKey(new NSString("playlistName"));
        set => SetValueForKey(new NSString(value), new NSString("playlistName"));
    }

    [Export("serverNickname")]
    public string ServerNickname
    {
        get => (NSString)ValueForKey(new NSString("serverNickname"));
        set => SetValueForKey(new NSString(value), new NSString("serverNickname"));
    }
}

public enum INPlayPlaylistIntentResponseCode
{
    Success,
    Failure,
    Ready
}

[Register("INPlayPlaylistIntentResponse")]
public class INPlayPlaylistIntentResponse : INIntentResponse
{
    public INPlayPlaylistIntentResponse(INPlayPlaylistIntentResponseCode code, NSUserActivity? userActivity) 
        : base()
    {
        Code = code;
    }

    protected INPlayPlaylistIntentResponse(IntPtr handle) : base(handle) { }

    public INPlayPlaylistIntentResponseCode Code { get; set; }
}

public interface IINPlayPlaylistIntentHandling
{
    void HandlePlayPlaylist(INPlayPlaylistIntent intent, Action<INPlayPlaylistIntentResponse> completion);
    void ConfirmPlayPlaylist(INPlayPlaylistIntent intent, Action<INPlayPlaylistIntentResponse> completion);
    void ResolvePlaylistName(INPlayPlaylistIntent intent, Action<INStringResolutionResult> completion);
    void ResolveServerNickname(INPlayPlaylistIntent intent, Action<INStringResolutionResult> completion);
    void ProvidePlaylistNameOptions(INPlayPlaylistIntent intent, Action<INObjectCollection<NSString>, NSError> completion);
    void ProvideServerNicknameOptions(INPlayPlaylistIntent intent, Action<INObjectCollection<NSString>, NSError> completion);
}
