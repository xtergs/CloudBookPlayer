using System;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using AudioBooksPlayer.WPF.Annotations;
using AudioBooksPlayer.WPF.DAL;
using AudioBooksPlayer.WPF.ExternalLogic;
using AudioBooksPlayer.WPF.Logic;
using AudioBooksPlayer.WPF.Model;
using AudioBooksPlayer.WPF.Streaming;
using GalaSoft.MvvmLight.CommandWpf;
using System.IO;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using AudioBooksPlayer.WPF.ViewModel;

namespace AudioBooksPlayer.WPF
{
    public class BaseViewModel : INotifyPropertyChanged
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
        private IFileSelectHelper fileSelectHelper;
        private AudioBooksProcessor audioProcessor;
        private AudioPlayer audioPlayer;
        private Context context;
        private Streaming.StreamingUDP streamer;
        private BookStreamer bookStreamer;


        public MainViewModel(IFileSelectHelper fileSelectHelper, AudioPlayer audioPlayer, Context context
            ,bool startupDiscovery = false, int discoverPort = -1)
        {
            if (fileSelectHelper == null)
                throw new ArgumentNullException(nameof(fileSelectHelper));
            if (audioPlayer == null)
                throw new ArgumentNullException(nameof(audioPlayer));
            if (this.context == context)
                throw new ArgumentNullException(nameof(context));
            
            this.fileSelectHelper = fileSelectHelper;
            this.audioPlayer = audioPlayer;
            this.context = context;

            Operations = new OperationsViewModel();

            discoverModule = new DiscoverModule();
            
            audioProcessor = new AudioBooksProcessor();
            streamer = new StreamingUDP();
            bookStreamer = new BookStreamer(streamer);

            audioPlayer.PlayingNextFile += AudioPlayerOnPlayingNextFile;
            //streamer.GetCommand += StreamerOnGetCommand;
            streamer.ConnectionChanged += StreamerOnConnectionChanged;
            streamer.StreamStatusChanged += StreamerOnStreamStatusChanged;

            SetupCommands();



            if (discoverPort >= 0)
                discoverModule.Port = discoverPort;
            if (startupDiscovery)
            {
                StartDiscovery.Execute(null);
                TestStreamingCommand.Execute(null);
            }
        }

        public OperationsViewModel Operations { get; private set; }

        private void StreamerOnStreamStatusChanged(object sender, StreamStatus streamStatus)
        {
            if (streamStatus.Status == StreamingStatus.Stream)
            {
                Interlocked.Increment(ref _activeStreams);
                Operations.AddOperation(streamStatus);
                OnPropertyChanged(nameof(ActiveStreams));
            }

            else
            {
                Interlocked.Decrement(ref _activeStreams);
                Operations.RemoteOperation(streamStatus.operationId);
                OnPropertyChanged(nameof(ActiveStreams));
            }
        }

        private void StreamerOnConnectionChanged(object sender, bool b)
        {
            if (b)
                ActiveConnections++;
            else
                ActiveConnections--;
        }

        private void AudioPlayerOnPlayingNextFile(object sender, EventArgs eventArgs)
        {
            if (PlayingAudioBook.Files.Length <= PlayingAudioBook.CurrentFile)
                return;
            PlayingFile = PlayingAudioBook.Files[PlayingAudioBook.CurrentFile];
            OnPropertyChanged(nameof(PlayingFile));
        }

        //private async void StreamerOnGetCommand(object sender, CommandFrame commandFrame)
        //{
        //    switch (commandFrame.Type)
        //    {
        //        case CommandEnum.StreamFileUDP:
        //        {
        //            var book = AudioBooks.Select(x => x.Files.First(y=> y.FilePath == commandFrame.Book)).FirstOrDefault();
        //            using (var stream = File.OpenRead(book.FilePath))
        //                await streamer.StartSendStream(stream,
        //                    new IPEndPoint(new IPAddress(commandFrame.ToIp), commandFrame.ToIpPort),
        //                    new Progress<StreamProgress>());
        //            break;
        //        }
        //    }
        //}

        private void SetupCommands()
        {
            TestStreamingCommand = new RelayCommand(TestStreamingExecute, TestStreamingCanExecute);
            StartDiscovery = new RelayCommand<bool>(StartDiscoveryExecute, (f) => f || discoverModule != null);
			//StreamBook = new RelayCommand(StreamBook);

		}

	    //private void StreamBook()
	    //{
		   // bookStreamer.StartStreamingServer();
	    //}

	    private bool TestStreamingCanExecute()
        {
            return true;
            //return SelectedAudioBook != null && SelectedAudioBook.Files.Any();
        }

        private void TestStreamingExecute()
        {
            Task.Run(() =>
            {
                bookStreamer.StartStreamingServer(new Progress<StreamProgress>(Handler));
            });
        }

        private void Handler(StreamProgress streamProgress)
        {
            //SendingProgress = streamProgress;
            Operations.OperationStatusChanged(streamProgress.OperationId, streamProgress.Position, streamProgress.Length);
        }

        public StreamProgress SendingProgress
        {
            get { return _progress; }
            set
            {
                if (value.Equals(_progress)) return;
                _progress = value;
                OnPropertyChanged();
            }
        }

        public bool IsPlaying
        {
            get { return _isPlaying; }
            set
            {
                if (value == _isPlaying)
                    return;
                _isPlaying = value;
                OnPropertyChanged();
            }
        }
        

        public bool IsBussy
        {
            get { return _isBussy; }
            set
            {
                if (value == _isBussy)
                    return;
                _isBussy = value;
                OnPropertyChanged();
            }
        }

        public string BussyStatus
        {
            get { return _bussyStatus; }
            set
            {
                if (value == _bussyStatus)
                    return;
                _bussyStatus = value;
                OnPropertyChanged();
            }
        }

        public bool IsDiscoverying
        {
            get { return _isDiscoverying; }
            set
            {
                if (value == _isDiscoverying) return;
                _isDiscoverying = value;
                OnPropertyChanged();
            }
        }

        public string DiscoveryStatus
        {
            get { return _discoveryStatus; }
            set
            {
                if (value == _discoveryStatus) return;
                _discoveryStatus = value;
                OnPropertyChanged();
            }
        }

        public AudioBooksInfo[] AudioBooks => context.AudioBooks;

        public AudioFileInfo[] SelectedBookFiles => SelectedAudioBook?.Files;

        public AudioBooksInfo SelectedAudioBook
        {
            get { return _selectedAudioBook; }
            set
            {
                if (Equals(value, _selectedAudioBook)) return;
                _selectedAudioBook = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(SelectedBookFiles));
                OnPropertyChanged(nameof(PlaySelectedAudioBook));
            }
        }


        private ICommand addAudioBook;
        private AudioBooksInfo _selectedAudioBook;
        private bool _isPlaying;
        private volatile bool _isBussy;
        private string _bussyStatus;
        private volatile bool _isDiscoverying;
        private string _discoveryStatus;
        private StreamProgress _progress;
        private AudioBooksInfo _playingAudioBook;
        private AudioFileInfo _playingFile;
        private Timer updateCurrentPositionTimer;

        public int ActiveConnections
        {
            get { return _activeConnections; }
            private set
            {
                if (value == _activeConnections) return;
                _activeConnections = value;
                OnPropertyChanged();
            }
        }

        public int ActiveStreams
        {
            get { return _activeStreams; }
            private set
            {
                if (value == _activeStreams) return;
                _activeStreams = value;
                OnPropertyChanged();
            }
        }

        public ICommand AddAudioBookCommand
        {
            get
            {
                return new RelayCommand(async () =>
                {
                    var folder = fileSelectHelper.SelectFolder();
                    if (folder == null)
                        return;
                    context.AddAudioBook(await Task.Run(()=> audioProcessor.ProcessAudoiBookFolder(folder)));
                    NotifyAudioBooksChanged();
                });
            }
        }
        public ICommand RemoteAudioBookCommand
        {
            get
            {
                return new RelayCommand(() =>
                {
                    context.RemoteAudioBook(SelectedAudioBook);
                    NotifyAudioBooksChanged();
                }, () => SelectedAudioBook != null);
            } }


	    public ICommand AddFolderBooksCommand
	    {
		    get
		    {
			    return new RelayCommand(async () =>
			    {
					var folder = fileSelectHelper.SelectFolder();
					if (folder == null)
						return;
				    RootFolderAudioBooks ffolder = await audioProcessor.ProcessFolderWithBooksAsync(folder);
				    context.AddRootFolder(ffolder);
				    foreach (var audioBooksInfo in ffolder.Books)
				    {
					    context.AddAudioBook(audioBooksInfo);
				    }
					NotifyAudioBooksChanged();
				});
		    }
	    }


		public void NotifyAudioBooksChanged()
        {
            OnPropertyChanged(nameof(AudioBooks));
            if (IsDiscoverying)
                StartDiscovery.Execute(true);
        }

        public AudioBooksInfo PlayingAudioBook
        {
            get { return _playingAudioBook; }
            private set
            {
                if (Equals(value, _playingAudioBook)) return;
                _playingAudioBook = value;
                PlayingFile = PlayingAudioBook.Files[PlayingAudioBook.CurrentFile];
                OnPropertyChanged();
            }
        }

        public AudioFileInfo PlayingFile
        {
            get { return _playingFile; }
            private set
            {
                if (Equals(value, _playingFile)) return;
                _playingFile = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(TotalFileTime));
            }
        }

        public double CurrentFileTime
        {
            get { return audioPlayer.CurrentTime.TotalSeconds; }
            set
            {
                if (value > 0)
                    audioPlayer.SetTimePosition(new TimeSpan(0,0,0, (int)value));
                OnPropertyChanged();
            }
        }

        public double TotalFileTime => PlayingFile?.Duration.TotalSeconds ?? 0;

        private void updateCurrentPositionTick(object state)
        {
            CurrentFileTime = -1;
        }

        public ICommand PlaySelectedAudioBook
        {
            get
            {
                return new RelayCommand(() =>
                {
                    PlayingAudioBook = SelectedAudioBook;
                    audioPlayer.PlayAudioBook(PlayingAudioBook);
                    IsPlaying = true;
                    if (updateCurrentPositionTimer == null)
                    {
                        var timeSpan = new TimeSpan(0, 0, 0, 1, 0);
                        updateCurrentPositionTimer = new Timer(updateCurrentPositionTick, null, timeSpan, timeSpan);
                    }
                }, () => SelectedAudioBook != null);
            }
        }

        public ICommand StopPlayingAudioBook
        {
            get
            {
                return new RelayCommand(() =>
                {
                    audioPlayer.StopPlay();
                    updateCurrentPositionTimer?.Dispose();
                    updateCurrentPositionTimer = null;
                    IsPlaying = false;
                }, ()=> IsPlaying);
            }
        }

        public ICommand PlayNextFile
        {
            get
            {
                return new RelayCommand(() =>
                {
                    audioPlayer.PlayNext();
                }, () => PlayingAudioBook?.LeftFilesToPlay() > 1);
            }
        }

        public ICommand PlayPrevFile
        {
            get
            {
                return new RelayCommand(() =>
                {
                    audioPlayer.PlayPrev();
                }, () => PlayingAudioBook?.CurrentFile > 0);
            }
        }

        public ICommand LoadData
        {
            get
            {
                return new RelayCommand(() =>
                {
                    if (IsBussy)
                        return;
                    IsBussy = true;
                    BussyStatus = "Loading data...";
                    context.LoadData();
                    NotifyAudioBooksChanged();
                    IsBussy = false;
                }, () => !IsBussy);
            }
        }

        public ICommand SaveDataCommand
        {
            get
            {
                return new RelayCommand(() =>
                {
                    if (IsBussy)
                        return;
                    IsBussy = true;
                    BussyStatus = "Saving data...";
                    context.SaveData();
                    IsBussy = false;
                }, () => !IsBussy);
            }
        }
        #region Discovery
        private readonly DiscoverModule discoverModule;
        private int _activeConnections = 0;
        private int _activeStreams;

        public ICommand StartDiscovery { get; private set; }

        private void StartDiscoveryExecute(bool force)
        {
            if (IsDiscoverying && !force)
            {
                IsDiscoverying = false;
                discoverModule.StopDiscovery();
                DiscoveryStatus = "Discovery stoped";
            }
            else
            {
                IsDiscoverying = true;
                discoverModule.StartDiscoverty(AudioBooks.ToList());
                DiscoveryStatus = "Broadcast";
            }
        }

        public void DiscoveryChnages()
        {
            if (IsDiscoverying)
                discoverModule.StartDiscoverty(AudioBooks.ToList());
        }
        #endregion

        public ICommand TestStreamingCommand { get; private set; }

        public DiscoverModule ModuleDiscoverModule => discoverModule;

		//public ICommand StreamBook { get; set; }
    }
}

