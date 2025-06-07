using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HomeSpeaker.Maui.Services
{
    public class HomeSpeakerClientFactory
    {
        public HomeSpeakerClientService Create(string path)
        {
            return new HomeSpeakerClientService(path);
        }
    }
}
