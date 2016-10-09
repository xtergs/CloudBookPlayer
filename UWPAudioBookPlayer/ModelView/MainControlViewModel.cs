﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Runtime.CompilerServices;
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
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Media;
using GalaSoft.MvvmLight.Command;
using PropertyChanged;
using SQLite.Net.Attributes;
using UWPAudioBookPlayer.Comparer;
using UWPAudioBookPlayer.DAL;
using UWPAudioBookPlayer.DAL.Model;
using UWPAudioBookPlayer.Model;
using UWPAudioBookPlayer.Helper;
using UWPAudioBookPlayer.Service;

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
    }

    public class AudioBookSourcesCombined
    {
        public AudioBookSource MainSource { get; set; }
        public AudioBookSource Cloud { get; set; }
        public AudioBookSource[] Clouds { get; set; }
    }

    [ImplementPropertyChanged]
    public class MainControlViewModel : INotifyPropertyChanged
    {
        public delegate MainControlViewModel MainControlViewModelFactory(MediaElement mediaPlayer);

        private Folder baseFolder;
        private IDataRepository repository;
        //private DropBoxController drbController;
        //private ICloudController odController;
        private List<ICloudController> cloudControllers;
        private AudioBookSourceFactory factory;
        private readonly MediaPlayer player;
        private DAL.Model.CurrentState _currentState = new DAL.Model.CurrentState();
        private ISettingsService settings;
        private INotification notificator;

        public ObservableAudioBooksCollection<AudioBookSourceWithClouds> Folders { get; private set; }

        public ObservableCollection<ICloudController> RefreshingControllers { get; } =
            new ObservableCollection<ICloudController>();

        public AudioBookSourceWithClouds[] LocalFolders => Folders.Where(x => !(x is AudioBookSourceCloud)).ToArray();

        public ObservableCollection<UploadProgress> UploadOperations { get; set; } =
            new ObservableCollection<UploadProgress>();

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
                if (value == null)
                    _currentState = new DAL.Model.CurrentState();
                else
                    _currentState = value;
                PlayingSource = Folders.FirstOrDefault(f => f.Name == _currentState.BookName);
                if (PlayingSource != null && PlayingSource.CurrentFile >= 0)
                {
                    SetSource(PlayingSource);
                }
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
        //public RelayCommand UploadBookToDrBoxCommand { get; private set; }
        public RelayCommand<ICloudController> UploadBookToCloudCommand { get; private set; }
        public RelayCommand<Guid> CancelOperationCommand { get; private set; }
        public RelayCommand<Guid> PauseOperationCommand { get; private set; }
        public RelayCommand<Guid> ResumeOperationCommand { get; private set; }
        public RelayCommand DownloadBookFromDrBoxCommand { get; private set; }
        public RelayCommand<ICloudController> DownloadBookFromCloudCommand { get; private set; }
        public RelayCommand ShowBookDetailCommand { get; private set; }
        public RelayCommand ChangeTimerStateCommand { get; private set; }
        public RelayCommand<BookMark> AddBookMarkCommand { get; private set; }
        public RelayCommand<AudioBookSourceWithClouds> ResumePlayBookCommand { get; set; }
        public RelayCommand<PlayBackHistoryElement> PlayHistoryElementCommand { get; private set; }

        public RelayCommand<AudioBookSourceWithClouds> AddSourceToLibraryCommand { get; private set; }
        public RelayCommand RefreshCloudsCommand { get; private set; }


        public event EventHandler<AudioBookSourcesCombined> ShowBookDetails;

        //public DropBoxController DrbController
        //{
        //    get { return drbController; }
        //}

        public List<ICloudController> CloudControllers
        {
            get { return cloudControllers; }
        }

        public List<ICloudController> OnlyCloudControolers => cloudControllers.Where(x => x.IsCloud).ToList();

        public ISettingsService Settings
        {
            get { return settings; }
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

        public async void RefreshCloudData()
        {
            foreach (var cloud in OnlyCloudControolers)
                await RefreshCloudData(cloud);
        }

        public MainControlViewModel()
        {
            var mediaPlayer = new MediaPlayer()
            {
                AutoPlay = false,
            };
//            mediaPlayer.SetBinding(MediaElement.PlaybackRateProperty,
//                new Binding() {Path = new PropertyPath("PlayingSource.PlaybackRate"), Mode = BindingMode.OneWay});
//            mediaPlayer.SetBinding(MediaElement.PositionProperty, new Binding()
//            {
//                Path = new PropertyPath("PlayingSource.Position"),
//                Mode = BindingMode.TwoWay,
//                UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged
//            });

            var playbackBinding = new Binding()
            {
                Path = new PropertyPath("PlayingSource.PlaybackRate"),
                Source = this,
                Mode = BindingMode.OneWay
            };
            //BindingOperations.SetBinding(mediaPlayer, MediaElement.PlaybackRateProperty, playbackBinding);

            var positionBinding = new Binding
            {
                Path = new PropertyPath(nameof(PlayingSource.Position)),
                Mode = BindingMode.TwoWay,
                Source = this
            };

            //BindingOperations.SetBinding(mediaPlayer, MediaElement.PositionProperty, positionBinding);

            var fileDuratoinBinding = new Binding
                {
                    Path = new PropertyPath(nameof(FileDuration)),
                    Mode = BindingMode.OneWay,
                    Source = this,
                };
            //BindingOperations.SetBinding(mediaPlayer, MediaElement.NaturalDurationProperty, fileDuratoinBinding);
            if (mediaPlayer == null)
                throw new ArgumentNullException(nameof(mediaPlayer));
            this.player = mediaPlayer;
//            if (settings == null)
//                throw new ArgumentException(nameof(settings));
//            this.settings = settings;
            factory = new AudioBookSourceFactory();
            Folders = new ObservableAudioBooksCollection<AudioBookSourceWithClouds>();
            repository = new JSonRepository();
            ApplicationData.Current.DataChanged += CurrentOnDataChanged;
            //drbController = new DropBoxController();
            //odController = new OneDriveController();
            cloudControllers = new List<ICloudController>();
            notificator = new UniversalNotification();

            PlayCommand = new RelayCommand(Play);
            PauseCommand = new RelayCommand(Pause);
            NextCommand = new RelayCommand(NextFile,
                () => PlayingSource != null && PlayingSource.CurrentFile + 1 < PlayingSource.Files.Count());
            PrevCommand = new RelayCommand(PrevFile, () => PlayingSource != null && PlayingSource.CurrentFile - 1 >= 0);
            MoveSecsCommand = new RelayCommand<int>(MoveSecs, (c) => PlayingSource != null);
            RemoveAudioBookSource = new RelayCommand<AudioBookSource>(RemoveSource);
            AddCloudAccountCommand = new RelayCommand<CloudType>(AddDropBoxAccount);
            //UploadBookToDrBoxCommand = new RelayCommand(UploadBookToDrBox, () => drbController.IsAutorized && !string.IsNullOrWhiteSpace( SelectedFolder?.AccessToken));
            UploadBookToCloudCommand = new RelayCommand<ICloudController>(UploadBookToCloud, controller => controller != null && controller.IsAutorized && !string.IsNullOrWhiteSpace(SelectedFolder?.AccessToken));
            //DownloadBookFromDrBoxCommand = new RelayCommand(DownloadBookFromDrBok, () => drbController.IsAutorized);
            DownloadBookFromCloudCommand = new RelayCommand<ICloudController>(DownloadBookFromCloud,
                controller => controller != null && controller.IsAutorized);
            ShowBookDetailCommand = new RelayCommand(ShowBookDetailExecute);

            CancelOperationCommand = new RelayCommand<Guid>(CancelOperation);
            PauseOperationCommand = new RelayCommand<Guid>(PauseOperation);
            ResumeOperationCommand = new RelayCommand<Guid>(ResumeOperation);

            ChangeTimerStateCommand = new RelayCommand(ChangeTimerState);
            ResumePlayBookCommand = new RelayCommand<AudioBookSourceWithClouds>(ResumePlayBook);
            PlayHistoryElementCommand = new RelayCommand<PlayBackHistoryElement>(PlayHistoryElement, x => x != null);
            AddBookMarkCommand = new RelayCommand<BookMark>(AddBookMark);
            OnPropertyChanged(nameof(AddBookMarkCommand));
            AddSourceToLibraryCommand = new RelayCommand<AudioBookSourceWithClouds>(AddSourceToLibrary);
            RefreshCloudsCommand = new RelayCommand(RefreshCloudData);

            player.CurrentStateChanged += PlayerOnCurrentStateChanged;
            player.PlaybackSession.PositionChanged += PlaybackSessionOnPositionChanged;
            player.MediaOpened += PlayerOnMediaOpened;
            player.MediaPlayerRateChanged += PlayerOnMediaPlayerRateChanged;
            player.MediaEnded += PlayerOnMediaEnded;
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
            repository.AddBookMark(PlayingSource, obj);
            if (obj.IsRange)
            {
                CutAndSaveBookMarkFile(PlayingSource, obj.FileName, obj);
            }
            OnPropertyChanged(nameof(BookMarksForSelectedPlayingBook));
        }

        private async void CutAndSaveBookMarkFile(AudioBookSourceWithClouds book, string fileName, BookMark bookmark)
        {
            var trimOperation = new UploadProgress()
            {
                BookName = fileName,
                OperationId = Guid.NewGuid(),
                Type = CloudType.Local,
                Status = "Trimming..."
            };

            UploadOperations.Add(trimOperation);

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

                string bookMarkFileName = $"{bookmark.Order}_{bookmark.Title}{Path.GetExtension(fileName)}";

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
                    await
                        fileToWrite.RenameAsync(Path.GetFileNameWithoutExtension(fileToWrite.Name) + ".wav",
                            NameCollisionOption.ReplaceExisting);
                    var encoding = MediaEncodingProfile.CreateWav(AudioEncodingQuality.High);
                    composition.Clips.Clear();
                    composition = null;
                    composition = new MediaComposition();
                    composition.Clips.Add(clip);
                    var result =
                        await
                            composition.RenderToFileAsync(fileToWrite, MediaTrimmingPreference.Precise,encoding);

                    if (result != Windows.Media.Transcoding.TranscodeFailureReason.None)
                    {
                        Debug.WriteLine("Trying to trim file");
                        Debug.WriteLine(result.ToString());
                        await notificator.ShowMessage("", $"Occured error trimming file {fileName}");
                    }

                }
            }
            finally
            {
                UploadOperations.Remove(trimOperation);
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
            PlayingSource = audioBookSourceDetailWithCloud;
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
                        ActionButtons.Cancel | ActionButtons.Continue, 5*1000);
            if (res == ActionButtons.Continue)
            {
                SetTimer();
                return;
            }
            else
                Pause();
        }


        private async void ShowBookDetailExecute()
        {
            var ev = new AudioBookSourcesCombined() {MainSource = SelectedFolder};
            if (CloudControllers.Any(x => x.IsAutorized))
            {
                List<AudioBookSource> sources = new List<AudioBookSource>();
                foreach (var cloud in CloudControllers.Where(x => x.IsAutorized).ToList())
                    sources.Add(await cloud.GetAudioBookInfo(SelectedFolder));
                ev.Clouds = sources.ToArray();
            }
            OnShowBookDetails(ev);
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
                await DownloadBookFromOnline(SelectedFolder as OnlineAudioBookSource);
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
            UploadProgress progress = new UploadProgress()
            {
                BookName = tempSelectedFolder.Name,
                MaximumValue = tempSelectedFolder.Files.Count(),
                OperationId = Guid.NewGuid(),
                Status = "Downloading",
                Type = cloudControllser.Type
            };
            UploadOperations.Add(progress);
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

                if (originalSelectedFolder == null)
                {
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

                var alreadyInFolder =
                    (await bookFolder.GetFilesAsync()).ToList();
                int readed = 0;
                byte[] buffer = new byte[1024*4*4];
                var avalibleFiles = tempSelectedFolder.AvalibleFiles;
                for (int i = 0; i < tempSelectedFolder.AvalibleCount; i++)
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
                        if (property.Size == avalibleFiles[i].Size)
                            continue;
                    }
                    var index = i;
                    await Task.Run(async () =>
                    {
                        var stream =
                            await
                                cloudControllser.DownloadBookFile(tempSelectedFolder.Folder,
                                    avalibleFiles[index].Name);
                        // somehow we didn't found a file
                        if (stream == null)
                            return;
                        var file =
                            await
                                bookFolder.CreateFileAsync(avalibleFiles[index].Name,
                                    CreationCollisionOption.ReplaceExisting);

                        using (var random = await file.OpenTransactedWriteAsync())
                        {
                            uint written = 0;
                            while ((readed = stream.Read(buffer, 0, buffer.Length)) >= 0 &&
                                   written < avalibleFiles[index].Size)
                            {
                                random.Stream.AsStreamForWrite().Write(buffer, 0, readed);
                                written += (uint) readed;
                            }
                            await random.Stream.FlushAsync();
                            await random.CommitAsync();
                        }
                        originalSelectedFolder.Files.Add(new AudiBookFile()
                        {
                            Duration = avalibleFiles[index].Duration,
                            IsAvalible = true,
                            Name = avalibleFiles[index].Name,
                            Order = avalibleFiles[index].Order,
                            Size = (await file.GetBasicPropertiesAsync()).Size
                        });
                    });
                }
                originalSelectedFolder.CreationDateTimeUtc = tempSelectedFolder.CreationDateTimeUtc;
                originalSelectedFolder.ModifiDateTimeUtc = tempSelectedFolder.ModifiDateTimeUtc;
            }
            finally
            {
                UploadOperations.Remove(progress);
            }
        }


        private async Task DownloadBookFromOnline(OnlineAudioBookSource source)
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
                Type = CloudType.Online
            };
            UploadOperations.Add(progress);
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
                     TotalDuration =  tempSelectedFolder.TotalDuration
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
                byte[] buffer = new byte[1024*4*4];
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
                                    written += (uint) readed;
                                }
                                await random.Stream.FlushAsync();
                                await random.CommitAsync();
                            }
                            var f = originalSelectedFolder.Files.First(x => x.Name == avalibleFiles[index].Name);
                            f.IsAvalible =true;
                            f.Size = (await file.GetBasicPropertiesAsync()).Size;
                            //originalSelectedFolder.Files.Add(new AudiBookFile()
                            //{
                            //    Duration = avalibleFiles[index].Duration,
                            //    IsAvalible = true,
                            //    Name = avalibleFiles[index].Name,
                            //    Order = avalibleFiles[index].Order,
                            //    Size = (await file.GetBasicPropertiesAsync()).Size
                            //});
                        }
                    });
                }
                originalSelectedFolder.CreationDateTimeUtc = tempSelectedFolder.CreationDateTimeUtc;
                originalSelectedFolder.ModifiDateTimeUtc = tempSelectedFolder.ModifiDateTimeUtc;
            }
            finally
            {
                UploadOperations.Remove(progress);
            }
        }


        //private async void DownloadBookFromDrBok()
        //{
        //    DrownloadBookFromCloud(DrbController);
        //}

        private void ResumeOperation(Guid obj)
        {
            var operation = UploadOperations.FirstOrDefault(o => o.OperationId == obj);
            if (operation == null)
                return;
            operation.IsPaused = false;
        }

        private void PauseOperation(Guid obj)
        {
            var operation = UploadOperations.FirstOrDefault(o => o.OperationId == obj);
            if (operation == null)
                return;
            operation.IsPaused = true;
        }

        private void CancelOperation(Guid obj)
        {
            var operation = UploadOperations.FirstOrDefault(o => o.OperationId == obj);
            if (operation == null)
                return;
            operation.IsCancelled = true;
        }

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

        private async void UploadBookToCloud(ICloudController obj)
        {
            var selectedFolderTemp = SelectedFolder;
            if (selectedFolderTemp == null || (selectedFolderTemp as AudioBookSourceCloud)?.CloudStamp == obj.CloudStamp)
                return;
            if (!selectedFolderTemp.Files.Any() || !obj.IsCloud)
                return;
            if (UploadOperations.Any(x => x.BookName == selectedFolderTemp.Name && x.Type == obj.Type))
                return;
            var UploadOperation = new UploadProgress()
            {
                OperationId = Guid.NewGuid(),
                Status = "Uploading",
                BookName = selectedFolderTemp.Name,
                MaximumValue = selectedFolderTemp.Files.Count,
            };
            UploadOperations.Add(UploadOperation);
            try
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
                                    ActionButtons.Cancel | ActionButtons.Retry);
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
                await UploadBookmarksToCloud(obj, selectedFolderTemp);
            }
            finally
            {
                UploadOperations.Remove(UploadOperation);
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
                    }
                });
        }

        private void AddSources(AudioBookSourceWithClouds[] soures)
        {
            if (soures == null )
                return;
            foreach (var source in soures)
            {
                var inFolders = Folders.FirstOrDefault(x =>x.Name == source.Name) as
                        AudioBookSourceWithClouds;
                if (inFolders == null)
                {
                    if (source.AvalibleCount > 0)
                        Folders.Add(source);
                    continue;
                }

                var fromCloud = inFolders as AudioBookSourceCloud;
                //Same entry from cloud already persist
                if (fromCloud != null && fromCloud.CloudStamp == (source as AudioBookSourceCloud)?.CloudStamp)
                {
                    fromCloud.Files = MergeFilesLists(source.Files, fromCloud.Files);
                    continue;
                }
                var isCloud = source as AudioBookSourceCloud;
                if (isCloud != null)
                {
                    if (inFolders.AdditionSources.OfType<AudioBookSourceCloud>().Any(s=> s.CloudStamp == isCloud.CloudStamp && s.CreationDateTimeUtc == isCloud.CreationDateTimeUtc && s.ModifiDateTimeUtc == isCloud.ModifiDateTimeUtc))
                        continue;
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
                    UpdateAudioBookWithClouds( source, inFolders);
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
                    continue;
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
        }

        public bool LoadingData { get; private set; }

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
                CurrentState = loaded.CurrentState;
                //CloudControllers.Clear();
                //foreach (var cloudService in loaded.CloudServices)
                //    cloudControllers.Add(factory.GetCloudController(cloudService));
                //if (loaded.CloudServices.Any(x => x.Name == "DropBox"))
                //{
                //    drbController.Token = loaded.CloudServices[0].Token;

                //}
                //if (loaded.CloudServices.Any(x => x.Name == "OneDrive"))
                //    odController.Token = loaded.CloudServices.First(x => x.Name == "OneDrive").Token;
                cloudControllers.Clear();
                foreach (var service in loaded.CloudServices)
                {
                    var cloud = (factory.GetCloudController(service));
                    await cloud.Inicialize();
                    cloudControllers.Add(cloud);
                }
                cloudControllers.Add(new OnlineController("Librivox"));
                //drbController.Inicialize();
                //odController.Inicialize();
                baseFolder = loaded.BaseFolder;
                CheckBaseFolder();
                foreach (var cloud in cloudControllers.Where(x => x.Type != CloudType.Online).ToArray())
                    await RefreshCloudData(cloud);
                watch.Stop();
                Debug.WriteLine($"load data is {watch.ElapsedMilliseconds} ms");
            }
            finally
            {
                LoadingData = false;
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

        private AudioBookSourceCloud[] drFolders = new AudioBookSourceCloud[] {};

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
            dest.Cover = source.Cover;
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
//                foreach (var audioBookSourceWithCloudse in onlyDrBox.Where(x=> x.AvalibleCount > 0))
//                {
//                    Folders.Add(audioBookSourceWithCloudse);
//                }
                await Task.WhenAll(tasks);
                //UploadBookToDrBoxCommand.RaiseCanExecuteChanged();
                //DownloadBookFromDrBoxCommand.RaiseCanExecuteChanged();
            }
            finally
            {
                RefreshingControllers.Remove(controller);
            }
        }
        //async Task RefreshDropBoxData()
        //{
        //    if (DropBoxRefreshing)
        //        return;
        //    DropBoxRefreshing = true;
        //    try
        //    {
        //        await RefreshCloudData(drbController);
        //    }
        //    finally
        //    {
        //        DropBoxRefreshing = false;
        //    }
        //}

        async Task CheckBaseFolder()
        {
            if (string.IsNullOrWhiteSpace(baseFolder?.AccessToken))
                return;

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
                var fold = await StorageApplicationPermissions.FutureAccessList.GetFolderAsync(baseFolder.AccessToken);
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
                        await factory.GetFromLocalFolderAsync(folder.Name, accessToken, new Progress<Tuple<int, int>>(
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

        public async Task SaveData()
        {
            foreach(var cloudService in CloudControllers.Where(x=> x.IsAutorized && x.IsCloud))
                Folders.AsParallel().Select(async x =>
                        await cloudService.UploadBookMetadata(x));


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

            books.CloudServices =
                cloudControllers.Where(x => x.IsAutorized && x.IsCloud)
                    .Select(x => new CloudService() {Name = x.ToString(), Token = x.Token})
                    .ToArray();

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
            };
            UploadOperations.Add(operation);
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
                UploadOperations.Remove(operation);
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
                    var result = await notificator.ShowMessage("", "Are really want to delete?", ActionButtons.Cancel | ActionButtons.Ok);
                    if (result == ActionButtons.Cancel)
                        return;
                    foreach(var cloud in CloudControllers)
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
                            ActionButtons.Cancel | ActionButtons.Ok) == ActionButtons.Ok)
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
                        ActionButtons.Cancel | ActionButtons.Ok) == ActionButtons.Ok)
                foreach(var cloud in CloudControllers)
                    await cloud.DeleteAudioBook(contains);
        }

        public BookMark[] BookMarksForSelectedPlayingBook => repository.BookMarks(PlayingSource);

        public IRandomAccessStream Image { get; set; }

        public async void Play()
        {
            if (PlayingSource != null)
            {
                //CurrentState.BookName = SelectedFolder.Name;
                if (CurrentState.BookName == PlayingSource.Name)
                {
                    if (PlayingFile == PlayingSource.GetCurrentFile)
                    {
                            //player.Position = SelectedFolder.Position;
                            player.Play();
                            State = MediaPlaybackFlow.Play;
                            //player.PlaybackRate = PlayingSource.PlaybackRate;
                            //player.DefaultPlaybackRate = PlayingSource.PlaybackRate;
                            

                        Image = (await PlayingSource.GetFileStream(PlayingSource.Images?.FirstOrDefault() ?? PlayingSource.Cover)).Item2;
                    }
                    else
                    {
                        //SelectedFolder.CurrentFile = 0;
                        //SelectedFolder.Position = TimeSpan.Zero;
                        //PlayingFile = SelectedFolder.Files[SelectedFolder.CurrentFile];
                        //PlayingSource = SelectedFolder;
                        player.Pause();
                        await SetSource(PlayingSource);
                        Play(PlayingFile.Name);
                        Image = (await PlayingSource.GetFileStream(PlayingSource.Images?.FirstOrDefault() ?? PlayingSource.Cover)).Item2;
                    }
                }
                else if (PlayingSource.Files.Any())
                {
                    CurrentState.BookName = PlayingSource.Name;
                    //SelectedFolder.CurrentFile = 0;
                    //SelectedFolder.Position = TimeSpan.Zero;
                    //PlayingSource = SelectedFolder;
                    player.Pause();
                    await SetSource(PlayingSource);
                    //PlayingFile = SelectedFolder.Files.First();
                    Play(PlayingFile.Name);
                    //PlayingSource.AddHistory(PlayBackHistoryElement.HistoryType.Play);
                    Image = (await PlayingSource.GetFileStream(PlayingSource.Images?.FirstOrDefault() ?? PlayingSource.Cover)).Item2;
                }
                ControlStateChanged();
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
                    var cloud = (AudioBookSourceCloud) book;
                    if (!book.AvalibleFiles.Any(x => x.Name == file))
                        return;
                    var link =
                        await cloudControllers.First(x => x.CloudStamp == cloud.CloudStamp).GetLink(book.Folder, file);
                    if (string.IsNullOrWhiteSpace(link))
                        return;
                    player.Source = new MediaPlaybackItem(MediaSource.CreateFromUri(new Uri(link)));
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

        TimeSpan temp = TimeSpan.MinValue;
        //private double playRateTemp = 1;
        private AudioBookSourceWithClouds _playingSource;


        private async Task SetSource(AudioBookSource book)
        {
            PlayingFile = book.GetCurrentFile;
            temp = book.Position;
            if (PlayingFile == null)
                return;
            //playRateTemp = book.PlaybackRate;
//            var afterOpened = new TypedEventHandler<MediaPlayer, object>(delegate(MediaPlayer sender, object args)
//            {
//                if (temp == TimeSpan.MinValue)
//                    return;
//                book.Position = temp;
//                temp = TimeSpan.MinValue;
//                //book.PlaybackRate = playRateTemp;
//                player.Position = book.Position;
//                //player.PlaybackRate = book.PlaybackRate;
//                //player.DefaultPlaybackRate = book.PlaybackRate;
//
//            });
//            player.MediaOpened -= afterOpened;
            //player.MediaOpened += afterOpened;
            Debug.WriteLine($"SetSource positin is {book.Position}");
            await SetSource(PlayingFile.Name, book.Position);
            player.PlaybackSession.Position = book.Position;
            //player.MediaOpened -= afterOpened;
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
                    foreach (var cloud in CloudControllers.Where(x => x.IsCloud))
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

        public void Stop()
        {
            player.Pause();
            State = MediaPlaybackFlow.Stop;
            PlayingSource = null;
            CurrentState.BookName = null;
            ControlStateChanged();
            if (PlayingSource != null)
                foreach (var cloud in CloudControllers.Where(x => x.IsCloud))
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

        public async void AddDropBoxAccount(CloudType type)
        {
            if (CloudControllers.Any(x => x.Type == type))
            {
                if (
                    await
                        notificator.ShowMessage("Already have this type",
                            "You already added this type of cloud service. Are want add another?",
                            ActionButtons.Cancel | ActionButtons.Ok) != ActionButtons.Ok)
                    return;
            }

            var cloudService = factory.GetCloudController(type);
            cloudService.CloseAuthPage += async (sender, args) =>
            {
                cloudService.NavigateToAuthPage -= CloudServiceOnNavigateToAuthPage;
                cloudService.CloseAuthPage -= CloudServiceOnCloseAuthPage;
                if (!cloudService.IsAutorized)
                    return;
                if (CloudControllers.Any(x => x.CloudStamp == cloudService.CloudStamp))
                {
                    notificator.ShowMessage("You alread added this account", "You have already added this account");
                    return;
                }
                CloudControllers.Add(cloudService);
                await SaveData();
                await RefreshCloudData(cloudService);
            };
            if (cloudService.IsUseExternalBrowser)
            {
                cloudService.NavigateToAuthPage += CloudServiceOnNavigateToAuthPage;
                cloudService.CloseAuthPage += CloudServiceOnCloseAuthPage;
            }
            cloudService.Auth();


            //if (type == CloudType.DropBox)
            //    drbController.Auth();
            //if (type==CloudType.OneDrive)
            //    odController.Auth();
        }

        private void CloudServiceOnCloseAuthPage(object sender, EventArgs eventArgs)
        {
            OnCloseAuthPage();
        }

        private void CloudServiceOnNavigateToAuthPage(object sender, Tuple<Uri, Action<Uri>> tuple)
        {
            OnNavigateToAuthPage(tuple);
        }

        public void AddOneDriveAccount()
        {
            
        }

        public async void AddBaseFolder(string folder, string accessToken)
        {
            baseFolder = new Folder() {Path = folder, AccessToken = accessToken};

            await CheckBaseFolder();
            foreach (var cloud in cloudControllers.Where(x=>x.IsCloud))
                await RefreshCloudData(cloud);
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
                CoreDispatcherPriority.Low,
                () => {
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
                });
        }

        
//        public ICloudController[] GetAvalibleCloudControllers(AudioBookSourceWithClouds source)
//        {
//            if (source == null)
//                return null;
//            //source.AdditionSources.Where(s=> s.)
//        }
    }
}