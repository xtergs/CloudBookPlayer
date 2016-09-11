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

	public class RemoteBookInfo
	{
		public string Name { get; set; }
		public string LocalPath { get; set; }
		public TimeSpan LocalTimePosition { get; set; }
		public TimeSpan TimePositoin { get; set; }
	}

	public class RemoteFileInfo
	{
		public string FileName { get; set; }
		public int Order { get; set; }
		public string LocalPath { get; set; }
		public long Length { get; set; }
		public TimeSpan Duration { get; set; }
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
        public AudioBookInfoRemote(AudioBooksInfo info, IPAddress address, int tcpPort)
        {
            Book = info;
            IpAddress = address;
	        TcpPort = tcpPort;
        }
        public AudioBooksInfo Book { get; set; }
        public IPAddress IpAddress { get; set; }
		public int TcpPort { get; set; }
    }

    public class AudioBooksInfoRemote
    {
        public AudioBooksInfoRemote(AudioBooksInfoBroadcast copy)
        {
            LastDiscoveryUtcTime = copy.LastDiscoveryUtcTime;
            IpAddress = copy.IpAddress;
            Books = copy.Books.Select(x => new AudioBookInfoRemote(x, copy.IpAddress, copy.TcpCommandsPort)).ToArray();
        }
        public DateTime LastDiscoveryUtcTime { get; set; }
        public AudioBookInfoRemote[] Books { get; set; }
        public IPAddress IpAddress { get; set; }
    }
}