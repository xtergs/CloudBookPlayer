using System;
using System.Net;
using AudioBooksPlayer.WPF.Model;

namespace AudioBooksPlayer.WPF.Streaming
{
    public class AudioBooksInfoBroadcast
    {
        public DateTime LastDiscoveryUtcTime { get; set; }
        public AudioBooksInfo[] Books { get; set; }
        public IPAddress IpAddress { get; set; }
    }
}