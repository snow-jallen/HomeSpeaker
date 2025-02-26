using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HomeSpeaker.Maui.ViewModels
{
    public partial class FilePickerViewModel : ObservableObject
    {
        [ObservableProperty]
        FileResult? result;
        [RelayCommand]
        public async void PickFile()
        {
            Result = await FilePicker.PickAsync(new PickOptions()
            {
                FileTypes = new
                FilePickerFileType(new Dictionary<DevicePlatform, IEnumerable<string>>
                {
                    { DevicePlatform.WinUI, new[] { ".mp4", ".txt" } }, // file extension
                }),
                PickerTitle="Pick Song"
            });
            
        }
    }
}
