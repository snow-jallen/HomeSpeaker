//using Android.Net.Wifi.Aware;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HomeSpeaker.Maui.Services
{
    public class PersistanceService
    {
        public Dictionary<string, string[]> DeviceNames;
        public static int TotalDevice = 0;

        public PersistanceService()
        {
            DeviceNames = new Dictionary<string, string[]>();
            var list = Preferences.Default.Get<string>("DeviceNames", "").Split(",");
            foreach (var item in list)
            {
                DeviceNames.Add(item, Preferences.Default.Get<string>(item, "`").Split("`"));
            }
            TotalDevice = DeviceNames.Count;
        }
        public void AddDevice(string deviceName, string path)
        {
            DeviceNames.Add(TotalDevice+"", new string[] {deviceName, path});
            Preferences.Default.Set(TotalDevice+"", deviceName+"`"+path);
            Preferences.Default.Set("DeviceNames", string.Join(",", DeviceNames.Keys));
        }
    }
}
