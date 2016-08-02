using System;
using System.Linq;
using System.Net;
using AudioBooksPlayer.WPF.Model;
using Newtonsoft.Json.Serialization;

namespace AudioBooksPlayer.WPF.Streaming
{
    struct AudioBooksBroadcastStructur
    {
        public AudioBooksInfo[] Books { get; set; }
        public int TcpCommandPort { get; set; }
    }
    public class AudioBooksInfoBroadcast
    {
        internal AudioBooksInfoBroadcast(AudioBooksBroadcastStructur broadcast, IPAddress addrss)
        {
            Books = broadcast.Books;
            TcpCommandsPort = broadcast.TcpCommandPort;
            LastDiscoveryUtcTime = DateTime.UtcNow;
            IpAddress = addrss;
        }

        public AudioBooksInfoBroadcast() { }
        public DateTime LastDiscoveryUtcTime { get; set; }
        public AudioBooksInfo[] Books { get; set; }
        public IPAddress IpAddress { get; set; }
        public int TcpCommandsPort { get; set; }
    }

    public class AudioBookInfoRemote
    {
        public AudioBookInfoRemote(AudioBooksInfo info, IPAddress address)
        {
            Book = info;
            IpAddress = address;
        }
        public AudioBooksInfo Book { get; set; }
        public IPAddress IpAddress { get; set; }
    }

    public class AudioBooksInfoRemote
    {
        public AudioBooksInfoRemote(AudioBooksInfoBroadcast copy)
        {
            LastDiscoveryUtcTime = copy.LastDiscoveryUtcTime;
            IpAddress = copy.IpAddress;
            Books = copy.Books.Select(x => new AudioBookInfoRemote(x, copy.IpAddress)).ToArray();
        }
        public DateTime LastDiscoveryUtcTime { get; set; }
        public AudioBookInfoRemote[] Books { get; set; }
        public IPAddress IpAddress { get; set; }
    }
}