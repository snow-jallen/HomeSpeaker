//using Android.Locations;
using Grpc.Net.Client;
using Grpc.Net.ClientFactory;
using Microsoft.Office.Interop.Excel;
using static HomeSpeaker.Shared.HomeSpeaker;


namespace HomeSpeaker.Maui.Services
{
    public class APIService : IDisposable
    {
        //private HomeSpeaker.Shared.HomeSpeaker _channel;

        public APIService(string address)
        {
            GrpcClientFactory.AllowUnencryptedHttp2 = true;
            using (var channel = GrpcChannel.ForAddress(address))
            {
                var client = channel.CreateGrpcService<HomeSpeakerService>();
            }
        }
        public void Dispose()
        {
            throw new NotImplementedException();
        }
    }
}
