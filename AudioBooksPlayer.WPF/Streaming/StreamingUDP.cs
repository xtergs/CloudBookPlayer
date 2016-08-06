using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
        StreamFileUdp,
        Ok,
        PauseStreaming,
        ResumeStreaming,
        CancelStream,
		StreamFilePipe,
		StreamFileTcp
    }

    public enum StreamingStatus
    {
        Stream,
        Pause,
        Cancel
    }

    public struct StreamStatus
    {
        public StreamingStatus Status { get; set; }
        public Guid operationId { get; set; }
    }

    public class StreamingUDP
    {

        public int Port { get; set; }
	    
        private Dictionary<Guid, int> receivmentTable = new Dictionary<Guid, int>();
        public Dictionary<Guid, IStreamer> awaitingUdpClients = new Dictionary<Guid, IStreamer>();
		public Dictionary<Guid, IStreamer> broadcastingClients = new Dictionary<Guid, IStreamer>();
		private ClientFactory factory = new ClientFactory();
        static object o = new object();

        public async Task StartListeneningSteam(IStreamer client, string connectionInfo, Stream stream, IPEndPoint endPoint,
            IProgress<ReceivmentProgress> reporter)
        {
            long totalReceeive = 0;
                long totalMissed = 0;
                long posInStream = stream.Position;
                ReceivmentProgress recProg = new ReceivmentProgress();
                bool isContinue = true;
                try
                {
                    await Task.Run( async () =>
                    {
                        
#pragma warning disable 1998
							await client.StartReceiveStream(connectionInfo, endPoint, reporter, async (frame, rp) => 
#pragma warning restore 1998
                            {
	                            if (totalMissed > 0)
                                {
                                    recProg.PackageReceivmetns = totalMissed/(double) totalReceeive;
                                    reporter.Report(recProg);
                                }
                                totalReceeive++;
	                            
								if (frame.Length == 0)
                                {
                                    isContinue = false;
	                                receivmentTable.Remove(frame.id);
                                    return;
                                }
                                if (receivmentTable.ContainsKey(frame.id))
                                    if (receivmentTable[frame.id] > frame.Order)
                                        return;
                                    else
                                    {
                                        totalMissed += frame.Order - receivmentTable[frame.id];
                                        receivmentTable[frame.id] = (int)++frame.Order;
                                        lock (o)
                                        {
                                            stream.Write(frame.buffer, frame.Offsset, frame.Length);
                                        }
                                        return;
                                    }
                                receivmentTable.Add(frame.id, (int)++frame.Order);
                                lock (o)
									stream.Write(frame.buffer, frame.Offsset, frame.Length);
							});
                    });
                    OnFileStreamingComplited();
                }
                catch (TimeoutException e)
                {
                    if (!isContinue)
                    {
                        OnFileStreamingComplited();
                        return;
                    }
                    throw;


                }
        }

        public Task StartListeneningSteam(Stream stream,IPEndPoint endPoint, IProgress<ReceivmentProgress> reporter)
        {
            using (var cl = factory.GetUdpClient())
            {
                return StartListeneningSteam(cl, "", stream, endPoint, reporter);
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
            OnConnectionChanged(true);
            try
            {
                var client = (TcpClient) state;
                byte[] buffer = new byte[4096];
                if (!client.Connected)
                    client.Connect((IPEndPoint) client.Client.RemoteEndPoint);
                CommandFrame commandFrame;
                using (var stream = client.GetStream())
                {
                    int readed = stream.Read(buffer, 0, buffer.Length);
                    commandFrame = CommandFromBytes(buffer, readed);
                    commandFrame.ToIp = ((IPEndPoint) client.Client.RemoteEndPoint).Address.GetAddressBytes();
	                string data;
	                switch (commandFrame.Type)
                    {
                        case CommandEnum.StreamFileUdp:
		                    data = commandFrame.Book;
                            commandFrame = EstablishUdpChanel(stream, commandFrame);
                            commandFrame.Type = CommandEnum.StreamFileUdp;
		                    commandFrame.Book = data;
                            break;
						case CommandEnum.StreamFilePipe:
							data = commandFrame.Book;
							commandFrame = EstablishUdpChanel(stream, commandFrame);
		                    commandFrame.Type = CommandEnum.StreamFilePipe;
							break;
                        case CommandEnum.PauseStreaming:
                        {
	                        try
	                        {
		                        var cl = awaitingUdpClients[Guid.Parse(commandFrame.Book)];
		                        cl.Pause();
		                        commandFrame.Type = CommandEnum.Ok;
		                        var bytes = CommandToBytes(commandFrame);
		                        stream.Write(bytes, 0, bytes.Length);
	                        }
								catch(IndexOutOfRangeException)
								{ }
	                        break;
                        }
                        case CommandEnum.ResumeStreaming:
                        {
                            var cl = awaitingUdpClients[Guid.Parse(commandFrame.Book)];
                            cl.Resume();
                            commandFrame.Type = CommandEnum.Ok;
                            var bytes = CommandToBytes(commandFrame);
                            stream.Write(bytes, 0, bytes.Length);
                            break;
                        }
                        case CommandEnum.CancelStream:
	                    {
		                    var id = Guid.Parse(commandFrame.Book);
		                    if (!awaitingUdpClients.ContainsKey(id))
			                    return;
							var cl = awaitingUdpClients[id];
                            cl.Cancel();
                            commandFrame.Type = CommandEnum.Ok;
                            var bytes = CommandToBytes(commandFrame);
                            stream.Write(bytes, 0, bytes.Length);
                            break;
                        }
                    }
                }

                OnGetCommand(commandFrame);
            }
            finally
            {
                OnConnectionChanged(false);
            }
        }

        public CommandFrame SendCommand(IPEndPoint endpoint, CommandFrame command)
        {
            TcpClient client = new TcpClient();
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
	        IStreamer client = null;
			if (frame.Type == CommandEnum.StreamFileUdp)
				client = factory.GetUdpClient();
	        if (frame.Type == CommandEnum.StreamFilePipe)
		        client = factory.GetPipeClient();
	        try
	        {
		        var ip = client.GetConnectionInfo();
		        int offset = 0;
		        frame.Command = ip;
		        frame.Type = CommandEnum.Ok;
		        var com = CommandToBytes(frame);
		        int readed = 0;
		        while (true)
		        {

			        stream.Write(com, 0, com.Length);
			        while (!stream.DataAvailable) ;
			        readed = 1;
			        while (stream.DataAvailable && readed > 0)
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
	        }
	        catch (Exception)
	        {
		        //client?.Dispose();
		        throw;
	        }
			client.StreamStatusChanged -= ClientOnStreamStatusChanged; //If client was been cached
			client.StreamStatusChanged += ClientOnStreamStatusChanged;
	        awaitingUdpClients.Add(frame.IdCommand, client);
            return frame;
        }

	    private void ClientOnStreamStatusChanged(object sender, StreamStatus streamStatus)
	    {
		    OnStreamStatusChanged(streamStatus);
	    }

	    private static byte[] CommandToBytes(CommandFrame command)
        {
            var sCom = JsonConvert.SerializeObject(command);
            var bytes = Encoding.ASCII.GetBytes(sCom);
            return bytes;
        }

        private static CommandFrame CommandFromBytes(byte[] buffer, int readed)
        {
            var sBuffer = Encoding.ASCII.GetString(buffer, 0, readed);
            var commandFrame = JsonConvert.DeserializeObject<CommandFrame>(sBuffer);
            return commandFrame;
        }

        public event EventHandler<bool> ConnectionChanged;

          
        public event EventHandler<CommandFrame> GetCommand;
        public event EventHandler FileStreamingComplited;
		public event EventHandler<StreamStatus> StreamStatusChanged;

		protected virtual void OnGetCommand(CommandFrame e)
        {
            GetCommand?.Invoke(this, e);
        }

        protected virtual void OnFileStreamingComplited()
        {
            FileStreamingComplited?.Invoke(this, EventArgs.Empty);
        }

        protected virtual void OnConnectionChanged(bool isNew)
        {
            ConnectionChanged?.Invoke(this, isNew);
        }

	    public Task StartBroadcastStream(Guid idCommand, FileStream stream, string command, IPEndPoint endpoint,
		    Progress<StreamProgress> streamProgresss)
	    {
			var cl = factory.GetUdpClient();
			broadcastingClients.Add(idCommand, cl);
			return cl.StartSendStream(stream, command, endpoint, streamProgresss).ContinueWith(x =>
			{
				broadcastingClients.Remove(idCommand);
				factory.Return(cl);
			});
		}

	    public Task StartSendStream(Guid idCommand, FileStream stream, string endpoint, IPEndPoint endpo, Progress<StreamProgress> streamProgresss)
	    {
			    return
				    awaitingUdpClients[idCommand].StartSendStream(stream, endpoint, endpo, streamProgresss)
					    .ContinueWith(x => awaitingUdpClients.Remove(idCommand));
	    }

	    protected virtual void OnStreamStatusChanged(StreamStatus e)
	    {
		    StreamStatusChanged?.Invoke(this, e);
	    }
    }
}
