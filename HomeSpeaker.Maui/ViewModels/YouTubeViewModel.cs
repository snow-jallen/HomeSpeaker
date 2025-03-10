//using Android.App.AppSearch;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HomeSpeaker.Maui.Services;
using HomeSpeaker.Shared;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Runtime.Intrinsics.Arm;
using System.Text;
using System.Threading.Tasks;
using Grpc.Core;
using HomeSpeaker.Maui.Models;
using YoutubeExplode.Common;
using System.Runtime.CompilerServices;
namespace HomeSpeaker.Maui.ViewModels;

public partial class YouTubeViewModel : ObservableObject, IQueryAttributable
{
    [ObservableProperty]
    ObservableCollection<YouTubeVideoViewModel> videos;
    [ObservableProperty]
    string searchTerm;
    [RelayCommand]
    public async void Search()
    {
        Videos = new ObservableCollection<YouTubeVideoViewModel>();
        var result = await device._grpcClient.SearchAsync(SearchTerm);
        foreach(Video video in result)
        {
            Videos.Add(new YouTubeVideoViewModel(video, device._grpcClient));
        }
    }
    [ObservableProperty]
    DeviceModel device;
    public async void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        device = (DeviceModel)query["device"];
    }
}
public partial class YouTubeVideoViewModel : ObservableObject
{
    Video video;
    HomeSpeakerClientService hscs;
    public YouTubeVideoViewModel(Video video, HomeSpeakerClientService hscs)
    {
        this.video = video;
        this.hscs = hscs;
        Name = video.Title;
        Author = video.Author;
    }
    
    [ObservableProperty]
    string name;
    [ObservableProperty]
    string author;
    [ObservableProperty]
    bool isDownloading;
    [ObservableProperty]
    bool isComplete;
    [ObservableProperty]
    double progressValue;
    partial void OnIsCompleteChanged(bool value)
    {
        IsnComplete=!IsComplete;
    }
    [ObservableProperty]
    public bool isnComplete=true;
    [RelayCommand]
    async void CacheVideoAsync()
    {
        IsDownloading = true;
        var cacheCallReply = hscs.CacheVideo(video);
        await foreach (var reply in cacheCallReply.ResponseStream.ReadAllAsync())
        {
            ProgressValue = reply.PercentComplete;
        }

        IsDownloading = false;
        IsComplete = true;
    }
}