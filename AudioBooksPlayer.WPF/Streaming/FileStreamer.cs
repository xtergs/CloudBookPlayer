using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using RemoteAudioBooksPlayer.WPF.ViewModel;

namespace AudioBooksPlayer.WPF.Streaming
{
    enum BookStramerState
    {
        
    }

    public class BookStreamer
    {
        StreamingUDP streamer = new StreamingUDP();
        private Progress<StreamProgress> streamProgresss;
        private MemorySecReadStream memoryStream = new MemorySecReadStream(new byte[1024*1024*10]);

        private AudioBookInfoRemote streamingBook;
        private int streamingFileOrder = 0;
        private IProgress<ReceivmentProgress> receivmentReporter;

        public Task<Stream> GetStreamingBook(AudioBookInfoRemote book, IProgress<ReceivmentProgress> reporter)
        {
            this.streamingBook = book;
            this.receivmentReporter = reporter;
            streamer.FileStreamingComplited += StreamerOnFileStreamingComplited;
            return GetStreamingBook(book.Book.Files.First().FilePath, book.IpAddress, receivmentReporter);
        }

        public async Task<Stream> GetStreamingBook(string filePath, IPAddress endpoint, IProgress<ReceivmentProgress> reporter )
        {
            UdpClient client = new UdpClient(new IPEndPoint(IPAddress.Any, 0));
            CommandFrame command = new CommandFrame()
            {
                Book = filePath,
                Type = CommandEnum.StreamFile,
                IdCommand = Guid.NewGuid(),
                FromIp = endpoint.GetAddressBytes(),
                ToIpPort = ((IPEndPoint)client.Client.LocalEndPoint).Port
            };
            var endpointt = new IPEndPoint(endpoint, 8000);
            command = streamer.SendCommand(endpointt, command);
            if (command.Type != CommandEnum.Ok)
            {
                Debug.WriteLine("error while trying to stream book");
                client.Close();
                client.Dispose();
                return null;
            }
            var listenEndpoint = new IPEndPoint(new IPAddress(command.FromIp), command.ToIpPort);
            streamer.StartListeneningSteam(client, memoryStream, listenEndpoint, reporter);
            //streamer.FileStreamingComplited += StreamerOnFileStreamingComplited;
            return memoryStream;
        }

        private void StreamerOnFileStreamingComplited(object sender, EventArgs eventArgs)
        {
            streamingFileOrder++;
            if (streamingBook.Book.Files.Length == streamingFileOrder)
            {
                streamer.FileStreamingComplited -= StreamerOnFileStreamingComplited;
                return;
            }
            GetStreamingBook(streamingBook.Book.Files[streamingFileOrder].FilePath, streamingBook.IpAddress,
                receivmentReporter);
        }

        public void StartStreamingServer(Progress<StreamProgress> progress)
        {
            streamProgresss = progress;
            streamer.GetCommand += StreamerOnGetCommand;
            streamer.ListenForCommands();
        }

        private void StreamerOnGetCommand(object sender, CommandFrame commandFrame)
        {
            switch (commandFrame.Type)
            {
                    case CommandEnum.StreamFile:
                    StreamFile(commandFrame);
                    break;
            }
        }

        private Task StreamFile(CommandFrame commandFrame)
        {
            var stream = File.OpenRead(commandFrame.Book);
            var endpoint = new IPEndPoint(new IPAddress(commandFrame.ToIp), commandFrame.ToIpPort);
            return streamer.StartSendStream(streamer.awaitingUdpClients[commandFrame.IdCommand], stream, endpoint,
                streamProgresss);
        }
    }
}
