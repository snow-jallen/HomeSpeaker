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
using HomeSpeaker.Maui.Models;

namespace HomeSpeaker.Maui.ViewModels
{
    public partial class FilePickerViewModel : ObservableObject, IQueryAttributable
    {
        private DeviceModel _device;
        private HttpClient _client;

        [ObservableProperty]
        FileResult? result;
        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(SendCommand))]
        string name;
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
                PickerTitle = "Pick Song"
            });
            SendCommand.NotifyCanExecuteChanged();
        }
        bool canSend() =>!(name==null||Result==null);

        [RelayCommand(CanExecute =nameof(canSend))]
        public async void Send()
        {
            using (var multipartFormContent = new MultipartFormDataContent())
            {
                var fileStreamContent = new StreamContent(File.OpenRead(Result.FullPath));
                fileStreamContent.Headers.ContentType = new MediaTypeHeaderValue("audio/mp3");

                multipartFormContent.Add(fileStreamContent, name: Name, fileName: Name);

               // multipartFormContent.Add(JsonContent.Create(song));


                var response = await _client.PostAsync("files/add", multipartFormContent);
                response.EnsureSuccessStatusCode();
                await response.Content.ReadAsStringAsync();
                Result = null;
                Name = null;

                await Shell.Current.GoToAsync("///Music", new Dictionary<string, object> { { "device", _device } });
            }
        }

        public void ApplyQueryAttributes(IDictionary<string, object> query)
        {
            _device = (DeviceModel)query["device"];
            _client = _device._httpClient;
        }
    }
}
