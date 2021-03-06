﻿using MvvmHelpers;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using Xamarin.Essentials;
using Xamarin.Forms;
using static HomeSpeaker.Server.gRPC.HomeSpeaker;

namespace HomeSpeaker.Mobile.ViewModels
{
    public class StarredViewModel : BaseViewModel
    {
        public StarredViewModel()
        {
            Songs = new ObservableCollection<SongGroup>();
            this.client = DependencyService.Get<HomeSpeakerClient>();
            Title = "Starred";
            init();
        }

        private readonly HomeSpeakerClient client;
        public Command LoginCommand { get; }
        public ObservableCollection<SongGroup> Songs { get; private set; }
        public ObservableRangeCollection<SongViewModel> Songs2 { get; private set; } = new ();
        public ObservableRangeCollection<Grouping<SongGroup, SongViewModel>> Songs2Groups { get; private set; } = new ();

        private string status;
        public string Status
        {
            get => status;
            set
            {
                SetProperty(ref status, value);
                OnPropertyChanged(nameof(StatusIsVisible));
            }
        }
        public bool StatusIsVisible => String.IsNullOrWhiteSpace(Status) is false;

        private async void init()
        {
            Status = "getting song info...";
            var groups = new Dictionary<string, List<SongViewModel>>();
            var getSongsReply = client.GetSongs(new Server.gRPC.GetSongsRequest { });
            var starredSongs = (await App.Database.GetStarredSongsAsync()).Select(s => s.Path).ToList();
            await foreach (var reply in getSongsReply.ResponseStream.ReadAllAsync())
            {
                foreach (var s in reply.Songs.Where(s => starredSongs.Contains(s.Path)))
                {
                    var song = s.ToSongViewModel();
                    if (groups.ContainsKey(song.Folder) is false)
                        groups[song.Folder] = new List<SongViewModel>();
                    groups[song.Folder].Add(song);

                    Songs2.Add(song);
                }
            }

            foreach(var folder in Songs2.Select(s=>s.Folder).Distinct())
            {
                var songsInFolder = Songs2.Where(s => s.Folder == folder).ToList();
                var key = new SongGroup(folder, songsInFolder);
                Songs2Groups.Add(new Grouping<SongGroup, SongViewModel>(key, songsInFolder));
            }

            foreach (var group in groups.OrderBy(g => g.Key))
            {
                Songs.Add(new SongGroup(group.Key, group.Value.OrderBy(s=>s.Path).ToList()));
            }

            Status = null;

            // Prefixing with `//` switches to a different navigation stack instead of pushing to the active one
            //await Shell.Current.GoToAsync($"//{nameof(AboutPage)}");

        }
    }
}