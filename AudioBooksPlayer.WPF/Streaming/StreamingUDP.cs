using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AudioBooksPlayer.WPF.Model;
using Newtonsoft.Json;

namespace AudioBooksPlayer.WPF.Streaming
{
    public enum CommandEnum
    {
        StreamFile,
        Ok
    }

    public class StreamingUDP
    {
        private int port = 18121;
        private Timer broadcastTimer;
        private List<AudioBooksInfo> AuduioBooks;
        private UdpClient broadcastClient;
        private volatile bool isBroadcasting = false;

        private UdpClient listenClient;
        private volatile bool isListen = false;

        public void StartDiscoverty(List<AudioBooksInfo> books )
        {
            this.AuduioBooks = books;
            if (isBroadcasting)
                return;
            isBroadcasting = true;
            broadcastClient = new UdpClient();
            broadcastClient.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            //broadcastClient.Client.Bind(new IPEndPoint(IPAddress.Parse("255.255.255.255"), port));
            broadcastClient.EnableBroadcast = true;
            broadcastTimer = new Timer(BroadcastTimerCallback, null, new TimeSpan(0,0,0,10), new TimeSpan(0,0,0,20,0) );
        }

        public void StopDiscovery()
        {
            broadcastClient.Dispose();
            broadcastClient = null;
            broadcastTimer.Dispose();
            broadcastTimer = null;
            AuduioBooks = null;
            isBroadcasting = false;
        }

        private async void BroadcastTimerCallback(object state)
        {
            var jsdata = JsonConvert.SerializeObject((AuduioBooks.ToArray()));
            var sendData = Encoding.ASCII.GetBytes(jsdata);
            //broadcastClient.Connect(new IPEndPoint(IPAddress.Broadcast, port));
            var res = await broadcastClient.SendAsync(sendData, sendData.Length, new IPEndPoint(IPAddress.Parse("255.255.255.255"), port)).ConfigureAwait(false);
        }

        public event EventHandler<AudioBooksInfoBroadcast> DiscoveredNewSource; 

        public async void StartListen()
        {
            if (isListen)
                return;
            isListen = true;
            var ipAddress = new IPEndPoint(IPAddress.Any, port+1);
            var addres = new IPEndPoint(IPAddress.Any, port);
            listenClient = new UdpClient();
            listenClient.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            listenClient.Client.Bind(addres);
            //listenClient.EnableBroadcast = true;
            //listenClient.Connect(addres);
            while (isListen)
            {
                var data = await listenClient.ReceiveAsync();
                var jsdata = Encoding.ASCII.GetString(data.Buffer);
                var discoveredBooks = JsonConvert.DeserializeObject<AudioBooksInfo[]>(jsdata);
                OnDiscoveredNewSource(new AudioBooksInfoBroadcast()
                {
                    Books = discoveredBooks,
                    IpAddress = data.RemoteEndPoint.Address,
                    LastDiscoveryUtcTime = DateTime.UtcNow
                });
            }
        }

        protected virtual void OnDiscoveredNewSource(AudioBooksInfoBroadcast e)
        {
            DiscoveredNewSource?.Invoke(this, e);
        }

        public async Task StartSendStream(UdpClient client, Stream stream, IPEndPoint endpoint, IProgress<StreamProgress> reporter,
            int transferePerMs = 50)
        {
            using (client)
            {
                byte[] buffer = new byte[1024 * 4 * 4];
                UdpFrame frame = new UdpFrame()
                {
                    Id = Guid.NewGuid(),
                    Order = 0
                };
                int order = 0;
                var prs = new StreamProgress();
                

                    client.EnableBroadcast = true;
                    string bytes;
                    byte[] b;
                    prs.Length = stream.Length;
                    while (true)
                    {
                        while (stream.CanRead && stream.Position < stream.Length)
                        {
                            var readed = stream.Read(buffer, 0, buffer.Length);
                            frame.Data = buffer;
                            frame.Length = readed;
                            bytes = JsonConvert.SerializeObject(frame);
                            b = Encoding.ASCII.GetBytes(bytes);
                            await client.SendAsync(b, b.Length, endpoint);
                            await Task.Delay(transferePerMs);
                            prs.Position = stream.Position;
                            reporter.Report(prs);
                            frame.Order++;
                        }
                        //stream.Position = 0;
                        break;
                    }
                    frame.Data = null;
                    bytes = JsonConvert.SerializeObject(frame);
                    b = Encoding.ASCII.GetBytes(bytes);
                    await client.SendAsync(b, b.Length, endpoint);
                }
            
        }
        public Task StartSendStream(Stream stream, IPEndPoint endpoint, IProgress<StreamProgress> reporter, 
            int transferePerMs = 50)
        {
            using (UdpClient client = new UdpClient())
            {
                client.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
                return StartSendStream(client, stream, endpoint, reporter);
            }
        }

        private Dictionary<Guid, int> receivmentTable = new Dictionary<Guid, int>();
        private Dictionary<Guid,List<UdpFrame>> bufFrames = new Dictionary<Guid, List<UdpFrame>>();
        public Dictionary<Guid, UdpClient> awaitingUdpClients = new Dictionary<Guid, UdpClient>(); 
        static object o = new object();

        public async Task StartListeneningSteam(UdpClient client, Stream stream, IPEndPoint endPoint,
            IProgress<ReceivmentProgress> reporter)
        {
            using (client)
            {
                long totalReceeive = 0;
                long totalMissed = 0;
                long posInStream = stream.Position;
                ReceivmentProgress recProg = new ReceivmentProgress();
                try
                {
                    client.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
                    //client.Client.Bind(endPoint);
                    client.Client.ReceiveTimeout = 5000;
                    client.Client.ReceiveBufferSize = int.MaxValue;
                }
                catch (Exception e)
                {
                    throw;
                }
                bool isContinue = true;
                await Task.Run(async () =>
                {
                    while (isContinue)
                    {
                        await client.ReceiveAsync().ContinueWith((state) =>
                        {
                            if (totalMissed > 0)
                            {
                                recProg.PackageReceivmetns = totalMissed/(double) totalReceeive;
                                reporter.Report(recProg);
                            }
                            totalReceeive++;
                            var frame =
                                JsonConvert.DeserializeObject<UdpFrame>(Encoding.ASCII.GetString(state.Result.Buffer));
                            if (frame.Data == null)
                            {
                                isContinue = false;
                                return;
                            }
                            if (receivmentTable.ContainsKey(frame.Id))
                                if (receivmentTable[frame.Id] > frame.Order)
                                    return;
                                else
                                {
                                    totalMissed += frame.Order - receivmentTable[frame.Id];
                                    receivmentTable[frame.Id] = ++frame.Order;
                                    lock (o)
                                    {
                                        var pos = stream.Position;
                                        stream.Position = posInStream;
                                        stream.Write(frame.Data, 0, frame.Length);
                                        posInStream = stream.Position;
                                        stream.Position = pos;
                                    }
                                    return;
                                }
                            receivmentTable.Add(frame.Id, ++frame.Order);
                            lock (o)
                                stream.Write(frame.Data, 0, frame.Length);
                        });
                    }
                });
            }
        }

        public Task StartListeneningSteam(Stream stream,IPEndPoint endPoint, IProgress<ReceivmentProgress> reporter)
        {
            using (UdpClient client = new UdpClient())
            {
                return StartListeneningSteam(client, stream, endPoint, reporter);
            }
        }

        public void ListenForCommands()
        {
            IPEndPoint end = new IPEndPoint(IPAddress.Any, 0);
            TcpListener listener = new TcpListener(IPAddress.Any, 8000);
            TcpClient client;
            listener.Start();

            while (true)
            {
                client = listener.AcceptTcpClient();
                ThreadPool.QueueUserWorkItem(TcpCommandListenerCallBack, client);
            }
        }

        private void TcpCommandListenerCallBack(object state)
        {
            var client = (TcpClient) state;
            byte[] buffer = new byte[4096];
            if (!client.Connected)
                client.Connect((IPEndPoint)client.Client.RemoteEndPoint);
            CommandFrame commandFrame;
            using (var stream = client.GetStream())
            {
                int readed = stream.Read(buffer, 0, buffer.Length);
                commandFrame = CommandFromBytes(buffer, readed);
                commandFrame.ToIp = ((IPEndPoint) client.Client.RemoteEndPoint).Address.GetAddressBytes();
                switch (commandFrame.Type)
                {
                    case CommandEnum.StreamFile:
                        commandFrame  = EstablishUdpChanel(stream, commandFrame);
                        commandFrame.Type = CommandEnum.StreamFile;
                        break;
                }
            }
            OnGetCommand(commandFrame);
        }

        public CommandFrame SendCommand(IPEndPoint endpoint, CommandFrame command)
        {
            TcpClient client = new TcpClient();
            //client.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            //client.Client.Bind(endpoint);

            int readed = 0;
            int offset = 0;
            byte[] buffer = new byte[4096];
            command.ToIp = endpoint.Address.GetAddressBytes();
            //command.ToIpPort = endpoint.Port;
            var bytes = CommandToBytes(command);
            client.Connect(endpoint);
            using (var stream = client.GetStream())
            {
                stream.Write(bytes, 0, bytes.Length);
                while (!stream.DataAvailable) ;
                readed = 1;
                while (stream.DataAvailable && readed > 0)
                {
                    readed = stream.Read(buffer, offset, buffer.Length-offset);
                    offset += readed;
                }
                readed += offset;
                command = CommandFromBytes(buffer, readed);
                if (command.Type == CommandEnum.Ok)
                    stream.Write(buffer, 0, readed);
                stream.Close(100);
            }
            return command;

        }

        private CommandFrame EstablishUdpChanel(NetworkStream stream, CommandFrame frame)
        {
            UdpClient client = new UdpClient( new IPEndPoint(IPAddress.Any, 0));
            var ip = (IPEndPoint) client.Client.LocalEndPoint;
            int offset = 0;
            frame.FromIpPort = ip.Port;
            frame.Type = CommandEnum.Ok;
            var com = CommandToBytes(frame);
            int readed = 0;
            while (true)
            {

                stream.Write(com, 0, com.Length);
                while (!stream.DataAvailable) ;
                readed = 1;
                while (stream.DataAvailable && readed >0)
                {
                    readed = stream.Read(com, offset, com.Length - offset);
                    offset += readed;
                }
                readed += offset;
                var ccom = CommandFromBytes(com, readed);
                if (ccom.Type == CommandEnum.Ok)
                {
                    stream.Close(100);
                    break;
                }
            }
            awaitingUdpClients.Add(frame.IdCommand, client);
            return frame;
        }

        private byte[] CommandToBytes(CommandFrame command)
        {
            var sCom = JsonConvert.SerializeObject(command);
            var bytes = Encoding.ASCII.GetBytes(sCom);
            return bytes;
        }

        private CommandFrame CommandFromBytes(byte[] buffer, int readed)
        {
            var sBuffer = Encoding.ASCII.GetString(buffer, 0, readed);
            var commandFrame = JsonConvert.DeserializeObject<CommandFrame>(sBuffer);
            return commandFrame;
        }

        public event EventHandler<CommandFrame> GetCommand;

        protected virtual void OnGetCommand(CommandFrame e)
        {
            GetCommand?.Invoke(this, e);
        }
    }
}
