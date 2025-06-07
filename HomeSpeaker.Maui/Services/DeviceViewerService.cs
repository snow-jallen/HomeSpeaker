using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HomeSpeaker.Maui.Services;

public class DeviceViewerService
{
    public Server Current { set; get; }
    public List<Server> Servers { set; get; }
}

public class Server
{
    public string Name { set; get; }
    public string Path { set; get; }
}
