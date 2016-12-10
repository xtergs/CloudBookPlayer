using System;
using System.Linq;
using System.Net;
using AudioBooksPlayer.WPF.Model;
using Newtonsoft.Json;

namespace AudioBooksPlayer.WPF.Streaming
{
    struct AudioBooksBroadcastStructur
    {
        public AudioBooksInfo[] Books { get; set; }
        public int TcpCommandPort { get; set; }
        public string Name { get; set; }
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
            Name = broadcast.Name;
        }

        public AudioBooksInfoBroadcast() { }
        public DateTime LastDiscoveryUtcTime { get; set; }
        public AudioBooksInfo[] Books { get; set; }
        public IPAddress IpAddress { get; set; }
        public int TcpCommandsPort { get; set; }
        public string Name { get; set; }

        [JsonIgnore]
        public bool IsNameSet => !string.IsNullOrWhiteSpace(Name);
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
            if (copy.IsNameSet)
                Name = copy.Name;
            else
                Name = IpAddress.ToString();
            Books = copy.Books.Select(x => new AudioBookInfoRemote(x, copy.IpAddress, copy.TcpCommandsPort)).ToArray();
        }
        public DateTime LastDiscoveryUtcTime { get; set; }
        public AudioBookInfoRemote[] Books { get; set; }
        public IPAddress IpAddress { get; set; }
        public string Name { get; set; }
    }
}