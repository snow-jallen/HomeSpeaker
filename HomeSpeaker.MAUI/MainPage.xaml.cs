using HomeSpeaker.Shared;
using System.Collections.ObjectModel;

namespace HomeSpeaker.MAUI
{
    public partial class MainPage : ContentPage
    {

        private readonly HomeSpeaker.HomeSpeakerClient _client;
        private ObservableCollection<SongMessage> _songs = new();

        public MainPage(HomeSpeaker.HomeSpeakerClient client)
        {
            InitializeComponent();
            _client = client;
            SongsListView.ItemsSource = _songs;
        }

        private async void OnGetSongsClicked(object sender, EventArgs e)
        {
            _songs.Clear();

            try
            {
                var call = _client.GetSongs(new GetSongsRequest { Folder = "" });
                while (await call.ResponseStream.MoveNext())
                {
                    var reply = call.ResponseStream.Current;
                    foreach (var song in reply.Songs)
                    {
                        _songs.Add(song);
                    }
                }
            }
            catch (Exception ex)
            {
                await DisplayAlert("Error", $"Could not load songs: {ex.Message}", "OK");
            }
        }

        private async void OnGetStatusClicked(object sender, EventArgs e)
        {
            try
            {
                var reply = await _client.GetPlayerStatusAsync(new GetStatusRequest());
                StatusLabel.Text = $"Now playing: {reply.CurrentSong?.Name ?? "(none)"}  " +
                                   $"Vol: {reply.Volume}, " +
                                   $"Elapsed: {reply.Elapsed.ToTimeSpan()}, " +
                                   $"StillPlaying: {reply.StilPlaying}";
            }
            catch (Exception ex)
            {
                await DisplayAlert("Error", $"Could not get status: {ex.Message}", "OK");
            }
        }

    }

}
