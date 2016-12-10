using System;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using AudioBooksPlayer.WPF.Annotations;
using Newtonsoft.Json;
using RemoteAudioBooksPlayer.WPF.ViewModel;

namespace AudioBooksPlayer.WPF.Streaming
{
    enum BookStramerState
    {
        
    }

    public class BookStreamer
    {
        StreamingUDP streamer;
	    public int defaultTcpPort { get; set; } = 8000;
        private IStreamer client;
        private Progress<StreamProgress> streamProgresss;
        private MemorySecReadStream memoryStream = new MemorySecReadStream(new byte[1024*1024]);
	    private Timer balanceLoadTimer;

        private Guid streamCommandId;
        private AudioBookInfoRemote streamingBook;
        private int streamingFileOrder = 0;
        private IProgress<ReceivmentProgress> receivmentReporter;

        public BookStreamer(StreamingUDP cl)
        {
            if (cl == null)
                streamer = new StreamingUDP();
            else
                streamer = cl;
        }

        public MemorySecReadStream Stream => memoryStream;

	    public Task<Stream> GetStreamingBook(AudioBookInfoRemote book, IProgress<ReceivmentProgress> reporter,
		    bool controlLoad = true)
	    {
		    this.streamingBook = book;
		    this.receivmentReporter = reporter;
		    streamer.FileStreamingComplited += StreamerOnFileStreamingComplited;
		    BalanceStreamLoad(controlLoad);

	        CommandData data = GetCommandDataForBook(book);

            return GetStreamingBookPipe(data, book.IpAddress,
			    book.TcpPort == 0 ? defaultTcpPort : book.TcpPort
			    , receivmentReporter);
	    }

	    public class CommandData
	    {
		    public int Version = 1;
		    public string BookName;
		    public int Order = 0;
		    public int OffsetFile = 0;
		    public TimeSpan TimeOffset = default(TimeSpan);
		    public int BytesPerTransfere = 1014*4*4;
		    public int TransfereRateMs = 50;

		    public string ToJs()
		    {
			    return JsonConvert.SerializeObject(this);
		    }

		    public static CommandData FromJs(string command)
		    {
			    return JsonConvert.DeserializeObject<CommandData>(command);
		    }
	    }

		private async Task<Stream> GetStreamingBookPipe(CommandData commandd, IPAddress endpoint, int TcpPort, IProgress<ReceivmentProgress> repoerter, bool contin  = false)
		{
			IsPausedStream = false;
			//UdpClient udpClient = new UdpClient(new IPEndPoint(IPAddress.Any, 0));
			CommandFrame command = new CommandFrame()
			{
				Book = "",
				Type = CommandEnum.StreamFilePipe,
				IdCommand = Guid.NewGuid(),
				FromIp = endpoint.GetAddressBytes(),
				Command = commandd.ToJs()
			};
			var endpointt = new IPEndPoint(endpoint, TcpPort == 0 ? defaultTcpPort : TcpPort);
			command = streamer.SendCommand(endpointt, command);
			if (command.Type != CommandEnum.Ok)
			{
				Debug.WriteLine("error while trying to stream book");

				return null;
			}
			var listenEndpoint = new IPEndPoint(new IPAddress(command.FromIp), command.ToIpPort);
			streamCommandId = command.IdCommand;
			ClientFactory fa = new ClientFactory();
            var client = fa.GetPipeClient();
		    if (contin)
		    {
		        memoryStream.Flush();
		        memoryStream = new MemorySecReadStream(new byte[1024*1024]);
		    }
		    streamer.StartListeneningSteam(client, command.Command, memoryStream, listenEndpoint, repoerter);
			return memoryStream;
		}

		private async Task<Stream> GetStreamingBookPipe(string filePath, IPAddress endpoint, int TcpPort, IProgress<ReceivmentProgress> repoerter)
	    {
			IsPausedStream = false;
			//UdpClient udpClient = new UdpClient(new IPEndPoint(IPAddress.Any, 0));
			CommandFrame command = new CommandFrame()
			{
				Book = filePath,
				Type = CommandEnum.StreamFilePipe,
				IdCommand = Guid.NewGuid(),
				FromIp = endpoint.GetAddressBytes(),
			};
			var endpointt = new IPEndPoint(endpoint, TcpPort == 0 ? defaultTcpPort : TcpPort);
			command = streamer.SendCommand(endpointt, command);
			if (command.Type != CommandEnum.Ok)
			{
				Debug.WriteLine("error while trying to stream book");

				return null;
			}
			var listenEndpoint = new IPEndPoint(new IPAddress(command.FromIp), command.ToIpPort);
			streamCommandId = command.IdCommand;
			ClientFactory fa = new ClientFactory();
		    var client = fa.GetPipeClient();
			streamer.StartListeneningSteam(client, command.Command, memoryStream, listenEndpoint, repoerter);
			//client = udpClient;
			//streamer.FileStreamingComplited += StreamerOnFileStreamingComplited;
			return memoryStream;
		}

//        private async Task<Stream> GetStreamingBook(string filePath, IPAddress endpoint, int tcpPort, IProgress<ReceivmentProgress> reporter )
//        {
//            IsPausedStream = false;
//            ClientFactory factory = new ClientFactory();
//	        var client = factory.GetUdpClient();
//            CommandFrame command = new CommandFrame()
//            {
//                Book = filePath,
//                Type = CommandEnum.StreamFileUdp,
//				Command = client.GetConnectionInfo(),
//                IdCommand = Guid.NewGuid(),
//                FromIp = endpoint.GetAddressBytes(),
//               // ToIpPort = ((IPEndPoint)client.GetConnectionInfo().Client.LocalEndPoint).Port
//            };
//            var endpointt = new IPEndPoint(endpoint, tcpPort);
//            command = streamer.SendCommand(endpointt, command);
//            if (command.Type != CommandEnum.Ok)
//            {
//                Debug.WriteLine("error while trying to stream book");
//                
//                return null;
//            }
//            var listenEndpoint = new IPEndPoint(new IPAddress(command.FromIp), command.ToIpPort);
//            streamCommandId = command.IdCommand;
//            streamer.StartListeneningSteam(client, "", memoryStream, listenEndpoint, reporter);
//            //streamer.FileStreamingComplited += StreamerOnFileStreamingComplited;
//            return memoryStream;
//        }

        private CommandData GetCommandDataForBook(AudioBookInfoRemote book)
        {
            CommandData data = new CommandData();
            data.BookName = book.Book.BookName;
            data.Order = book.Book.CurrentFile;
            data.TransfereRateMs = 1000;
            data.BytesPerTransfere =
                (int) (book.Book.CurrentFileInfo.Size/book.Book.CurrentFileInfo.Duration.TotalSeconds)*2;
            return data;
        }

        private void StreamerOnFileStreamingComplited(object sender, EventArgs eventArgs)
        {
            streamingFileOrder++;
            if (streamingBook.Book.Files.Length == streamingFileOrder)
            {
                streamer.FileStreamingComplited -= StreamerOnFileStreamingComplited;
                return;
            }
            GetStreamingBookPipe(GetCommandDataForBook(streamingBook), streamingBook.IpAddress, streamingBook.TcpPort,
                receivmentReporter, contin: true);
        }

	    public delegate CommandData GetFileFromBook(CommandData data);
		GetFileFromBook fillFileFromBook;
        public int StartStreamingServer(Progress<StreamProgress> progress, GetFileFromBook getFileFromBook )
        {
	        this.fillFileFromBook = getFileFromBook;
            streamProgresss = progress;
            streamer.GetCommand += StreamerOnGetCommand;
            streamer.ListenForCommands();
	        return streamer.TcpPort;
        }

        private void StreamerOnGetCommand(object sender, CommandFrame commandFrame)
        {
            switch (commandFrame.Type)
            {
                case CommandEnum.Ok:
                case CommandEnum.StreamFilePipe:
                    case CommandEnum.StreamFileUdp:
                    StreamFile(commandFrame);
                    break;
            }
        }

        private Task StreamFile(CommandFrame commandFrame)
        {
	        var commandData = CommandData.FromJs(commandFrame.Command);
	        var ret = fillFileFromBook(commandData);
	        var stream = File.OpenRead(ret.BookName);
	        var endpoint = new IPEndPoint(new IPAddress(commandFrame.ToIp), commandFrame.ToIpPort);
	        if (commandData.OffsetFile > 0)
		        stream.Position = commandData.OffsetFile;
            return streamer.StartSendStream(commandFrame.IdCommand, stream, commandData,
				endpoint,streamProgresss).ContinueWith(x =>
                {
	                stream.Close();
					stream.Dispose();
				});
        }

		public Task StreamFileBroadcast(string path)
		{
			var stream = File.OpenRead(path);
			var endpoint = new IPEndPoint(IPAddress.Any, 0);
			streamCommandId = Guid.NewGuid();
			return streamer.StartBroadcastStream(streamCommandId, stream,"", endpoint,
				streamProgresss).ContinueWith(x =>
				{
					stream.Close();
				});
		}

	    public async Task DownloadBook(AudioBookInfoRemote book, string downloadTo)
	    {
		    BalanceStreamLoad(true);
			streamingBook = book;
		    var path = Path.Combine(downloadTo, Path.GetFileName(book.Book.BookName));


		    Directory.CreateDirectory(path);
		    for (int i = 0; i < book.Book.Files.Length; i++)
		    {
			    string fileName = Path.GetFileName(book.Book.Files[i].FilePath);
			    await DownloadFile(book.Book.Files[i].FilePath, book.IpAddress, book.TcpPort, Path.Combine(path, fileName), book.Book.Files[i].Size);
		    }

	    }

	    public async Task DownloadFile(string book, IPAddress endpoint, int tcpPort, string downloadTo, long length)
	    {
			FileInfo info = new FileInfo(downloadTo);
		    if (info.Exists && info.Length == length)
			    return;
		    using (var file = File.Create(downloadTo))
		    {
			    int totalReaded = 0;
			    int readed = 0;
				byte[] buffer = new byte[1024*4*4];
			    var stream = await GetStreamingBookPipe(book, endpoint, tcpPort , new Progress<ReceivmentProgress>());
			    await Task.Delay(500);
			    while (totalReaded < length)
			    {
				    readed = stream.Read(buffer, 0, buffer.Length);
				    file.Write(buffer, 0, readed);
				    totalReaded += readed;
			    }
			    file.Flush();
				file.Close();
		    }

	    }

	    private TimeSpan span = new TimeSpan(0, 0, 0, 1);
	    private void BalanceStreamLoad(bool isStart = true)
	    {
		    if (isStart)
		    {
			    if (balanceLoadTimer != null)
				    return;
			    balanceLoadTimer = new Timer(BalanceStreamLoad, null, span, span);
		    }
		    else
		    {
			    if (balanceLoadTimer == null)
				    return;
			    balanceLoadTimer.Dispose();
			    balanceLoadTimer = null;
		    }
	    }

	    private void BalanceStreamLoad(object obj)
	    {
			if (Stream.LeftToWrite / ((double)Stream.Capacity) < 0.5 && !IsPausedStream)
			{
				PauseStream();
			}
			else if (IsPausedStream && Stream.LeftToRead / ((double)Stream.Capacity) < 0.3)
			{
				ResumeStream();
			}
		}

		static object o = new object();
        public bool IsPausedStream { get; private set; }

	    public AudioBookInfoRemote StreamingBook
	    {
		    get { return streamingBook; }
	    }

	    public bool PauseStream()
        {
            lock (o)
            {
                if (IsPausedStream)
                    return true;
                var endpoint = streamingBook.IpAddress;
                CommandFrame command = new CommandFrame()
                {
                    Book = streamCommandId.ToString(),
                    Type = CommandEnum.PauseStreaming,
                    IdCommand = Guid.NewGuid(),
                };
                var endpointt = new IPEndPoint(endpoint, streamingBook.TcpPort);
                command = streamer.SendCommand(endpointt, command);
                if (command.Type != CommandEnum.Ok)
                {
                    Debug.WriteLine("error while trying to pause book");
                    IsPausedStream = false;
                    return false;
                }
                IsPausedStream = true;
                return true;
            }
        }

        public bool ResumeStream()
        {
            lock (o)
            {
                if (!IsPausedStream)
                    return true;

                var endpoint = streamingBook.IpAddress;
                CommandFrame command = new CommandFrame()
                {
                    Book = streamCommandId.ToString(),
                    Type = CommandEnum.ResumeStreaming,
                    IdCommand = Guid.NewGuid(),
                };
                var endpointt = new IPEndPoint(endpoint, streamingBook.TcpPort);
                command = streamer.SendCommand(endpointt, command);
                if (command.Type != CommandEnum.Ok)
                {
                    Debug.WriteLine("error while trying to resume book");
                    IsPausedStream = true;
                    return false;
                }
                IsPausedStream = false;
                return true;
            }
        }

        public bool StopStream()
        {
            lock (o)
            {
                if (!IsPausedStream)
                    return true;

                var endpoint = streamingBook.IpAddress;
                CommandFrame command = new CommandFrame()
                {
                    Book = streamCommandId.ToString(),
                    Type = CommandEnum.CancelStream,
                    IdCommand = Guid.NewGuid(),
                };
                var endpointt = new IPEndPoint(endpoint, streamingBook.TcpPort);
                command = streamer.SendCommand(endpointt, command);
                if (command.Type != CommandEnum.Ok)
                {
                    Debug.WriteLine("error while trying to cancel book streaming");
                    return false;
                }
                client = null;
                Stream.Clear();
                return true;
            }
        }
    }
}
