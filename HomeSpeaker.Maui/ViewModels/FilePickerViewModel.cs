using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using HomeSpeaker.Shared;
using System.Net.Http.Headers;
using System.Net.Http;
using System.Net.Http.Json;

namespace HomeSpeaker.Maui.ViewModels
{
    public partial class FilePickerViewModel : ObservableObject
    {
        [ObservableProperty]
        FileResult? result;
        [ObservableProperty]
        string name;
        [ObservableProperty]
        string album;
        [ObservableProperty]
        string artist;
        [ObservableProperty]
        Song song;
        [RelayCommand]
        public async void PickFile()
        {
            Result = await FilePicker.PickAsync(new PickOptions()
            {
                FileTypes = new
                FilePickerFileType(new Dictionary<DevicePlatform, IEnumerable<string>>
                {
                    { DevicePlatform.WinUI, new[] { ".mp3", ".txt" } }, // file extension
                }),
                PickerTitle="Pick Song"
            });
        }
        [RelayCommand]
        public async void Send()
        {
            HttpClient httpClient = new();
            using (var multipartFormContent = new MultipartFormDataContent())
            {
                var fileStreamContent = new StreamContent(File.OpenRead(Result.FullPath));
                fileStreamContent.Headers.ContentType = new MediaTypeHeaderValue("audio/mp4");

                multipartFormContent.Add(fileStreamContent, name: Name, fileName: Name);

                var song = new SongDTO
                {
                    Album = this.Album,
                    Artist = this.Artist,
                    Name = this.Name,
                    File = multipartFormContent
                };


                var response = await httpClient.PostAsync("https://localhost:7238/files/add", multipartFormContent);
                response.EnsureSuccessStatusCode();
                await response.Content.ReadAsStringAsync();
            }
        }
    }
}
