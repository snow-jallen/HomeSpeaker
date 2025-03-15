using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using HomeSpeaker.Maui.Models;
using HomeSpeaker.Maui.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HomeSpeaker.Maui.ViewModels
{
    public partial class PlaylistViewModel : ObservableObject, IQueryAttributable
    {
        private HomeSpeakerClientService client;

        [ObservableProperty]
        private ObservableCollection<PlaylistModel> playlists;
        
        private void Sync()
        {
            Playlists = new ObservableCollection<PlaylistModel>(client.Playlists);
            OnPropertyChanged(nameof(Playlists));
        }

        public void ApplyQueryAttributes(IDictionary<string, object> query)
        {
            client = (HomeSpeakerClientService)query["device"];
            Sync();
        }
    }
}
