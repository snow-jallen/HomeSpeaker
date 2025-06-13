using System;

namespace HomeSpeaker.Shared
{
    public record AirPlayStatus
    {
        public bool IsConnected { get; init; }
        public string DeviceName { get; init; } = string.Empty;
        public DateTime ConnectedAt { get; init; }
        public string ClientIpAddress { get; init; } = string.Empty;
    }
}
