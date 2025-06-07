﻿//using Android.Database;
using CommunityToolkit.Maui.Core;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Google.Protobuf.WellKnownTypes;
using HomeSpeaker.Maui.Services;
using HomeSpeaker.Shared;
using CommunityToolkit.Maui.Alerts;
using CommunityToolkit.Maui.Core;
using System.Collections.ObjectModel;

namespace HomeSpeaker.Maui.ViewModels;

public partial class SongViewModel(HomeSpeakerClientService client) : ObservableObject
{
    [ObservableProperty]
    public int _songId;

    [ObservableProperty]
    public string _name;

    private string? path;
    public string? Path
    {
        get => path;
        set
        {
            path = value;
            if (path?.Contains('\\') ?? false)
                Folder = System.IO.Path.GetDirectoryName(path.Replace('\\', '/'));
            else
                Folder = System.IO.Path.GetDirectoryName(path);
        }
    }

    [ObservableProperty]
    private string _album;

    [ObservableProperty]
    private string _artist;

    [ObservableProperty]
    private string? _folder;

    [RelayCommand]
    private async Task PlaySong()
    {
        await client.PlaySongAsync(SongId);
    }

    // update metadata functionality

    [ObservableProperty]
    private bool isEditing;

    [ObservableProperty]
    private string message;

    [ObservableProperty]
    private int updatedSongId;

    [ObservableProperty]
    private string updatedSongName;

    [ObservableProperty]
    private string updatedSongAlbum;

    [ObservableProperty]
    private string updatedSongArtist;

    [RelayCommand]
    private async Task UpdateMetadataAsync()
    {
        if(string.IsNullOrEmpty(UpdatedSongName) && string.IsNullOrEmpty(UpdatedSongAlbum) && string.IsNullOrEmpty(UpdatedSongArtist))
        {
            return;
        }
        else
        {
            if(string.IsNullOrEmpty(UpdatedSongName)) UpdatedSongName = Name;
            if(string.IsNullOrEmpty(UpdatedSongAlbum)) UpdatedSongAlbum = Album;
            if(string.IsNullOrEmpty(UpdatedSongArtist)) UpdatedSongArtist = Artist;
        }

        var success = await client.UpdateSongMetadataAsync(SongId, UpdatedSongName, UpdatedSongAlbum, UpdatedSongArtist);
        if (success)
        {
            Name = UpdatedSongName;
            Album = UpdatedSongAlbum;
            Artist = UpdatedSongArtist;

            Message = "Song metadata updated successfully!";
        }
        else
        {
            Message = "Song metadata could not be updated. Before attempting to edit a song's details, please ensure music is not currently playing.";
        }

        IsEditing = false;
        //await ShowSnackbarAsync(Message);
        await ShowToastAsync(Message);

    }

    private async Task ShowSnackbarAsync(string message)
    {
        var snackbar = Snackbar.Make(message, async () => { await Task.CompletedTask; },  "OK", TimeSpan.FromSeconds(3));

        await snackbar.Show();
    }

    private async Task ShowToastAsync(string message)
    {
        var toast = Toast.Make(message, ToastDuration.Long, 14);
        toast.Show();
    }

    [RelayCommand]
    private void ToggleEdit()
    {
        IsEditing = !IsEditing;
        PlaylistMenuOpen = false;
    }

    [ObservableProperty]
    private bool playlistMenuOpen;

    [RelayCommand]
    private void TogglePlaylists()
    {
        PlaylistMenuOpen = !PlaylistMenuOpen;
        IsEditing = false;
        Playlists = new ObservableCollection<string>(client.Playlists.Select(p => p.PlaylistName));
    }

    [ObservableProperty]
    private ObservableCollection<string> playlists;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(AddToPlaylistCommand))]
    private string playlistName;

    [RelayCommand(CanExecute = nameof(CanAddSongToPlaylist))]
    private async Task AddToPlaylist()
    {
        await client.AddSongToPlaylist(PlaylistName, this);
        PlaylistMenuOpen = false;
    }

    private bool CanAddSongToPlaylist() => PlaylistName != null;
}

public partial class SongGroup : List<SongViewModel>
{
    public string FolderName { get; set; }
    public string FolderPath { get; set; }

    public SongGroup(string name, List<SongViewModel> songs) : base(songs)
    {
        var parts = name.Split('/', '\\');
        FolderName = parts.Last();
        FolderPath = name;
    }
}

public static class ViewModelExtensions
{
    public static SongViewModel ToSongViewModel(this SongMessage song, HomeSpeakerClientService client)
    {
        return new SongViewModel(client)
        {
            SongId = song?.SongId ?? -1,
            Name = song?.Name?.Trim() ?? "[ Null Song Response ??? ]",
            Album = song?.Album?.Trim() ?? "[ No Album ]",
            Artist = song?.Artist?.Trim() ?? "[ No Artist ]",
            Path = song?.Path?.Trim()
        };
    }


    //public async static IAsyncEnumerable<T> ReadAllAsync<T>(this IAsyncStreamReader<T> streamReader, CancellationToken cancellationToken = default)
    //{
    //    if (streamReader == null)
    //    {
    //        throw new System.ArgumentNullException(nameof(streamReader));
    //    }

    //    while (await streamReader.MoveNext(cancellationToken))
    //    {
    //        yield return streamReader.Current;
    //    }
    //}

}