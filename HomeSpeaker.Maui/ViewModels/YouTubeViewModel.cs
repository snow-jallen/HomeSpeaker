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
namespace HomeSpeaker.Maui.ViewModels;

public partial class YouTubeViewModel(HomeSpeakerClientService hscs) : ObservableObject
{
    [ObservableProperty]
    ObservableCollection<YouTubeVideoViewModel> videos;
    [ObservableProperty]
    string searchTerm;
    [RelayCommand]
    public async void Search()
    {
        Videos = new ObservableCollection<YouTubeVideoViewModel>();
        var result = await hscs.SearchAsync(SearchTerm);
        foreach(Video video in result)
        {
            Videos.Add(new YouTubeVideoViewModel(video, hscs));
        }
    }
}
public partial class YouTubeVideoViewModel(Video video, HomeSpeakerClientService hscs) : ObservableObject
{
    [ObservableProperty]
    Video video;
    [ObservableProperty]
    bool isDownloading;
    [ObservableProperty]
    bool isComplete;
    [ObservableProperty]
    double progressValue;
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