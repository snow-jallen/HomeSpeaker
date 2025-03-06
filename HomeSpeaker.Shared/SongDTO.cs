using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace HomeSpeaker.Shared
{
    public class SongDTO
    {
        public string Name { get; set; }
        public string Album { get; set; }
        public string Artist { get; set; }
        public MultipartFormDataContent File { get; set; }
    }
}
