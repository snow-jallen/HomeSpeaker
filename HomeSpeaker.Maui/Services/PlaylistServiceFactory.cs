using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HomeSpeaker.Maui.Services
{
    public class PlaylistServiceFactory
    {
        public PlaylistService Create(HomeSpeakerClientService client)
        {
            return new PlaylistService(client);
        }
    }
}
