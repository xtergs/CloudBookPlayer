using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using AudioBooksPlayer.WPF.Streaming;
using GalaSoft.MvvmLight.CommandWpf;
using RemoteAudioBooksPlayer.WPF.Annotations;
using RemoteAudioBooksPlayer.WPF.Logic;

namespace RemoteAudioBooksPlayer.WPF.ViewModel
{
    public class BaseViewModel:INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        [NotifyPropertyChangedInvocator]
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
    public class MainViewModel : BaseViewModel
    {
        private StreamPlayer player;
        private StreamingUDP streamerUdp;
        private BookStreamer bookStreamer;
        private string _source;
        private volatile bool _isListen;
        private double _packageLoss;
        private AudioBookInfoRemote _selectedBroadcastAudioBook;

        public ObservableCollection<AudioBooksInfoRemote> RemoteBooks { get; set; } =
            new ObservableCollection<AudioBooksInfoRemote>();

        public AudioBookInfoRemote SelectedBroadcastAudioBook
        {
            get { return _selectedBroadcastAudioBook; }
            set
            {
                if (Equals(value, _selectedBroadcastAudioBook)) return;
                _selectedBroadcastAudioBook = value;
                OnPropertyChanged();
            }
        }

        public bool IsListen
        {
            get { return _isListen; }
            set { _isListen = value; }
        }

        public MainViewModel(StreamPlayer player, bool startupDiscoveryListener = false)
        {
            if (player == null)
                throw new ArgumentNullException(nameof(player));
            this.player = player;
            streamerUdp = new StreamingUDP();
            bookStreamer = new BookStreamer();
            streamerUdp.DiscoveredNewSource += StreamerUdpOnDiscoveredNewSource;

            PlayCommand = new RelayCommand(PlayStream, CanPlayStream);
            ListenCommand = new RelayCommand(StartListen, CanStartToListen);
            TestStreamListen = new RelayCommand(TestStreamListenExecute);

            if (startupDiscoveryListener)
                ListenCommand.Execute(null);
        }

        private async void TestStreamListenExecute()
        {
            var stream = await bookStreamer.GetStreamingBook(SelectedBroadcastAudioBook,
               new Progress<ReceivmentProgress>(Handler));
            //streamerUdp.SendCommand(new IPEndPoint( RemoteBooks.First().IpAddress, 8000), new CommandFrame()
            //{
            //    Book = RemoteBooks.First().Books.First().Files.First().FilePath,
            //    Type =  CommandEnum.StreamFile,
            //});
            //MemorySecReadStream stream = new MemorySecReadStream(new byte[1024*4*10000]);
            //streamerUdp.StartListeneningSteam(stream, new IPEndPoint(IPAddress.Parse("192.168.0.100"), 7894), new Progress<ReceivmentProgress>(Handler));
            await Task.Delay(500);
            player.PlayStream(stream);
        }

        private void Handler(ReceivmentProgress receivmentProgress)
        {
            PackageLoss = receivmentProgress.PackageReceivmetns;
        }

        public double PackageLoss
        {
            get { return _packageLoss; }
            set
            {
                if (value.Equals(_packageLoss)) return;
                _packageLoss = value;
                OnPropertyChanged();
            }
        }

        private bool CanStartToListen()
        {
            return !IsListen;
        }

        private void StreamerUdpOnDiscoveredNewSource(object sender, AudioBooksInfoBroadcast audioBooksInfoBroadcast)
        {
            var elem = RemoteBooks.FirstOrDefault(x => Equals(x.IpAddress, audioBooksInfoBroadcast.IpAddress));
            if (elem != null)
                RemoteBooks.Remove(elem);
            RemoteBooks.Add(new AudioBooksInfoRemote(audioBooksInfoBroadcast));
        }

        private void StartListen()
        {
            if (IsListen)
                return;
            IsListen = true;
            streamerUdp.StartListen();
        }

        private bool CanPlayStream()
        {
            return !string.IsNullOrWhiteSpace(Source);
        }

        private void PlayStream()
        {
            player.PlayStream(Source);
        }

        public string Source
        {
            get { return _source; }
            set
            {
                if (value == _source)
                    return;
                _source = value;
                OnPropertyChanged();
            }
        }

        public ICommand PlayCommand { get; private set; }
        public ICommand ListenCommand { get; private set; }
        public ICommand TestStreamListen { get; private set; }

    }
}
