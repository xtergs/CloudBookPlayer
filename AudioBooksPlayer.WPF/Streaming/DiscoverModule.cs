using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using AudioBooksPlayer.WPF.Model;
using Newtonsoft.Json;

namespace AudioBooksPlayer.WPF.Streaming
{
    public class DiscoverModule
    {
        private UdpClient listenClient;
        private UdpClient broadcastClient;
        private volatile bool isBroadcasting = false;
        private List<AudioBooksInfo> AuduioBooks;
        private Timer broadcastTimer;
        private volatile bool isListen = false;
        private int port = 18121;

        public bool IsDiscovered => isBroadcasting;
        public bool IsListening => isListen;

        public int TcpPort { get; set; }
        public string Name { get; set; }

        public int Port
        {
            get { return port; }
            set
            {
                if (value < 0)
                    return;
                if (IsListening)
                    throw new CantSetPortWhileListeningException(
                        "Cant set new port while listening for udp incoming data");
                port = value;
            }
        }

        public void StartDiscoverty(List<AudioBooksInfo> books)
        {
            this.AuduioBooks = books;
            if (isBroadcasting)
                return;
            isBroadcasting = true;
            broadcastClient = new UdpClient();
            broadcastClient.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            broadcastClient.EnableBroadcast = true;
	        broadcastClient.Client.SendBufferSize = int.MaxValue;
            broadcastTimer = new Timer(BroadcastTimerCallback, null, new TimeSpan(0, 0, 0, 10), new TimeSpan(0, 0, 0, 20, 0));
        }

        public void StopDiscovery()
        {
            broadcastClient?.Dispose();
            broadcastClient = null;
            broadcastTimer?.Dispose();
            broadcastTimer = null;
            AuduioBooks = null;
            isBroadcasting = false;
        }

        private async void BroadcastTimerCallback(object state)
        {
            if (AuduioBooks == null)
                return;
            AudioBooksBroadcastStructur frame = new AudioBooksBroadcastStructur()
            {
                Books = AuduioBooks.ToArray(),
                TcpCommandPort = TcpPort
            };
            var jsdata = JsonConvert.SerializeObject(frame);
            var sendData = Encoding.ASCII.GetBytes(jsdata);
            var res = await broadcastClient.SendAsync(sendData, sendData.Length, new IPEndPoint(IPAddress.Parse("255.255.255.255"), port)).ConfigureAwait(false);
        }

        public event EventHandler<AudioBooksInfoBroadcast> DiscoveredNewSource;

        public async void StartListen()
        {
            if (isListen)
                return;
            isListen = true;
            var addres = new IPEndPoint(IPAddress.Any, port);
            using (listenClient = new UdpClient())
            {
                listenClient.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
                listenClient.Client.Bind(addres);
	            listenClient.Client.ReceiveBufferSize = int.MaxValue;

				while (isListen)
                {
                    try
                    {
                        var data = await listenClient.ReceiveAsync().ConfigureAwait(false);
                        var jsdata = Encoding.ASCII.GetString(data.Buffer);
                        var discoveredBooks = JsonConvert.DeserializeObject<AudioBooksBroadcastStructur>(jsdata);
                        OnDiscoveredNewSource(new AudioBooksInfoBroadcast(discoveredBooks, data.RemoteEndPoint.Address));
                    }
                    catch (Exception e)
                    {
                        Debug.WriteLine(e.Message);
                    }
                }
            }
        }

        public void StopListen()
        {
            isListen = false;
            listenClient.Close();
        }

        protected virtual void OnDiscoveredNewSource(AudioBooksInfoBroadcast e)
        {
            DiscoveredNewSource?.Invoke(this, e);
        }
    }

    public class CantSetPortWhileListeningException : Exception
    {
        public CantSetPortWhileListeningException()
            : base()
        {
        }

        public CantSetPortWhileListeningException(string message)
            : base(message)
        {
        }
    }
}
