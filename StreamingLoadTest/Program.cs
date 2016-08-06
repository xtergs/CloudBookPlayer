using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AudioBooksPlayer.WPF.Streaming;

namespace StreamingLoadTest
{
    class Program
    {
        private static int maxCount = 10000;
        private static int batchCount = 50;
        private static List<BookStreamer> streamers = new List<BookStreamer>();
	    private static Timer timer;
         
        static void Main(string[] args)
        {
	        batchCount = int.Parse(System.Configuration.ConfigurationManager.AppSettings.Get("countToAdd"));
			Task.Delay(10000).Wait();
	        timer = new Timer(Callback, null, 1000, 10000);
            DiscoverModule discover = new DiscoverModule();
            discover.DiscoveredNewSource += DiscoverOnDiscoveredNewSource;
            discover.StartListen();
            while (true)
            {
                Task.Delay(10000).Wait();
                Console.WriteLine($"Current count of streamers: {streamers.Count}");
            }
        }

	    private static void Callback(object state)
	    {
			    byte[] buf = new byte[1000*80];
		    foreach (var bookStreamer in streamers.ToArray())
		    {

			    bookStreamer.Stream.Read(buf, 0, buf.Length);
		    }
	    }

	    private static void DiscoverOnDiscoveredNewSource(object sender, AudioBooksInfoBroadcast audioBooksInfoBroadcast)
        {
            if (streamers.Count >= maxCount)
                return;
            for (int i = 0; i < batchCount; i++)
            {
                BookStreamer streamer = new BookStreamer(null);
                streamer.GetStreamingBook(
                    new AudioBookInfoRemote(audioBooksInfoBroadcast.Books.First(), audioBooksInfoBroadcast.IpAddress),
                    new Progress<ReceivmentProgress>());
                streamers.Add(streamer);
            }
        }
    }
}
