using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HomeSpeaker.MAUI.Services;
using HomeSpeaker.Shared;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HomeSpeaker.MAUI.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly HomeSpeakerMauiService homeSpeakerService;

    public ObservableCollection<Song> Songs { get; private set; }
    public string StatusMessage { get; private set; }

    public MainViewModel()
    {
        homeSpeakerService = new HomeSpeakerMauiService("http://192.168.144.1");
    }

    [RelayCommand]
    public async Task LoadSongsAsync()
    {
        var allSongs = await homeSpeakerService.GetAllSongsAsync();
        Songs = new ObservableCollection<Song>(allSongs);
        StatusMessage = $"Loaded {allSongs.Count} songs.";
    }

    [RelayCommand]
    public async Task PlaySongAsync(Song selectedSong)
    {
        var reply = await homeSpeakerService.PlaySongAsync(selectedSong.SongId);
        if (reply.Ok)
        {
            StatusMessage = $"Playing: {selectedSong.Name}";
        }
        else
        {
            StatusMessage = "Server said: not OK to play that song.";
        }
    }

    [RelayCommand]
    public async Task GetStatusAsync()
    {
        var status = await homeSpeakerService.GetStatusAsync();
        StatusMessage = $"Current: {status.CurrentSong?.Name} / Volume: {status.Volume}";
    }
}