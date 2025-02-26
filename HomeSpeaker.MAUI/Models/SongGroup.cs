using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HomeSpeaker.MAUI.Models
{
    public partial class SongGroup : List<SongModel>
    {
        string FolderName { get; set; }
        string FolderPath { get; set; }

        public SongGroup(string name, List<SongModel> songs) : base(songs)
        {
            FolderName = name;
            FolderPath = name;
        }
    }
}
