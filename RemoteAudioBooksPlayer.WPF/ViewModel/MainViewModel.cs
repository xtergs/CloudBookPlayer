using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using AudioBooksPlayer.WPF.Model;
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
        public StreamPlayer player;
        private StreamingUDP streamerUdp;
        private readonly DiscoverModule discoverModule;
        private BookStreamer bookStreamer;
        private string _source;
        private volatile bool _isListen;
        private double _packageLoss;
        private AudioBookInfoRemote _selectedBroadcastAudioBook;
        private Timer streamStatusTimer;

        public ObservableCollection<AudioBooksInfoRemote> RemoteBooks { get; set; } =
            new ObservableCollection<AudioBooksInfoRemote>();

	    public ObservableCollection<AudioBookInfoRemote> LocalBooks { get; set; } =
		    new ObservableCollection<AudioBookInfoRemote>(); 

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
            discoverModule = new DiscoverModule();
            bookStreamer = new BookStreamer(streamerUdp);
            discoverModule.DiscoveredNewSource += StreamerUdpOnDiscoveredNewSource;

            PlayCommand = new RelayCommand(PlayStream, CanPlayStream);
            ListenCommand = new RelayCommand(StartListen, CanStartToListen);
            PlayStreamCommand = new RelayCommand(TestStreamListenExecute, PlayStreamCanExecute);
            PausePlayCommand = new RelayCommand(PausePlayExecute, PausePlayCanExecute);
            CancelPlayingCommand = new RelayCommand(CancelPlayingExecute, CancelPlayingCanExecute);
			DownloadBookCommand = new RelayCommand(DownloadBookExecute);

			var timeSpan = new TimeSpan(0, 0, 0, 1, 0);
            streamStatusTimer = new Timer(UpdateStatusStream, null, timeSpan, timeSpan );

            if (startupDiscoveryListener)
                ListenCommand.Execute(null);
        }

	    private  void DownloadBookExecute()
	    {
		    if (SelectedBroadcastAudioBook != null)
		    {
			    new BookStreamer(streamerUdp).DownloadBook(SelectedBroadcastAudioBook, "D:\\TestBooks");
		    }
	    }

	    private bool CancelPlayingCanExecute()
        {
            return true;
        }

        private void CancelPlayingExecute()
        {
            player.Stop();
            bookStreamer.StopStream();
            CurrentlyPlayingBook = null;
            PlayingFileInfo = null;
            isPaused = false;
        }

        private bool PlayStreamCanExecute()
        {
            return SelectedBroadcastAudioBook != null;
        }

        private bool PausePlayCanExecute()
        {
            return isPaused && player.PlaybackState == StreamPlayer.StreamingPlaybackState.Playing;
        }

        private void PausePlayExecute()
        {
            bookStreamer.PauseStream();
            player.Pause();
            isPaused = true;
        }

        private volatile bool isPaused = false;
        private void UpdateStatusStream(object state)
        {
            OnPropertyChanged(nameof(LeftToRead));
            OnPropertyChanged(nameof(LeftToWrite));
            OnPropertyChanged(nameof(ReadPosition));
            OnPropertyChanged(nameof(WritePositon));
            OnPropertyChanged(nameof(CurrentPosition));
            OnPropertyChanged(nameof(TotalLengthPlayingFile));

	        if (player.PlaybackState != StreamPlayer.StreamingPlaybackState.Paused)
		        ;
        }

        public double LeftToRead => bookStreamer.Stream.Capacity ;
        public double LeftToWrite => bookStreamer.Stream.Capacity ;
        public double ReadPosition => bookStreamer.Stream.LeftToRead;
        public double WritePositon => bookStreamer.Stream.LeftToWrite;

        public TimeSpan TotalLengthPlayingFile
        {
            get
            {
                if (SelectedBroadcastAudioBook != null)
                    return SelectedBroadcastAudioBook.Book.Files.First().Duration;
                return TimeSpan.Zero;
            }
        }

        private TimeSpan BasicPosition { get; set; }

	    public TimeSpan CurrentPosition
	    {
		    get
		    {
			    if (SelectedBroadcastAudioBook?.Book?.Files == null)
				    return default(TimeSpan);

			    return (new TimeSpan(0, 0, 0,
				    (int)
					    (player.PlayedTime/
					     (SelectedBroadcastAudioBook.Book.CurrentFileInfo.Size/
					      SelectedBroadcastAudioBook.Book.CurrentFileInfo.Duration.TotalSeconds)),
				    0) - player.BufferedTime);
		    }
	    }

        public AudioBookInfoRemote CurrentlyPlayingBook { get; private set; }
        public AudioFileInfo PlayingFileInfo { get; private set; }
        public string Statatus { get; set; }

	    private async void TestStreamListenExecute()
        {
            BasicPosition = TimeSpan.Zero;
            player.Stop();
	        bookStreamer.StopStream();
			//LocalBooks.FirstOrDefault(x=> x.Book.BookName == SelectedBroadcastAudioBook.Book.BookName)
            var stream = await bookStreamer.GetStreamingBook(SelectedBroadcastAudioBook,
               new Progress<ReceivmentProgress>(Handler));
            player.PlayStream(stream);
	        CurrentlyPlayingBook = SelectedBroadcastAudioBook;
	        PlayingFileInfo = SelectedBroadcastAudioBook.Book.CurrentFileInfo;
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
            Application.Current.Dispatcher.InvokeAsync(() =>
            {
                if (elem != null)
                    RemoteBooks.Remove(elem);
                RemoteBooks.Add(new AudioBooksInfoRemote(audioBooksInfoBroadcast));
            }, DispatcherPriority.DataBind);
        }

        private void StartListen()
        {
            if (IsListen)
                return;
            IsListen = true;
            discoverModule.StartListen();
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
        public ICommand PlayStreamCommand { get; private set; }
        public ICommand PausePlayCommand { get; private set; }
        public ICommand CancelPlayingCommand { get; private set; }
		public ICommand DownloadBookCommand { get; private set; }

        public DiscoverModule Module
        {
            get { return discoverModule; }
        }
    }
}
