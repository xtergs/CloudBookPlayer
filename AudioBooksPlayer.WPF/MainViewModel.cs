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
using System.Threading.Tasks;

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
            ,bool startupDiscovery = false)
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

            audioProcessor = new AudioBooksProcessor();
            streamer = new StreamingUDP();
            bookStreamer = new BookStreamer();

            streamer.GetCommand += StreamerOnGetCommand;

            SetupCommands();

            if (startupDiscovery)
            {
                StartDiscovery.Execute(null);
                TestStreamingCommand.Execute(null);
            }
        }

        private async void StreamerOnGetCommand(object sender, CommandFrame commandFrame)
        {
            switch (commandFrame.Type)
            {
                case CommandEnum.StreamFile:
                {
                    var book = AudioBooks.First(x => x.BookName == commandFrame.Book);
                    using (var stream = File.OpenRead(book.Files.First().FilePath))
                        await streamer.StartSendStream(stream,
                            new IPEndPoint(new IPAddress(commandFrame.ToIp), commandFrame.ToIpPort),
                            new Progress<StreamProgress>());
                    break;
                }
            }
        }

        private void SetupCommands()
        {
            TestStreamingCommand = new RelayCommand(TestStreamingExecute, TestStreamingCanExecute);
            StartDiscovery = new RelayCommand<bool>(StartDiscoveryExecute, (f) => f || !IsDiscoverying);
        }

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
            SendingProgress = streamProgress;
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

        public AudioFileInfo[] SelectedBookFiles => SelectedAudioBook.Files;

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

        public ICommand AddAudioBookCommand
        {
            get
            {
                return new RelayCommand(() =>
                {
                    var folder = fileSelectHelper.SelectFolder();
                    if (folder == null)
                        return;
                    context.AddAudioBook(audioProcessor.ProcessAudoiBookFolder(folder));
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

        public ICommand PlaySelectedAudioBook
        {
            get
            {
                return new RelayCommand(() =>
                {

                    audioPlayer.PlayAudioBook(SelectedAudioBook);
                    IsPlaying = true;
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
                    IsPlaying = false;
                }, ()=> IsPlaying);
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

        public ICommand StartDiscovery { get; private set; }

        private void StartDiscoveryExecute(bool force)
        {
            IsDiscoverying = true;
            streamer.StartDiscoverty(AudioBooks.ToList());
            DiscoveryStatus = "Broadcast";
        }

        public ICommand TestStreamingCommand { get; private set; }



    }
}
