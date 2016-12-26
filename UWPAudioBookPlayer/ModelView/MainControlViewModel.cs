using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using Windows.Foundation;
using Windows.Media.Core;
using Windows.Media.Editing;
using Windows.Media.MediaProperties;
using Windows.Media.Playback;
using Windows.Storage;
using Windows.Storage.AccessCache;
using Windows.Storage.Streams;
using Windows.System;
using Windows.System.RemoteSystems;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Media;
using GalaSoft.MvvmLight.Command;
using Newtonsoft.Json;
using PropertyChanged;
using SQLite.Net.Attributes;
using UWPAudioBookPlayer.Comparer;
using UWPAudioBookPlayer.DAL;
using UWPAudioBookPlayer.DAL.Model;
using UWPAudioBookPlayer.Model;
using UWPAudioBookPlayer.Helper;
using UWPAudioBookPlayer.Service;
using Buffer = System.Buffer;

namespace UWPAudioBookPlayer.ModelView
{
    public class MainControlViewModelFactory
    {
        public static MainControlViewModel GetMainControlViewModel(MediaElement element, ISettingsService settings)
        {
            //return new MainControlViewModel(element, settings);
            return null;
        }
    }

    public enum MediaPlaybackFlow
    {
        Play,
        Stop,
        Pause
    }

    public enum CloudType
    {
        General,
        Local,
        DropBox,
        OneDrive,
        Online
    }

    public class Folder
    {
        [PrimaryKey]
        public string Path { get; set; }
        public string AccessToken { get; set; }
    }

    public enum OperationStatus
    {
        Started,
        Ended,
    }

    public class OperationProgress
    {
        public OperationProgress()
        {
            StopCommand = new RelayCommand(CancelOperation);
            PauseCommand = new RelayCommand(PauseOperation);
            ResumeCommand = new RelayCommand(ResumeOperation);

        }
        public UploadProgress Progress { get; set; }
        public ICommand StopCommand { get; private set; }
        public ICommand ResumeCommand { get; private set; }
        public ICommand PauseCommand { get; private set; }
        public bool IsPaused { get; private set; } = false;

        public void Stop()
        {
            if (StopCommand.CanExecute(this))
                StopCommand.Execute(this);
        }

        public void Pause()
        {
            if (PauseCommand.CanExecute(this))
                PauseCommand.Execute(this);
        }

        public void Resume()
        {
            if (ResumeCommand.CanExecute(this))
                ResumeCommand.Execute(this);
        }

        private void ResumeOperation()
        {
            this.Progress.IsPaused = false;
        }

        private void PauseOperation()
        {
            Progress.IsPaused = true;
        }

        private void CancelOperation()
        {
            Progress.IsCancelled = true;
        }
    }

    public class OperationsService
    {
        public bool AlreadyWorking(string operationName, CloudType type)
        {
            return Operations.Any(x => x.Progress.BookName == operationName && x.Progress.Type == type);
        }

        public ObservableCollection<OperationProgress> Operations { get; } = new ObservableCollection<OperationProgress>();

        public IProgress<UploadProgress> GetIProgressReporter()
        {
            return new Progress<UploadProgress>(ProgressReporterHandle);
        }

        private void ProgressReporterHandle(UploadProgress uploadProgress)
        {
            if (uploadProgress.OperationStatus == OperationStatus.Started)
                Operations.Add(GetOperationProgress(uploadProgress));
            else
                RemoveOperation(uploadProgress);


        }

        private void RemoveOperation(UploadProgress progress)
        {
            var operation = Operations.First(x => x.Progress.OperationId == progress.OperationId);
            Operations.Remove(operation);
        }

        private OperationProgress GetOperationProgress(UploadProgress proggress)
        {
            return new OperationProgress()
            {
                //                PauseCommand = _pauseCommand,
                //                StopCommand = _stopCommand,
                //                ResumeCommand = _resuemCommand,
                Progress = proggress
            };
        }
    }

    [ImplementPropertyChanged]
    public class UploadProgress
    {
        public Guid OperationId { get; set; }
        public string Status { get; set; }
        public CloudType Type { get; set; } = CloudType.Local;
        public string BookName { get; set; }
        public int MinVal { get; set; }
        public int Value { get; set; }
        public int MaximumValue { get; set; }
        public bool IsCancelled { get; set; }
        public bool IsPaused { get; set; }

        public OperationStatus OperationStatus { get; set; }
    }

    public class AudioBookSourcesCombined
    {
        public AudioBookSource MainSource { get; set; }
        public AudioBookSource Cloud { get; set; }
        public AudioBookSource[] Clouds { get; set; }
    }

    public struct RemoteOpenParams
    {
        public string Name { get; set; }
        public string FileName { get; set; }
        public TimeSpan PositionInFile { get; set; }
        public TimeSpan TotalDuration { get; set; }
        public bool IsLink { get; set; }
        public double PlayBackSpeed { get; set; }


        public string CloudStamp { get; set; }
        public string CloudAuthInfo { get; set; }
    }

    [ImplementPropertyChanged]
    public class MainControlViewModel : INotifyPropertyChanged
    {
        public delegate MainControlViewModel MainControlViewModelFactory(MediaElement mediaPlayer);

        private Folder baseFolder;
        private IDataRepository repository;
        private AudioBookSourceFactory factory;
        private readonly MediaPlayer player;
        private DAL.Model.CurrentState _currentState = new DAL.Model.CurrentState();
        private ISettingsService settings;
        private INotification notificator;
        private ManageSources manageSources;
        private readonly OperationsService operationService;
        private readonly ControllersService _controllersService;

        public ObservableAudioBooksCollection<AudioBookSourceWithClouds> Folders { get; private set; }

        public ObservableCollection<ICloudController> RefreshingControllers { get; } =
            new ObservableCollection<ICloudController>();

        public AudioBookSourceWithClouds[] LocalFolders => Folders.Where(x => !(x is AudioBookSourceCloud)).ToArray();

        //        public ObservableCollection<UploadProgress> UploadOperations { get; set; } =
        //            new ObservableCollection<UploadProgress>();

        public AudioBookSourceWithClouds SelectedFolder { get; set; }

        public AudioBookSourceWithClouds PlayingSource
        {
            get { return _playingSource; }
            set
            {
                _playingSource = value;
                OnPropertyChanged(nameof(PlayingSource));
            }
        }

        public AudiBookFile PlayingFile { get; private set; }

        public MediaPlaybackFlow State { get; set; } = MediaPlaybackFlow.Stop;

        public DAL.Model.CurrentState CurrentState
        {
            get { return _currentState; }
            set
            {
                _currentState = value ?? new DAL.Model.CurrentState();
                UpdateCurrentState();
            }
        }

        private async Task UpdateCurrentState()
        {
            if (CurrentState == null)
                return;
            var source = Folders.FirstOrDefault(f => f.Name == CurrentState.BookName);

            if (source != null && source.CurrentFile >= 0)
            {
                await SetSource(source).ConfigureAwait(false);
            }
        }

        private Timer sleepTimer;
        private Timer updateSleepValuesTimer;
        public int LeftSleepTimer { get; set; }

        public bool DropBoxRefreshing { get; private set; }

        public RelayCommand PlayCommand { get; private set; }
        public RelayCommand PauseCommand { get; private set; }
        public RelayCommand NextCommand { get; private set; }
        public RelayCommand PrevCommand { get; private set; }
        public RelayCommand<int> MoveSecsCommand { get; private set; }
        public RelayCommand<AudioBookSource> RemoveAudioBookSource { get; private set; }
        public RelayCommand<CloudType> AddCloudAccountCommand { get; private set; }
        public RelayCommand<ICloudController> RemoveCloudAccountCommand { get; private set; }
        public RelayCommand<ICloudController> UploadBookToCloudCommand { get; private set; }
        public RelayCommand DownloadBookFromDrBoxCommand { get; private set; }
        public RelayCommand<ICloudController> DownloadBookFromCloudCommand { get; private set; }
        public RelayCommand<AudioBookSourceWithClouds> ShowBookDetailCommand { get; private set; }
        public RelayCommand ChangeTimerStateCommand { get; private set; }
        public RelayCommand<BookMark> AddBookMarkCommand { get; private set; }
        public RelayCommand<AudioBookSourceWithClouds> ResumePlayBookCommand { get; set; }
        public RelayCommand<PlayBackHistoryElement> PlayHistoryElementCommand { get; private set; }

        public RelayCommand<AudioBookSourceWithClouds> AddSourceToLibraryCommand { get; private set; }
        public RelayCommand<AudioBookSourceWithClouds> StartPlaySourceCommand { get; private set; }
        public RelayCommand RefreshCloudsCommand { get; private set; }
        public RelayCommand RefreshAllCommand { get; private set; }


        public event EventHandler<AudioBookSourcesCombined> ShowBookDetails;

        public ISettingsService Settings
        {
            get { return settings; }
            set { settings = value; }
        }

        public List<double> AvaliblePlayBackSpeed { get; } = new List<double>()
        {
            0.5,
            0.6,
            0.7,
            0.8,
            0.9,
            1.0,
            1.1,
            1.2,
            1.3,
            1.4,
            1.5
        };

        private async void RefreshAll()
        {
            await RefreshAllAsync();
        }

        private async Task RefreshAllAsync()
        {
            await CheckBaseFolder();
            await RefreshCloudDataAsync();
        }

        public async void RefreshCloudData()
        {
            await RefreshCloudDataAsync();
        }

        private async Task RefreshCloudDataAsync()
        {
            foreach (var cloud in ControllersService1.Clouds)
            {
                await RefreshCloudData(cloud);
            }
        }

        public MainControlViewModel(RemoteDevicesService remoteDevicesService, ManageSources manage, OperationsService operService, ControllersService controllersService)
        {
            if (remoteDevicesService == null)
                Debug.WriteLine($"{nameof(remoteDevicesService)} is null!");
            _remoteDeviceService = remoteDevicesService;
            if (operService == null)
                throw new ArgumentNullException(nameof(operService));
            operationService = operService;
            _controllersService = controllersService;
            ControllersService1.FileChanged += CloudOnFileChanged;
            ControllersService1.MediaInfoChanged += CloudOnMediaInfoChanged;
            ControllersService1.AccountAlreadyAdded += ControllersServiceOnAccountAlreadyAdded;
            ControllersService1.ControllerDelted += ControllersService1OnControllerDelted;

            manageSources = manage;
            var mediaPlayer = new MediaPlayer()
            {
                AutoPlay = false,
            };

            if (mediaPlayer == null)
                throw new ArgumentNullException(nameof(mediaPlayer));
            this.player = mediaPlayer;
            factory = new AudioBookSourceFactory();
            Folders = new ObservableAudioBooksCollection<AudioBookSourceWithClouds>();
            repository = new JSonRepository();
            ApplicationData.Current.DataChanged += CurrentOnDataChanged;
            notificator = new UniversalNotification();

            PlayCommand = new RelayCommand(Play, CanPlay);
            PauseCommand = new RelayCommand(Pause, CanPause);
            NextCommand = new RelayCommand(NextFile,
                () => PlayingSource != null && PlayingSource.CurrentFile + 1 < PlayingSource.Files.Count());
            PrevCommand = new RelayCommand(PrevFile, () => PlayingSource != null && PlayingSource.CurrentFile - 1 >= 0);
            MoveSecsCommand = new RelayCommand<int>(MoveSecs, (c) => PlayingSource != null);
            RemoveAudioBookSource = new RelayCommand<AudioBookSource>(RemoveSource);
            AddCloudAccountCommand = new RelayCommand<CloudType>(AddDropBoxAccount);
            RemoveCloudAccountCommand = new RelayCommand<ICloudController>(RemoveCloudAccountAsync);
            UploadBookToCloudCommand = new RelayCommand<ICloudController>(UploadBookToCloud, controller => controller != null && controller.IsAutorized);
            DownloadBookFromCloudCommand = new RelayCommand<ICloudController>(DownloadBookFromCloud,
                controller => controller != null && controller.IsAutorized);
            ShowBookDetailCommand = new RelayCommand<AudioBookSourceWithClouds>(ShowBookDetailExecute);

            ChangeTimerStateCommand = new RelayCommand(ChangeTimerState);
            ResumePlayBookCommand = new RelayCommand<AudioBookSourceWithClouds>(ResumePlayBook);
            PlayHistoryElementCommand = new RelayCommand<PlayBackHistoryElement>(PlayHistoryElement, x => x != null);
            AddBookMarkCommand = new RelayCommand<BookMark>(AddBookMark);
            OnPropertyChanged(nameof(AddBookMarkCommand));
            AddSourceToLibraryCommand = new RelayCommand<AudioBookSourceWithClouds>(AddSourceToLibrary);
            StartPlaySourceCommand = new RelayCommand<AudioBookSourceWithClouds>(StartPlaySource);
            RefreshCloudsCommand = new RelayCommand(RefreshCloudData);
            RefreshAllCommand = new RelayCommand(RefreshAll);
            StreamToDeviceCommand = new RelayCommand<RemoteSystem>(StreamToDevice, CanStreamToDevice);

            player.CurrentStateChanged += PlayerOnCurrentStateChanged;
            player.PlaybackSession.PositionChanged += PlaybackSessionOnPositionChanged;
            player.MediaOpened += PlayerOnMediaOpened;
            player.MediaFailed += PlayerOnMediaFailed;
            player.MediaPlayerRateChanged += PlayerOnMediaPlayerRateChanged;
            player.MediaEnded += PlayerOnMediaEnded;
            player.PlaybackSession.BufferingStarted += PlayerOnBufferingStarted;
            player.PlaybackSession.BufferingEnded += PlayerOnBufferingEnded;
            player.PlaybackSession.BufferingProgressChanged += PlaybackSessionOnBufferingProgressChanged;
            player.PlaybackSession.DownloadProgressChanged += PlaybackSessionOnDownloadProgressChanged;

        }

        private void ControllersService1OnControllerDelted(object sender, ICloudController cloudController)
        {
            foreach (
                var folder in
                    Folders.OfType<AudioBookSourceCloud>().Where(x => x.CloudStamp == cloudController.CloudStamp).ToArray())
            {
                Folders.Remove(folder);
            }
        }

        private void ControllersServiceOnAccountAlreadyAdded(object sender, EventArgs eventArgs)
        {
            notificator.ShowMessage("You alread added this account", "You have already added this account");
            return;
        }


        public bool IsListChecked { get; set; }

        public bool IsShowPlayingImage => !IsListChecked && (Settings?.IsShowPlayingBookImage ?? true);

        private bool CanPause()
        {
            return PlayingSource != null && PlayingFile != null;
        }

        public bool IsCanPlay => PlayingSource != null || SelectedFolder != null;
        private bool CanPlay()
        {
            return PlayingSource != null || SelectedFolder != null;
        }

        private bool CanStreamToDevice(RemoteSystem arg)
        {
            return PlayingSource != null && PlayingFile != null;
        }

        private void PlayerOnMediaFailed(MediaPlayer sender, MediaPlayerFailedEventArgs args)
        {
            Debug.WriteLine($"{args.ErrorMessage}\nCode:{args.ExtendedErrorCode.Message}\n{args.ExtendedErrorCode.StackTrace}\n{args.ExtendedErrorCode}");
        }

        public double BufferingProgress { get; set; }
        public double DownloadingProgresss { get; set; }

        private async void PlaybackSessionOnDownloadProgressChanged(MediaPlaybackSession sender, object args)
        {
            await
                Windows.ApplicationModel.Core.CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(
                    CoreDispatcherPriority.Normal,
                    () =>
                    {
                        DownloadingProgresss = sender.DownloadProgress;
                    });
        }


        private async void PlaybackSessionOnBufferingProgressChanged(MediaPlaybackSession sender, object args)
        {
            await
                Windows.ApplicationModel.Core.CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(
                    CoreDispatcherPriority.Normal,
                    () =>
                    {
                        BufferingProgress = sender.BufferingProgress;
                    });
        }

        private void PlayerOnBufferingEnded(MediaPlaybackSession sender, object args)
        {

        }

        private void PlayerOnBufferingStarted(MediaPlaybackSession sender, object args)
        {

        }

        private async void StreamToDevice(RemoteSystem obj)
        {
            var cloudStamp = (PlayingSource as AudioBookSourceCloud)?.CloudStamp;
            RemoteOpenParams launchParameters =
            new RemoteOpenParams()
            {
                Name = PlayingSource.Name,
                CloudStamp = cloudStamp,
                IsLink = false,
                FileName = PlayingFile.Name,
                CloudAuthInfo = ControllersService1.GetController(cloudStamp)?.Token,
                PositionInFile = PlayingSource.Position,
                TotalDuration = PlayingSource.TotalDuration,
            };
            var serialized = JsonConvert.SerializeObject(launchParameters);
            var result = await _remoteDeviceService.LaunchOnRemote(obj, serialized, "1");

            switch (result)
            {
                case RemoteLaunchUriStatus.Success:
                    Pause();
                    break;
                default:
                    await notificator.ShowMessage("Error",
                        $"Occured error trying to launch app on remote machine {obj.DisplayName}\nErrorCode: {result}")
                        .ConfigureAwait(false);
                    break;
            }
        }

        private void PlayerOnMediaEnded(MediaPlayer sender, object args)
        {
            //TODO next chapter
        }

        private void CurrentOnDataChanged(ApplicationData sender, object args)
        {

        }

        private bool _isOpening;

        private void PlayerOnMediaPlayerRateChanged(MediaPlayer sender, MediaPlayerRateChangedEventArgs args)
        {
            PlayingSource.SetPlayBackRate(sender.PlaybackSession.PlaybackRate);
            PlayBackRate = sender.PlaybackSession.PlaybackRate;
        }

        private void PlayerOnMediaOpened(MediaPlayer sender, object args)
        {
            FileDuration = sender.PlaybackSession.NaturalDuration;
            player.PlaybackSession.Position = PlayingSource.Position;
            player.PlaybackRate = PlayingSource.PlaybackRate;
            PlayBackRate = PlayingSource.PlaybackRate;
            Debug.WriteLine($"Media opened positin is {PlayingSource.Position}");
            _isOpening = false;
        }

        public TimeSpan CurrentPosition
        {
            get { return PlayingSource?.Position ?? TimeSpan.Zero; }
            set
            {
                player.PlaybackSession.Position = value;
            }
        }

        private async void PlaybackSessionOnPositionChanged(MediaPlaybackSession sender, object args)
        {
            if (_isOpening)
                return;
            await Windows.ApplicationModel.Core.CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(
                    CoreDispatcherPriority.Low,
                    () =>
                    {
                        PlayingSource?.SetPosition(sender.Position);
                        OnPropertyChanged(nameof(CurrentPosition));
                    });
        }

        public MediaPlayer Player => player;

        public TimeSpan FileDuration { get; set; }
        public double PlayBackRate { get; set; }

        private async void AddSourceToLibrary(AudioBookSourceWithClouds obj)
        {
            if (obj == null)
                throw new ArgumentNullException(nameof(obj));

            if (Folders.Any(x => x.Name == obj.Name))
                return;
            Folders.Add(obj);
            await SaveData();
        }

        private void AddBookMark(BookMark obj)
        {
            if (PlayingSource.BookMarks == null)
                PlayingSource.BookMarks = new List<BookMark>();
            if (repository.AddBookMark(PlayingSource, obj))
                if (obj.IsRange)
                {
                    CutAndSaveBookMarkFile(PlayingSource, obj.FileName, obj, OperationService.GetIProgressReporter());
                    repository.UpdateBookMark(PlayingSource, obj);
                }
            OnPropertyChanged(nameof(BookMarksForSelectedPlayingBook));
        }

        Regex unspupportedRegex = new Regex(@"([<>:"" /\|? *])", RegexOptions.IgnoreCase);

        private async void CutAndSaveBookMarkFile(AudioBookSourceWithClouds book, string fileName, BookMark bookmark, IProgress<UploadProgress> reporter)
        {
            var trimOperation = new UploadProgress()
            {
                BookName = fileName,
                OperationId = Guid.NewGuid(),
                Type = CloudType.Local,
                Status = "Trimming...",
                OperationStatus = OperationStatus.Started
            };

            reporter.Report(trimOperation);

            string validTitle = unspupportedRegex.Replace(bookmark.Title.Substring(0, Math.Min(50, bookmark.Title.Length)), " ").Trim();
            if (string.IsNullOrWhiteSpace(validTitle))
                validTitle = "bookmark";
            string bookMarkFileName = $"{bookmark.Order}_{validTitle}{Path.GetExtension(fileName)}";
            try
            {
                var folder = await StorageApplicationPermissions.FutureAccessList.GetFolderAsync(book.AccessToken);
                var file = await folder.GetFileAsync(fileName);
                var encodingProfile = await MediaEncodingProfile.CreateFromFileAsync(file);
                var clip = await MediaClip.CreateFromFileAsync(file);
                clip.TrimTimeFromStart = bookmark.Position;
                clip.TrimTimeFromEnd = clip.OriginalDuration.Subtract(bookmark.EndPosition);

                var composition = new MediaComposition();
                composition.Clips.Add(clip);


                var fileToWrite = await
                    (await folder.CreateFolderAsync("bookmarks", CreationCollisionOption.OpenIfExists)).CreateFileAsync(
                        bookMarkFileName);
                try
                {
                    var result =
                        await composition.RenderToFileAsync(fileToWrite, MediaTrimmingPreference.Precise, encodingProfile);

                    if (result != Windows.Media.Transcoding.TranscodeFailureReason.None)
                    {
                        Debug.WriteLine("Trying to trim file");
                        Debug.WriteLine(result.ToString());
                        await notificator.ShowMessage("", $"Occured error trimming file {fileName}");
                    }

                }
                catch (Exception e)
                {
                    bookMarkFileName = Path.GetFileNameWithoutExtension(fileToWrite.Name) + ".wav";
                    await
                        fileToWrite.RenameAsync(bookMarkFileName,
                            NameCollisionOption.ReplaceExisting);
                    var encoding = MediaEncodingProfile.CreateWav(AudioEncodingQuality.High);
                    composition.Clips.Clear();
                    composition = null;
                    composition = new MediaComposition();
                    composition.Clips.Add(clip);
                    var result =
                        await
                            composition.RenderToFileAsync(fileToWrite, MediaTrimmingPreference.Precise, encoding);

                    if (result != Windows.Media.Transcoding.TranscodeFailureReason.None)
                    {
                        Debug.WriteLine("Trying to trim file");
                        Debug.WriteLine(result.ToString());
                        await notificator.ShowMessage("", $"Occured error trimming file {fileName}");
                    }

                }
                bookmark.FileName = bookMarkFileName;
            }
            finally
            {
                trimOperation.OperationStatus = OperationStatus.Ended;
                reporter.Report(trimOperation);
            }
        }

        private void PlayHistoryElement(PlayBackHistoryElement obj)
        {
            PlayingSource.CurrentFile =
                PlayingSource.Files.IndexOf(PlayingSource.Files.First(x => x.Name == obj.FileName));
            PlayingSource.Position = obj.Position;
            Play();
        }

        private async void ResumePlayBook(AudioBookSourceWithClouds audioBookSourceDetailWithCloud)
        {
            if (PlayingSource != audioBookSourceDetailWithCloud)
                Pause();
            player.Source = null;
            //PlayingSource = audioBookSourceDetailWithCloud;
            await SetSource(audioBookSourceDetailWithCloud);
            await Task.Delay(100);
            Play();
        }

        private void ChangeTimerState()
        {
            if (CurrentState.SleepTimerIsSet)
            {
                CurrentState.SleepTimerIsSet = false;
            }
            else
            {
                CurrentState.SleepTimerIsSet = true;
                SetTimer();
            }

        }

        private void SetTimer()
        {
            if (sleepTimer == null)
            {
                sleepTimer = new Timer(Callback, null, (int)CurrentState.SleepTimerDuration.TotalMilliseconds, -1);
                LeftSleepTimer = (int)CurrentState.SleepTimerDuration.TotalSeconds;
                updateSleepValuesTimer = new Timer(DecreaseSleepValue, null, 1000, 1000);
            }
            else
            {
                sleepTimer.Change(CurrentState.SleepTimerDuration, TimeSpan.MaxValue);
                updateSleepValuesTimer.Change(int.MaxValue, int.MaxValue);
                LeftSleepTimer = int.MaxValue;
            }
        }

        private void DecreaseSleepValue(object state)
        {
            if (LeftSleepTimer > 0)
                LeftSleepTimer--;
        }

        private async void Callback(object state)
        {
            var res =
                await
                    notificator.ShowMessageWithTimer("Timer is out", "Timer is out, will stop after 5 seconds",
                        ActionButtons.Cancel | ActionButtons.Continue, 5 * 1000);
            if (res == ActionButtons.Continue)
            {
                SetTimer();
                return;
            }
            else
                Pause();
        }


        private async void ShowBookDetailExecute(AudioBookSourceWithClouds book)
        {
            if (book == null)
                return;
            var ev = new AudioBookSourcesCombined() { MainSource = book };
            if (ControllersService1.Controllers.Any())
            {
                List<AudioBookSource> sources = new List<AudioBookSource>();
                foreach (var cloud in ControllersService1.Controllers)
                    sources.Add(await cloud.GetAudioBookInfo(book));
                ev.Clouds = sources.ToArray();
            }
            OnShowBookDetails(ev);
        }


        private async Task<bool> IsAlreadyInFolder(StorageFile[] filesOnDisk, AudiBookFile audioFile)
        {
            StorageFile temp = null;
            if ((temp = filesOnDisk.FirstOrDefault(x => x.Name == audioFile.Name)) !=
                null)
            {
                var property = await temp.GetBasicPropertiesAsync();
                if (property.Size == audioFile.Size)
                    return true;
            }
            return false;
        }

        private async Task<ulong> WriteStreamToFile(Stream stream, StorageFolder bookFolder, AudiBookFile audioFile, byte[] buff = null)
        {
            if (stream == null)
                return 0;
            byte[] buffer;
            if (buff == null)
                buffer = new byte[1024 * 1024 * 4];
            else
                buffer = buff;
            int readed = 0;
            var file =
                await
                    bookFolder.CreateFileAsync(audioFile.Name,
                        CreationCollisionOption.ReplaceExisting);

            uint written = 0;
            using (var random = await file.OpenTransactedWriteAsync())
            {
                while ((readed = stream.Read(buffer, 0, buffer.Length)) >= 0 &&
                       written < audioFile.Size)
                {
                    random.Stream.AsStreamForWrite().Write(buffer, 0, readed);
                    written += (uint)readed;
                }
                await random.Stream.FlushAsync();
                await random.CommitAsync();
            }
            return written;
        }

        private async Task DownloadBookFromCloud(ICloudController cloudControllser, AudioBookSourceWithClouds tempSelectedFolder, AudioBookSourceWithClouds originalSelectedFolder, IProgress<UploadProgress> progres)
        {
            UploadProgress progress = new UploadProgress()
            {
                BookName = tempSelectedFolder.Name,
                MaximumValue = tempSelectedFolder.Files.Count(),
                OperationId = Guid.NewGuid(),
                Status = "Downloading",
                Type = cloudControllser.Type,
                OperationStatus = OperationStatus.Started
            };
            progres.Report(progress);
            try
            {
                StorageFolder bookFolder = null;
                if (originalSelectedFolder == null)
                {
                    var dir =
                        await
                            StorageApplicationPermissions.FutureAccessList.GetFolderAsync(baseFolder.AccessToken);
                    bookFolder =
                        await
                            dir.CreateFolderAsync(tempSelectedFolder.Folder, CreationCollisionOption.OpenIfExists);

                    originalSelectedFolder = new AudioBookSourceWithClouds()
                    {
                        IsHaveDropBox = true,
                        Name = tempSelectedFolder.Name,
                        AccessToken = StorageApplicationPermissions.FutureAccessList.Add(bookFolder),
                        Path = bookFolder.Path,
                        CountFilesDropBoxTotal = tempSelectedFolder.AvalibleCount,
                        CountFilesDropBox = tempSelectedFolder.Files.Count(),
                    };
                    originalSelectedFolder.AdditionSources.Add(tempSelectedFolder);
                    var index = Folders.IndexOf(tempSelectedFolder);
                    if (index >= 0)
                    {
                        Folders.Insert(index, originalSelectedFolder);
                        Folders.Remove(tempSelectedFolder);
                    }
                    else
                        Folders.Add(originalSelectedFolder);
                }
                else
                {
                    bookFolder =
                        await
                            StorageApplicationPermissions.FutureAccessList.GetFolderAsync(
                                originalSelectedFolder.AccessToken);
                    if (bookFolder == null)
                    {
                        Debug.WriteLine("Not found folder by access token, but should");
                        return;
                    }
                }


                var alreadyInFolder =
                    (await bookFolder.GetFilesAsync()).ToArray();

                byte[] buffer = new byte[1024 * 4 * 4];
                var avalibleFiles = tempSelectedFolder.AvalibleFiles;
                for (int i = 0; i < tempSelectedFolder.AvalibleCount; i++)
                {
                    progress.Value = i;
                    while (progress.IsPaused && !progress.IsCancelled)
                        await Task.Delay(500);
                    if (progress.IsCancelled)
                        return;
                    if (await IsAlreadyInFolder(alreadyInFolder, avalibleFiles[i]))
                        continue;
                    var index = i;
                    await Task.Run(async () =>
                    {
                        var stream =
                            await
                                cloudControllser.DownloadBookFile(tempSelectedFolder.Folder,
                                    avalibleFiles[index].Name);
                        // somehow we didn't found a file
                        ulong writed = await WriteStreamToFile(stream, bookFolder, avalibleFiles[index], buffer);
                        if (writed <= 0)
                            return;
                        originalSelectedFolder.Files.Add(new AudiBookFile()
                        {
                            Duration = avalibleFiles[index].Duration,
                            IsAvalible = true,
                            Name = avalibleFiles[index].Name,
                            Order = avalibleFiles[index].Order,
                            Size = writed
                        });
                    });
                }
                originalSelectedFolder.CreationDateTimeUtc = tempSelectedFolder.CreationDateTimeUtc;
                originalSelectedFolder.ModifiDateTimeUtc = tempSelectedFolder.ModifiDateTimeUtc;
            }
            finally
            {
                progress.OperationStatus = OperationStatus.Ended;
                progres.Report(progress);
            }
        }

        private async void DownloadBookFromCloud(ICloudController cloudControllser)
        {
            if (!cloudControllser.IsAutorized)
            {
                notificator.ShowMessage("", "Before procide, pelase authorize in DropBox");
                return;
            }
            if (SelectedFolder is OnlineAudioBookSource ||
                (cloudControllser.Type == CloudType.Online &&
                 SelectedFolder.AdditionSources.Any(x => x is OnlineAudioBookSource)))
            {
                await DownloadBookFromOnline(SelectedFolder as OnlineAudioBookSource, OperationService.GetIProgressReporter());
                return;
            }
            if (!(SelectedFolder is OnlineAudioBookSource) && !cloudControllser.IsCloud)
                return;
            AudioBookSourceWithClouds tempSelectedFolder = SelectedFolder;
            AudioBookSourceWithClouds originalSelectedFolder = null;
            if (SelectedFolder is AudioBookSourceCloud)
            {
                if (string.IsNullOrWhiteSpace(baseFolder?.AccessToken))
                {
                    notificator.ShowMessage("", "Before procide, please select base folder");
                    return;
                }
                tempSelectedFolder = SelectedFolder;
            }
            else
            {
                originalSelectedFolder = tempSelectedFolder;
                tempSelectedFolder = await cloudControllser.GetAudioBookInfo(tempSelectedFolder.Folder);
            }
            if (tempSelectedFolder == null)
                return;
            await
                DownloadBookFromCloud(cloudControllser, tempSelectedFolder, originalSelectedFolder,
                    OperationService.GetIProgressReporter());
        }


        private async Task DownloadBookFromOnline(OnlineAudioBookSource source, IProgress<UploadProgress> reporter)
        {
            AudioBookSourceWithClouds tempSelectedFolder = source;
            AudioBookSourceWithClouds originalSelectedFolder = null;
            if (string.IsNullOrWhiteSpace(baseFolder?.AccessToken))
            {
                notificator.ShowMessage("", "Before procide, please select base folder");
                return;
            }
            tempSelectedFolder = SelectedFolder;

            if (tempSelectedFolder == null)
                return;
            UploadProgress progress = new UploadProgress()
            {
                BookName = tempSelectedFolder.Name,
                MaximumValue = tempSelectedFolder.Files.Count(),
                OperationId = Guid.NewGuid(),
                Status = "Downloading",
                Type = CloudType.Online,
                OperationStatus = OperationStatus.Started
            };
            reporter.Report(progress);
            try
            {
                StorageFolder bookFolder = null;

                var dir =
                    await
                        StorageApplicationPermissions.FutureAccessList.GetFolderAsync(baseFolder.AccessToken);
                bookFolder =
                    await
                        dir.CreateFolderAsync(tempSelectedFolder.Folder, CreationCollisionOption.OpenIfExists);
                originalSelectedFolder = new AudioBookSourceWithClouds()
                {
                    Name = tempSelectedFolder.Name,
                    AccessToken = StorageApplicationPermissions.FutureAccessList.Add(bookFolder),
                    Path = bookFolder.Path,
                    IsLocked = tempSelectedFolder.IsLocked,
                    Files = tempSelectedFolder.Files.Select(x =>
                    {
                        var cloned = x.Clone();
                        cloned.IsAvalible = false;
                        return cloned;
                    }).ToList(),
                    Position = tempSelectedFolder.Position,
                    TotalDuration = tempSelectedFolder.TotalDuration
                };
                originalSelectedFolder.AdditionSources.Add(tempSelectedFolder);
                foreach (var addition in tempSelectedFolder.AdditionSources)
                    originalSelectedFolder.AdditionSources.Add(addition);
                var index = Folders.IndexOf(tempSelectedFolder);
                if (index >= 0)
                {
                    Folders.Insert(index, originalSelectedFolder);
                    Folders.Remove(tempSelectedFolder);
                }
                else
                    Folders.Add(originalSelectedFolder);

                var alreadyInFolder =
                    (await bookFolder.GetFilesAsync()).ToList();
                int readed = 0;
                byte[] buffer = new byte[1024 * 4 * 4];
                var avalibleFiles = tempSelectedFolder.Files;
                for (int i = 0; i < tempSelectedFolder.Files.Count; i++)
                {
                    progress.Value = i;
                    while (progress.IsPaused && !progress.IsCancelled)
                        await Task.Delay(500);
                    if (progress.IsCancelled)
                        return;
                    StorageFile temp = null;
                    if ((temp = alreadyInFolder.FirstOrDefault(x => x.Name == avalibleFiles[i].Name)) !=
                        null)
                    {
                        var property = await temp.GetBasicPropertiesAsync();
                        if (property.Size == avalibleFiles[i].Size && avalibleFiles[i].Size > 0)
                            continue;
                    }
                    index = i;
                    await Task.Run(async () =>
                    {
                        using (HttpClient client = new HttpClient())
                        {
                            var stream = await client.GetStreamAsync(source.Files[index].Path);
                            var file =
                                await
                                    bookFolder.CreateFileAsync(avalibleFiles[index].Name,
                                        CreationCollisionOption.ReplaceExisting);

                            using (var random = await file.OpenTransactedWriteAsync())
                            {
                                uint written = 0;
                                while ((readed = stream.Read(buffer, 0, buffer.Length)) > 0 && ((
                                                                                                     written <=
                                                                                                     avalibleFiles[index
                                                                                                     ].Size &&
                                                                                                     avalibleFiles[index
                                                                                                     ].Size > 0) ||
                                                                                                 avalibleFiles[index]
                                                                                                     .Size == 0))
                                {
                                    random.Stream.AsStreamForWrite().Write(buffer, 0, readed);
                                    written += (uint)readed;
                                }
                                await random.Stream.FlushAsync();
                                await random.CommitAsync();
                            }
                            var f = originalSelectedFolder.Files.First(x => x.Name == avalibleFiles[index].Name);
                            f.IsAvalible = true;
                            f.Size = (await file.GetBasicPropertiesAsync()).Size;
                        }
                    });
                }
                originalSelectedFolder.CreationDateTimeUtc = tempSelectedFolder.CreationDateTimeUtc;
                originalSelectedFolder.ModifiDateTimeUtc = tempSelectedFolder.ModifiDateTimeUtc;
            }
            finally
            {
                progress.OperationStatus = OperationStatus.Ended;
                reporter.Report(progress);
            }
        }


        //private async void DownloadBookFromDrBok()
        //{
        //    DrownloadBookFromCloud(DrbController);
        //}

        private async Task UploadBookmarksToCloud(ICloudController controller, AudioBookSourceWithClouds source)
        {
            var rangedBookmarks = repository.BookMarks(source)?.Where(x => x.IsRange)?.ToArray();
            if (rangedBookmarks == null)
                return;
            foreach (var bookmark in rangedBookmarks)
            {
                var stream = await factory.GetBookMark(source, bookmark);
                await controller.Uploadfile(source, stream.Key, stream.Value, factory.BookMarksFolder);
            }

        }

        private async Task UploadImagesToCloud(ICloudController controller, AudioBookSourceWithClouds source)
        {
            if (!source.Images.Any())
                return;
            foreach (var image in source.Images)
            {
                await controller.Uploadfile(source, image.Url, await factory.GetImageAsStream(source, image.Url));
            }
        }

        private async Task UploadBookToCloud(ICloudController obj, AudioBookSourceWithClouds selectedFolderTemp, UploadProgress UploadOperation)
        {
            var drBookInfo = await obj.GetAudioBookInfo(selectedFolderTemp);
            if (drBookInfo == null)
            {
                //drBookInfo = new AudioBookSourceCloud();
            }
            await obj.UploadBookMetadata(selectedFolderTemp);
            var dir =
                await StorageApplicationPermissions.FutureAccessList.GetFolderAsync(selectedFolderTemp.AccessToken);
            for (int i = 0; i < selectedFolderTemp.Files.Count(); i++)
            {
                UploadOperation.Value = i;
                while (UploadOperation.IsPaused && !UploadOperation.IsCancelled)
                    await Task.Delay(500);
                if (UploadOperation.IsCancelled)
                    return;
                var fl = await dir.GetFileAsync(selectedFolderTemp.Files[i].Name);
                var drFile = (drBookInfo?.AvalibleFiles?.FirstOrDefault(f => (f.Name == fl.Name)));
                if (drFile != null)
                {
                    var size = (await fl.GetBasicPropertiesAsync()).Size;
                    if (size == drFile.Size)
                        continue;
                    bool check = true;
                    while (check)
                    {
                        var freeSpace = await obj.GetFreeSpaceBytes();
                        if (size > freeSpace)
                        {
                            var result = await notificator.ShowMessage("Not enough free space",
                                "Not enough free space in cloud, please free and continue",
                                ActionButtons.Cancel, ActionButtons.Retry);
                            if (result == ActionButtons.Cancel)
                                return;

                        }
                        else
                            check = false;
                    }
                }
                using (var stream = await fl.OpenAsync(FileAccessMode.Read))
                    await
                        obj.Uploadbook(selectedFolderTemp.Folder, selectedFolderTemp.Files[i].Name,
                            stream.AsStreamForRead());

                //selectedFolderTemp.CountFilesDropBox = i + 1;
                //selectedFolderTemp.IsHaveDropBox = true;
            }
        }

        private async void UploadBookToCloud(ICloudController obj)
        {
            var selectedFolderTemp = SelectedFolder;
            if (selectedFolderTemp == null || (selectedFolderTemp as AudioBookSourceCloud)?.CloudStamp == obj.CloudStamp)
                return;
            if (!selectedFolderTemp.Files.Any() || !obj.IsCloud)
                return;
            if (OperationService.AlreadyWorking(selectedFolderTemp.Name, obj.Type))
                return;
            var UploadOperation = new UploadProgress()
            {
                OperationId = Guid.NewGuid(),
                Status = "Uploading",
                BookName = selectedFolderTemp.Name,
                MaximumValue = selectedFolderTemp.Files.Count,
                OperationStatus = OperationStatus.Started
            };
            var reporter = OperationService.GetIProgressReporter();
            reporter.Report(UploadOperation);
            try
            {
                await UploadBookToCloud(obj, selectedFolderTemp, UploadOperation);
                await UploadBookmarksToCloud(obj, selectedFolderTemp);
                await UploadImagesToCloud(obj, selectedFolderTemp);
            }
            finally
            {
                UploadOperation.OperationStatus = OperationStatus.Ended;
                reporter.Report(UploadOperation);
            }
        }

        //private async void UploadBookToDrBox()
        //{
        //    UploadBookToCloud(drbController);
        //}

        private async void PlayerOnCurrentStateChanged(MediaPlayer mediaPlayer, object args)
        {
            Debug.WriteLine($"CurrentStateChanged to {mediaPlayer.PlaybackSession.PlaybackState}");
            if (_isOpening)
            {
                var state = player.PlaybackSession.PlaybackState;
                return;
            }
            await Windows.ApplicationModel.Core.CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(
                CoreDispatcherPriority.Normal,
                () =>
                {
                    switch (player.PlaybackSession.PlaybackState)
                    {
                        case MediaPlaybackState.Playing:
                            //                            if (_isOpening)
                            //                                break;
                            player.PlaybackSession.PlaybackRate = PlayingSource.PlaybackRate;
                            PlayingSource.AddHistory(PlayBackHistoryElement.HistoryType.Play);
                            break;


                        case MediaPlaybackState.Paused:
                            if (
                                Math.Abs(player.PlaybackSession.Position.TotalSeconds -
                                         player.PlaybackSession.NaturalDuration.TotalSeconds) < 1)
                                NextFile();
                            else
                                PlayingSource.AddHistory(PlayBackHistoryElement.HistoryType.Pause);
                            break;

                        case MediaPlaybackState.Buffering:
                            BufferingProgress = player.PlaybackSession.BufferingProgress;
                            break;
                    }
                });
        }

        private void AddSource(AudioBookSourceWithClouds source)
        {
            if (source == null)
                return;
            var inFolders = Folders.FirstOrDefault(x => x.Name == source.Name);
            if (inFolders == null)
            {
                if (source.AvalibleCount > 0)
                    Folders.Add(source);
                return;
            }

            var fromCloud = inFolders as AudioBookSourceCloud;
            //Same entry from cloud already persist
            if (fromCloud != null && fromCloud.CloudStamp == (source as AudioBookSourceCloud)?.CloudStamp)
            {
                fromCloud.Files = MergeFilesLists(source.Files, fromCloud.Files);
                return;
            }
            var isCloud = source as AudioBookSourceCloud;
            if (isCloud != null)
            {
                if (
                    inFolders.AdditionSources.OfType<AudioBookSourceCloud>()
                        .Any(
                            s =>
                                s.CloudStamp == isCloud.CloudStamp &&
                                s.CreationDateTimeUtc == isCloud.CreationDateTimeUtc &&
                                s.ModifiDateTimeUtc == isCloud.ModifiDateTimeUtc))
                    return;
                var old = inFolders.AdditionSources.OfType<AudioBookSourceCloud>()
                    .Where(s => s.CloudStamp == isCloud.CloudStamp)
                    .ToList();
                foreach (var s in old)
                    inFolders.AdditionSources.Remove(s);
            }
            if (inFolders.CreationDateTimeUtc > source.CreationDateTimeUtc)
            {
                UpdateAudioBookWithClouds(inFolders, source);

            }
            else if (inFolders.CreationDateTimeUtc < source.CreationDateTimeUtc)
            {
                var tempLocal = source;
                UpdateAudioBookWithClouds(source, inFolders);
                //tasks.Add(controller.UploadBookMetadata(inFolders));
            }
            else
            if (inFolders.ModifiDateTimeUtc < source.ModifiDateTimeUtc)
            {
                UpdateAudioBookWithClouds(inFolders, source);
            }
            else if (inFolders.ModifiDateTimeUtc > source.ModifiDateTimeUtc)
            {
                UpdateAudioBookWithClouds(source, inFolders);
                //                    var tempLocal = f;
                //                    tasks.Add(controller.UploadBookMetadata(inFolders));
            }
            if (source.AvalibleCount <= 0)
                return;
            if (source is AudioBookSourceCloud)
                inFolders.AdditionSources.Add(source);
            else
            {
                source.AdditionSources.Add(inFolders);
                foreach (var f in inFolders.AdditionSources)
                    source.AdditionSources.Add(f);
                Folders.Remove(inFolders);
                Folders.Add(source);
            }
        }

        private void AddSources(AudioBookSourceWithClouds[] soures)
        {
            if (soures == null)
                return;
            foreach (var source in soures)
            {
                AddSource(source);
            }
        }

        public bool LoadingData { get; private set; }

        public async void StartObserveDevices()
        {
            if (_remoteDeviceService == null)
                return;
            var res = (await _remoteDeviceService.RequestAccess());
            switch (res)
            {
                case RemoteSystemAccessStatus.Allowed:
                case RemoteSystemAccessStatus.Unspecified:
                    _remoteDeviceService.StartWatch();
                    _remoteDeviceService.SystemsUpdated += RemoteDeviceServiceOnSystemsUpdated;
                    break;
            }
        }

        private void RemoteDeviceServiceOnSystemsUpdated(object sender, EventArgs eventArgs)
        {
            OnPropertyChanged(nameof(RemoteSystems));
        }


        public void StopObserveDevices()
        {
            if (_remoteDeviceService == null)
                return;
            _remoteDeviceService.StopWatch();
            _remoteDeviceService.SystemsUpdated -= RemoteDeviceServiceOnSystemsUpdated;
        }

        public List<RemoteSystem> RemoteSystems => _remoteDeviceService?.RemoteSystems.ToList();

        public bool IsNothingFound
        {
            get { return (!LoadingData && !CheckingBaseFolder && !RefreshingControllers.Any()) && Folders?.Count <= 0; }
        }

        public async Task LoadData()
        {
            if (LoadingData)
                return;
            LoadingData = true;
            try
            {
                Stopwatch watch = new Stopwatch();
                watch.Start();
                var loaded = await repository.Load();
                Folders =
                    new ObservableAudioBooksCollection<AudioBookSourceWithClouds>();
                AddSources(loaded.AudioBooks.Select(x =>
                {
                    x.IgnoreTimeOfChanges = false;
                    return x;
                }).ToArray());
                AddSources(loaded.OnlineBooks.Select(x =>
                {
                    x.CloudStamp = "LibriVox";
                    x.Type = CloudType.Online;
                    x.IgnoreTimeOfChanges = false;
                    return x;
                }).ToArray());
                if (PlayingSource == null)
                    CurrentState = loaded.CurrentState;
                await ControllersService1.InicializeControllers(loaded.CloudServices);
                baseFolder = loaded.BaseFolder;
                await CheckBaseFolder();
                foreach (var cloud in ControllersService1.GetOnlyClouds())
                    await RefreshCloudData(cloud);
                watch.Stop();
                Debug.WriteLine($"load data is {watch.ElapsedMilliseconds} ms");
            }
            finally
            {
                LoadingData = false;
                ControlStateChanged();
            }
        }

        private void CloudOnFileChanged(object sender, FileChangedStruct fileChangedStruct)
        {

        }

        private void CloudOnMediaInfoChanged(object sender, AudioBookSourceCloud audioBookSourceCloud)
        {
            AddSource(audioBookSourceCloud);
            if (PlayingSource.Name == audioBookSourceCloud.Name)
            {
                PlayingSource.CurrentFile = audioBookSourceCloud.CurrentFile;
                CurrentPosition = audioBookSourceCloud.Position;
                PlayingSource.SetPosition(audioBookSourceCloud.Position);
            }
        }

        List<AudiBookFile> MergeFilesLists(List<AudiBookFile> newList, List<AudiBookFile> oldList)
        {
            for (int i = 0; i < newList.Count; i++)
            {
                newList[i].IsAvalible = oldList.Any(x => x.Name == newList[i].Name && x.IsAvalible);
            }
            return newList;
        }

        private AudioBookSourceCloud[] drFolders = new AudioBookSourceCloud[] { };

        bool AddRefreshCloud(ICloudController controller)
        {
            if (RefreshingControllers.Any(c => c.Type == controller.Type && c.CloudStamp == controller.CloudStamp))
                return false;
            RefreshingControllers.Add(controller);
            return true;
        }

        private void UpdateAudioBookWithClouds(AudioBookSourceWithClouds dest, AudioBookSourceWithClouds source)
        {
            dest.Position = source.Position;
            dest.Images = source.Images;
            dest.CurrentFile = source.CurrentFile;
            dest.ExternalLinks = source.ExternalLinks;
            dest.IsLocked = source.IsLocked;
            dest.PlaybackRate = source.PlaybackRate;
            dest.Files = MergeFilesLists(source.Files, dest.Files);
            dest.ModifiDateTimeUtc = source.ModifiDateTimeUtc;
            dest.CreationDateTimeUtc = source.CreationDateTimeUtc;
        }

        async Task RefreshCloudData(ICloudController controller)
        {
            if (!AddRefreshCloud(controller))
                return;
            try
            {
                List<Task> tasks = new List<Task>();
                drFolders = (await controller.GetAudioBooksInfo()).ToArray();
                var oldFolders =
                    Folders.OfType<AudioBookSourceCloud>().Where(x => x.CloudStamp == controller.CloudStamp).ToList();
                foreach (var old in oldFolders)
                    Folders.Remove(old);
                AddSources(drFolders);
                Folders.Except(drFolders, new AudioBookWithCloudEqualityComparer()).Select(x =>
                {
                    x.IsHaveDropBox = false;
                    return x;
                }).ToList();
                List<AudioBookSourceWithClouds> onlyDrBox;
                onlyDrBox = drFolders.Except(Folders, new AudioBookWithCloudEqualityComparer()).ToList();
                await Task.WhenAll(tasks);
            }
            finally
            {
                RefreshingControllers.Remove(controller);
                await UpdateCurrentState();
                OnPropertyChanged(nameof(IsNothingFound));
            }
        }

        public ICloudController[] GetDownloadController(AudioBookSourceWithClouds book)
        {
            if (book == null)
                return new ICloudController[0];
            return manageSources.GetControllersForDownload(book, ControllersService1.Controllers);
        }

        public ICloudController[] GetuploadControllers(AudioBookSourceWithClouds book)
        {
            if (book == null)
                return new ICloudController[0];
            return manageSources.GetControllersForUpload(book, ControllersService1.Controllers);
        }

        public bool CheckingBaseFolder { get; set; }
        async Task CheckBaseFolder()
        {
            if (string.IsNullOrWhiteSpace(baseFolder?.AccessToken))
                return;
            CheckingBaseFolder = true;
            try
            {
                var dir = await StorageApplicationPermissions.FutureAccessList.GetFolderAsync(baseFolder.AccessToken);

                var localBookFolders = await dir.GetFoldersAsync();
                foreach (var folder in localBookFolders)
                {
                    if (!LocalFolders.Any(f => f.Folder == folder.Name))
                    {
                        string accessToken = StorageApplicationPermissions.FutureAccessList.Add(folder);
                        AddPlaySource(folder.Name, accessToken);
                        continue;
                    }
                    var fold =
                        await StorageApplicationPermissions.FutureAccessList.GetFolderAsync(baseFolder.AccessToken);
                    var alreadyBook = LocalFolders.First(f => f.Folder == folder.Name);
                    if (folder.Path == Path.Combine(fold.Path, alreadyBook.Path))
                    {
                        var source =
                            await
                                factory.GetFromLocalFolderAsync(folder.Path, alreadyBook.AccessToken,
                                    new Progress<Tuple<int, int>>(
                                        tuple => { }));
                        if (source?.Files == null || !source.Files.Any())
                        {
                            Folders.Remove(alreadyBook);
                            if (!string.IsNullOrWhiteSpace(alreadyBook.AccessToken))
                                StorageApplicationPermissions.FutureAccessList.Remove(alreadyBook.AccessToken);
                            continue;
                        }
                        bool hasDeletion = false;
                        bool changed = false;
                        for (int i = 0; i < alreadyBook.Files.Count; i++)
                        {
                            if (source.Files.Any(x => x.Name == alreadyBook.Files[i].Name))
                            {
                                if (!changed && !alreadyBook.Files[i].IsAvalible)
                                    changed = true;
                                alreadyBook.Files[i].IsAvalible = true;
                            }
                            else
                            {
                                if (!changed && alreadyBook.Files[i].IsAvalible)
                                    changed = true;
                                alreadyBook.Files[i].IsAvalible = false;
                            }
                        }
                        var added = source.Files.Except(alreadyBook.Files, new AudioBookFileEqualityComparer()).ToList();
                        if (added.Any())
                        {
                            alreadyBook.Files.AddRange(added);
                            factory.ReorderFiles(alreadyBook);
                            changed = true;
                        }
                        alreadyBook.TotalDuration = source.TotalDuration;
                        if (changed)
                            alreadyBook.UpdateModifyDateTime();
                    }
                    else
                    {
                        string accessToken = StorageApplicationPermissions.FutureAccessList.Add(folder);

                        var source =
                            await
                                factory.GetFromLocalFolderAsync(folder.Name, accessToken, new Progress<Tuple<int, int>>(
                                    tuple => { }));
                        if (source?.Files == null || !source.Files.Any())
                        {
                            Folders.Remove(alreadyBook);
                            StorageApplicationPermissions.FutureAccessList.Remove(accessToken);
                        }
                        alreadyBook.Files = source.Files;
                        alreadyBook.Path = source.Path;
                        alreadyBook.AccessToken = accessToken;
                        alreadyBook.TotalDuration = source.TotalDuration;
                        alreadyBook.UpdateModifyDateTime();
                    }
                }
            }
            finally
            {
                CheckingBaseFolder = false;
            }
        }

        public async Task SaveData()
        {
            if (PlayingSource != null)
                foreach (var cloudService in ControllersService1.Clouds)
                    await cloudService.UploadBookMetadata(PlayingSource);

            var books = new SaveModel
            {
                AudioBooks =
                    Folders.Where(
                        x => !string.IsNullOrWhiteSpace(x.AccessToken) &&
                             x.GetType() == typeof(AudioBookSourceWithClouds)).ToArray(),
                OnlineBooks =
                    Folders.Concat(Folders.SelectMany(x => x.AdditionSources)).OfType<OnlineAudioBookSource>().ToArray(),
                CurrentState = CurrentState,
                BaseFolder = baseFolder
            };

            books.CloudServices = ControllersService1.GetDataToSave();

            await repository.Save(books);
        }

        public async void AddPlaySource(string folder, string token)
        {
            if (LocalFolders.Any(f => f.Path == folder))
                return;
            UploadProgress operation = new UploadProgress()
            {
                OperationId = Guid.NewGuid(),
                BookName = folder,
                Status = "Adding",
                OperationStatus = OperationStatus.Started
            };
            var reporter = OperationService.GetIProgressReporter();
            reporter.Report(operation);
            try
            {
                var source = await factory.GetFromLocalFolderAsync(folder, token, new Progress<Tuple<int, int>>(
                    tuple =>
                    {
                        operation.Value = tuple.Item1;
                        operation.MaximumValue = tuple.Item2;
                    }));
                if (source == null)
                    return;
                Folders.Add(source);
            }
            finally
            {
                operation.OperationStatus = OperationStatus.Ended;
                reporter.Report(operation);
                OnPropertyChanged(nameof(IsNothingFound));
            }
            //foreach (var cloud in CloudControllers)
            //    await RefreshCloudData(cloud);
        }

        public async void AddObervingSource(string folder, string token)
        {
        }

        public async void RemoveSource(AudioBookSource source)
        {
            if (source == null)
                return;
            if (source is AudioBookSourceCloud && !(source is OnlineAudioBookSource))
            {
                if (settings.AskBeforeDeletionBook)
                {
                    var result = await notificator.ShowMessage("", "Are really want to delete?", ActionButtons.Cancel, ActionButtons.Ok);
                    if (result == ActionButtons.Cancel)
                        return;
                    foreach (var cloud in ControllersService1.Clouds)
                        await cloud.DeleteAudioBook(source);
                }
                return;
            }
            var contains = Folders.FirstOrDefault(f => f.Name == source.Name);
            if (contains == null)
                return;
            Folders.Remove(contains);
            try
            {
                if (
                    await
                        notificator.ShowMessage("", "Are you want to delete this book from disk?",
                            ActionButtons.Cancel, ActionButtons.Ok) == ActionButtons.Ok)
                    await factory.RemoveSource(contains);
                else
                    StorageApplicationPermissions.FutureAccessList.Remove(contains.AccessToken);
            }
            catch (ArgumentException e)
            {
                if (StorageApplicationPermissions.FutureAccessList.ContainsItem(contains.AccessToken))
                    throw;
            }
            contains.AccessToken = null;
            if (contains.Name == CurrentState.BookName)
            {
                State = MediaPlaybackFlow.Stop;
                Stop();
            }

            if (
                await
                    notificator.ShowMessage("", "Do you want to delete this book from clouds?",
                        ActionButtons.Cancel, ActionButtons.Ok) == ActionButtons.Ok)
                foreach (var cloud in ControllersService1.Clouds)
                    await cloud.DeleteAudioBook(contains);
        }

        public BookMark[] BookMarksForSelectedPlayingBook => repository.BookMarks(PlayingSource);

        private object _image;
        public object Image
        {
            get
            {
                if (_image != null && (_image as IRandomAccessStream)?.Size > 0)
                    return _image;
                return Settings.StandartCover;
            }
            set
            {
                _image = value;
            }
        }
        public RelayCommand<RemoteSystem> StreamToDeviceCommand { get; private set; }

        public INotification Notificator
        {
            get { return notificator; }
        }

        public bool IsOperationListOpen { get; set; }

        public OperationsService OperationService
        {
            get { return operationService; }
        }

        public ControllersService ControllersService1
        {
            get { return _controllersService; }
        }

        public async void Play()
        {
            if (PlayingSource != null)
            {
                if (CurrentState.BookName == PlayingSource.Name)
                {
                    if (PlayingFile == PlayingSource.GetCurrentFile)
                    {
                        player.Play();
                        State = MediaPlaybackFlow.Play;

                        if (PlayingSource.Cover.IsValide)
                            Image = (await PlayingSource.GetFileStream(PlayingSource.Cover.Url)).Item2;
                    }
                    else
                    {
                        player.Pause();
                        await SetSource(PlayingSource);
                        Play(PlayingFile.Name);
                    }
                }
                else if (PlayingSource.Files.Any())
                {
                    CurrentState.BookName = PlayingSource.Name;
                    player.Pause();
                    await SetSource(PlayingSource);
                    Play(PlayingFile.Name);
                }
                ControlStateChanged();

                PlayBackRate = PlayingSource.PlaybackRate;
            }
            else
            {
                player.Pause();
            }
        }

        private void ControlStateChanged()
        {
            NextCommand.RaiseCanExecuteChanged();
            PrevCommand.RaiseCanExecuteChanged();
            MoveSecsCommand.RaiseCanExecuteChanged();
            PauseCommand.RaiseCanExecuteChanged();
            PlayCommand.RaiseCanExecuteChanged();
        }

        private async Task SetSource(string file, TimeSpan position, AudioBookSource book = null)
        {
            _isOpening = true;
            try
            {
                book = book ?? PlayingSource;
                if (book is OnlineAudioBookSource)
                {
                    var f = book.Files.First(x => x.Name == file);

                    player.Source = new MediaPlaybackItem(MediaSource.CreateFromUri(new Uri(f.Path)));
                }
                else if (book is AudioBookSourceCloud)
                {
                    var cloud = (AudioBookSourceCloud)book;
                    if (!book.AvalibleFiles.Any(x => x.Name == file))
                        return;
                    var task = ControllersService1.GetController(cloud.CloudStamp)?.GetLink(book.Folder, file);
                    if (task == null)
                        return;

                    var link =
                        await task;
                    if (string.IsNullOrWhiteSpace(link))
                        return;
                    var source = MediaSource.CreateFromUri(new Uri(link));
                    source.StateChanged += SourceOnStateChanged;
                    player.Source = new MediaPlaybackItem(source);
                }
                else
                {
                    var stream = await book.GetFileStream(file);
                    var playBackItem = new MediaPlaybackItem(MediaSource.CreateFromStream(stream.Item2, stream.Item1));
                    player.Source = playBackItem;
                }
            }
            catch (Exception e)
            {
                _isOpening = false;
                throw;
            }

        }

        private void SourceOnStateChanged(MediaSource sender, MediaSourceStateChangedEventArgs args)
        {

        }

        TimeSpan temp = TimeSpan.MinValue;
        //private double playRateTemp = 1;
        private AudioBookSourceWithClouds _playingSource;
        private RemoteDevicesService _remoteDeviceService;


        private async Task SetSource(AudioBookSourceWithClouds book)
        {
            PlayingSource = book;
            if (book == null)
            {
                PlayingFile = null;
                return;
            }
            //PlayBackRate = book.PlaybackRate;
            PlayingFile = book.GetCurrentFile;
            temp = book.Position;
            if (PlayingFile == null)
                return;

            Debug.WriteLine($"SetSource positin is {book.Position}");
            await SetSource(PlayingFile.Name, book.Position);
            player.PlaybackSession.Position = book.Position;
            //player.MediaOpened -= afterOpened;
            if (PlayingSource.Cover.IsValide)
                Image = (await PlayingSource.GetFileStream(PlayingSource.Cover.Url)).Item2;
        }

        public async void Play(string file)
        {
            //await SetSource(file);
            player.Play();
            State = MediaPlaybackFlow.Play;
        }

        public void Pause()
        {
            try
            {
                if (player.PlaybackSession.CanPause && PlayingSource != null)
                {
                    Debug.WriteLine($"Pause position is {player.Position}");
                    PlayingSource.Position = player.PlaybackSession.Position;
                    State = MediaPlaybackFlow.Pause;
                    player.Pause();
                    if (PlayingSource == null)
                        return;
                    PlayingSource?.AddHistory(PlayBackHistoryElement.HistoryType.Pause);
                    foreach (var cloud in ControllersService1.Clouds)
                        cloud.UploadBookMetadata(PlayingSource);
                    //drbController.UploadBookMetadata(SelectedFolder);
                    //odController.UploadBookMetadata(SelectedFolder);
                }
            }
            catch (Exception e)
            {
                Debug.WriteLine($"Exception: {e.Message}\n{e.StackTrace}");
            }
        }

        public async void Stop()
        {
            player.Pause();
            State = MediaPlaybackFlow.Stop;
            await SetSource(null);
            CurrentState.BookName = null;
            ControlStateChanged();
            if (PlayingSource != null)
                foreach (var cloud in ControllersService1.Clouds)
                    cloud.UploadBookMetadata(PlayingSource);
        }

        private async Task PlayFile(AudiBookFile nextFile)
        {
            if (nextFile == null)
            {
                player.Pause();
                ControlStateChanged();
                return;
            }
            player.Pause();
            if (!nextFile.IsAvalible)
            {
                var cloudsFolder = PlayingSource.AdditionSources.FirstOrDefault(
                    s => s.AvalibleFiles.Any(x => x.Name == nextFile.Name && x.Order == nextFile.Order));
                var fiel = cloudsFolder?.AvalibleFiles.FirstOrDefault(f => f.Name == nextFile.Name);
                if (fiel != null)
                {
                    PlayingFile = fiel;
                    PlayingSource.Position = TimeSpan.Zero;
                    player.PlaybackSession.Position = TimeSpan.Zero;
                    await SetSource(PlayingFile.Name, PlayingSource.Position, cloudsFolder);
                    player.Play();

                }
                else
                    notificator.ShowMessage("File not avalible", "File not avalible");

            }
            else
            {

                PlayingFile = nextFile;

                PlayingSource.Position = TimeSpan.Zero;
                player.PlaybackSession.Position = TimeSpan.Zero;
                await SetSource(PlayingFile.Name, PlayingSource.Position);
                player.Play();
            }
        }

        private async void NextFile()
        {
            if (PlayingSource.CurrentFile + 1 < PlayingSource.Files.Count)
            {
                PlayingSource.CurrentFile++;
                var nextFile = PlayingSource.GetCurrentFile;
                await PlayFile(nextFile);
            }
            ControlStateChanged();
        }

        private async void PrevFile()
        {
            if (PlayingSource.CurrentFile - 1 >= 0)
            {
                PlayingSource.CurrentFile--;
                var nextFile = PlayingSource.GetCurrentFile;
                await PlayFile(nextFile);
            }
            ControlStateChanged();
        }

        public async void MoveSecs(int secs)
        {
            if (CurrentState == null)
                return;
            PlayingSource.Position = PlayingSource.Position.Add(TimeSpan.FromSeconds(secs));
            ControlStateChanged();
        }

        private async void RemoveCloudAccountAsync(ICloudController obj)
        {
            ControllersService1.RemoveController(obj);
            await SaveData();
        }

        public async void AddDropBoxAccount(CloudType type)
        {
            if (ControllersService1.IsExist(type))
            {
                if (
                    await
                        notificator.ShowMessage("Already have this type",
                            "You already added this type of cloud service. Are want add another?",
                            ActionButtons.Cancel, ActionButtons.Ok) != ActionButtons.Ok)
                    return;
            }
            var cloudService = await ControllersService1.AddNewController(type);
            if (cloudService == null)
                return;
            await SaveData();
            await RefreshCloudData(cloudService);
        }

        public void AddOneDriveAccount()
        {

        }

        public async void AddBaseFolder(string folder, string accessToken)
        {
            baseFolder = new Folder() { Path = folder, AccessToken = accessToken };

            await CheckBaseFolder();
            OnPropertyChanged(nameof(IsNothingFound));
            foreach (var cloud in ControllersService1.GetOnlyClouds())
                await RefreshCloudData(cloud);

            await SaveData();
        }

        protected virtual void OnShowBookDetails(AudioBookSourcesCombined e)
        {
            ShowBookDetails?.Invoke(this, e);
        }

        public event EventHandler<Tuple<Uri, Action<Uri>>> NavigateToAuthPage;

        protected virtual void OnNavigateToAuthPage(Tuple<Uri, Action<Uri>> e)
        {
            NavigateToAuthPage?.Invoke(this, e);
        }

        public event EventHandler CloseAuthPage;

        protected virtual void OnCloseAuthPage()
        {
            CloseAuthPage?.Invoke(this, EventArgs.Empty);
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual async void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            await Windows.ApplicationModel.Core.CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(
                CoreDispatcherPriority.Normal,
                () =>
                {
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
                });
        }


        //        public ICloudController[] GetAvalibleCloudControllers(AudioBookSourceWithClouds source)
        //        {
        //            if (source == null)
        //                return null;
        //            //source.AdditionSources.Where(s=> s.)
        //        }
        public async Task StartPlaySource(string remove)
        {
            if (string.IsNullOrWhiteSpace(remove))
                return;
            var param = remove.Split(';');
            if (!param.Any())
                return;

            if (param[0] == "1")
            {
                var parameters = GetOpenParams(param[1]);
                await SetPlayingSource(parameters);
            }
        }

        public async void StartPlaySource(AudioBookSourceWithClouds source)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));

            if (!Folders.Any(x => x.Name == source.Name))
                Folders.Add(source);

            await SaveData();

            await SetSource(source);
            Play();
        }

        RemoteOpenParams GetOpenParams(string json)
        {
            var obj = JsonConvert.DeserializeObject<RemoteOpenParams>(json);
            return obj;
        }

        async Task SetPlayingSource(RemoteOpenParams param)
        {
            var source = Folders.FirstOrDefault(x => x.Name == param.Name);
            if (source != null)
            {
                var file = source.AvalibleFiles.FirstOrDefault(f => f.Name == param.FileName);
                await SetSource(source);
                CurrentPosition = param.PositionInFile;
                source.CurrentFile = (int)file.Order;
                PlayingSource.SetPosition(param.PositionInFile);
                PlayingSource.PlaybackRate = param.PlayBackSpeed;
                Play();
            }
        }

        public Task<Stream> DownloadFileFromBook(AudioBookSource source, string file)
        {
            var controller = factory.SelectContorller(source, ControllersService1.Controllers);
            if (controller == null)
                return Task.FromResult(default(Stream));
            return controller.DownloadBookFile(source.Name, file);
        }
    }


    public class myRandmStream : IRandomAccessStream
    {
        private IRandomAccessStream baseStream;

        public myRandmStream(IRandomAccessStream stream)
        {
            baseStream = stream;
        }
        public void Dispose()
        {
            baseStream.Dispose();
        }

        public IAsyncOperationWithProgress<IBuffer, uint> ReadAsync(IBuffer buffer, uint count, InputStreamOptions options)
        {
            var res = baseStream.ReadAsync(buffer, count, options);
            return res;
        }

        public IAsyncOperationWithProgress<uint, uint> WriteAsync(IBuffer buffer)
        {
            var res = baseStream.WriteAsync(buffer);
            return res;
        }

        public IAsyncOperation<bool> FlushAsync()
        {
            return baseStream.FlushAsync();
        }

        public IInputStream GetInputStreamAt(ulong position)
        {
            var stream = baseStream.GetInputStreamAt(position);
            return stream;
        }

        public IOutputStream GetOutputStreamAt(ulong position)
        {
            var res = baseStream.GetOutputStreamAt(position);
            return res;
        }

        public void Seek(ulong position)
        {
            baseStream.Seek(position);
        }

        public IRandomAccessStream CloneStream()
        {
            return baseStream.CloneStream();
        }

        public bool CanRead => baseStream.CanRead;
        public bool CanWrite => baseStream.CanWrite;
        public ulong Position => baseStream.Position;

        public ulong Size
        {
            get { return baseStream.Size; }
            set { baseStream.Size = value; }
        }
    }
}


