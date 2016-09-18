using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using Windows.Storage;
using Windows.Storage.AccessCache;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
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
            return new MainControlViewModel(element, settings);
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
        Local,
        DropBox,
        OneDrive
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
    public class MainControlViewModel
    {
        public delegate MainControlViewModel MainControlViewModelFactory(MediaElement mediaPlayer);

        private Folder baseFolder;
        private IDataRepository repository;
        //private DropBoxController drbController;
        //private ICloudController odController;
        private List<ICloudController> cloudControllers;
        private AudioBookSourceFactory factory;
        private MediaElement player;
        private CurrentState _currentState = new CurrentState();
        private ISettingsService settings;
        private INotification notificator;

        public ObservableCollection<AudioBookSourceWithClouds> Folders { get; private set; }

        public ObservableCollection<ICloudController> RefreshingControllers { get; } =
            new ObservableCollection<ICloudController>();

        public AudioBookSourceWithClouds[] LocalFolders => Folders.Where(x => !(x is AudioBookSourceCloud)).ToArray();

        public ObservableCollection<UploadProgress> UploadOperations { get; set; } =
            new ObservableCollection<UploadProgress>();

        public AudioBookSourceWithClouds SelectedFolder { get; set; }

        public AudioBookSourceWithClouds PlayingSource
        {
            get { return _playingSource; }
            set { _playingSource = value; }
        }

        public AudiBookFile PlayingFile { get; private set; }

        public MediaPlaybackFlow State { get; set; } = MediaPlaybackFlow.Stop;

        public CurrentState CurrentState
        {
            get { return _currentState; }
            set
            {
                if (value == null)
                    _currentState = new CurrentState();
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


        public event EventHandler<AudioBookSourcesCombined> ShowBookDetails;

        //public DropBoxController DrbController
        //{
        //    get { return drbController; }
        //}

        public List<ICloudController> CloudControllers
        {
            get { return cloudControllers; }
        }

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


        public MainControlViewModel(MediaElement mediaPlayer, ISettingsService settings)
        {
            if (mediaPlayer == null)
                throw new ArgumentNullException(nameof(mediaPlayer));
            this.player = mediaPlayer;
            if (settings == null)
                throw new ArgumentException(nameof(settings));
            this.settings = settings;
            factory = new AudioBookSourceFactory();
            Folders = new ObservableCollection<AudioBookSourceWithClouds>();
            repository = new JSonRepository();
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

            player.CurrentStateChanged += PlayerOnCurrentStateChanged;
        }

        private void AddBookMark(BookMark obj)
        {
            PlayingSource.BookMarks.Add(obj);
        }

        private void PlayHistoryElement(PlayBackHistoryElement obj)
        {
            PlayingSource.CurrentFile =
                PlayingSource.Files.IndexOf(PlayingSource.Files.First(x => x.Name == obj.FileName));
            PlayingSource.Position = obj.Position;
            Play();
        }

        private void ResumePlayBook(AudioBookSourceWithClouds audioBookSourceDetailWithCloud)
        {
            Pause();
            PlayingSource = audioBookSourceDetailWithCloud;
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
                foreach (var cloud in CloudControllers.Where(x => x.IsAutorized))
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
            AudioBookSourceWithClouds tempSelectedFolder = SelectedFolder;
            AudioBookSourceWithClouds originalSelectedFolder = null;
            if (SelectedFolder is AudioBookSourceCloud)
            {
                if (string.IsNullOrWhiteSpace(baseFolder.AccessToken))
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

        private async void UploadBookToCloud(ICloudController obj)
        {
            var selectedFolderTemp = SelectedFolder;
            if (selectedFolderTemp == null)
                return;
            if (!selectedFolderTemp.Files.Any())
                return;
            if (UploadOperations.Any(x => x.BookName == selectedFolderTemp.Name))
                return;
            var UploadOperation = new UploadProgress()
            {
                OperationId = Guid.NewGuid(),
                Status = "Uploading",
                BookName = selectedFolderTemp.Name,
                MaximumValue = selectedFolderTemp.Files.Count
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

                    selectedFolderTemp.CountFilesDropBox = i + 1;
                    selectedFolderTemp.IsHaveDropBox = true;
                }
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

        private void PlayerOnCurrentStateChanged(object sender, RoutedEventArgs routedEventArgs)
        {
            switch (player.CurrentState)
            {
                case MediaElementState.Playing:
                    if (State == MediaPlaybackFlow.Play)
                        player.PlaybackRate = PlayingSource.PlaybackRate;
                        PlayingSource.AddHistory(PlayBackHistoryElement.HistoryType.Play);
                    break;


                case MediaElementState.Paused:
                    if (State == MediaPlaybackFlow.Play)
                        if (Math.Abs(player.Position.TotalSeconds - player.NaturalDuration.TimeSpan.TotalSeconds) < 1)
                            NextFile();
                        else
                            player.Play();
                    break;
            }
        }

        public async Task LoadData()
        {
            var loaded = await repository.Load();
            Folders = new ObservableCollection<AudioBookSourceWithClouds>(loaded.AudioBooks);
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
                cloud.Inicialize();
                cloudControllers.Add(cloud);
            }
            //drbController.Inicialize();
            //odController.Inicialize();
            baseFolder = loaded.BaseFolder;
            CheckBaseFolder();
            foreach(var cloud in cloudControllers)
                await RefreshCloudData(cloud);
        }

        List<AudiBookFile> MergeFilesLists(List<AudiBookFile> newList, List<AudiBookFile> oldList)
        {
            for (int i = 0; i < newList.Count; i++)
            {
                newList[i].IsAvalible = oldList.Any(x => x.Name == newList[i].Name);
            }
            return newList;
        }

        private List<AudioBookSourceCloud> drFolders = new List<AudioBookSourceCloud>(0);

        async Task RefreshCloudData(ICloudController controller)
        {
            RefreshingControllers.Add(controller);
            try
            {
                drFolders = (await controller.GetAudioBooksInfo()).Where(x => x.AvalibleCount > 0).ToList();
                var oldFolders =
                    Folders.OfType<AudioBookSourceCloud>().Where(x => x.CloudStamp == controller.CloudStamp).ToList();
                foreach (var old in oldFolders)
                    Folders.Remove(old);
                foreach (var f in drFolders)
                {
                    if (f.AvalibleCount <= 0)
                        continue;
                    var inFolders = Folders.FirstOrDefault(x => x.Folder == f.Folder) as AudioBookSourceWithClouds;
                    if (inFolders == null)
                        continue;

                    var old = inFolders.AdditionSources.OfType<AudioBookSourceCloud>()
                        .Where(s => s.CloudStamp == controller.CloudStamp)
                        .ToList();
                    foreach (var s in old)
                        inFolders.AdditionSources.Remove(s);

                    if (inFolders.CreationDateTimeUtc > f.CreationDateTimeUtc)
                    {
                        var tempLocal = inFolders;
                        inFolders = f;
                        inFolders.Path = tempLocal.Path;
                        inFolders.AccessToken = tempLocal.AccessToken;
                        inFolders.Files = MergeFilesLists(f.Files, tempLocal.Files);

                    }
                    else if (inFolders.CreationDateTimeUtc < f.CreationDateTimeUtc)
                    {
                        var tempLocal = f;
                        await controller.UploadBookMetadata(inFolders);
                    }
                    else 
                    if (inFolders.ModifiDateTimeUtc < f.ModifiDateTimeUtc)
                    {
                        var tempLocal = inFolders;
                        inFolders = f;
                        inFolders.Path = tempLocal.Path;
                        inFolders.AccessToken = tempLocal.AccessToken;
                        inFolders.Files = MergeFilesLists(f.Files, tempLocal.Files);
                    }
                    else if (inFolders.ModifiDateTimeUtc > f.ModifiDateTimeUtc)
                    {
                        var tempLocal = f;
                        //tempLocal.Files = MergeFilesLists(inFolders.Files, f.Files);
                        await controller.UploadBookMetadata(inFolders);
                    }
                    inFolders.AdditionSources.Add(f);
                }
                Folders.Except(drFolders, new AudioBookWithCloudEqualityComparer()).Select(x =>
                {
                    x.IsHaveDropBox = false;
                    return x;
                }).ToList();
                List<AudioBookSourceWithClouds> onlyDrBox;
                onlyDrBox = drFolders.Except(Folders, new AudioBookWithCloudEqualityComparer()).ToList();
                foreach (var audioBookSourceWithCloudse in onlyDrBox)
                {
                    Folders.Add(audioBookSourceWithCloudse);
                }
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
            foreach(var cloudService in CloudControllers.Where(x=> x.IsAutorized))
                Folders.AsParallel().Select(async x =>
                        await cloudService.UploadBookMetadata(x));
            

            var books = new SaveModel
            {
                AudioBooks =
                    Folders.Where(
                        x =>
                            !string.IsNullOrWhiteSpace(x.AccessToken) &&
                            x.GetType() == typeof(AudioBookSourceWithClouds)).ToArray(),
                CurrentState = CurrentState,
                BaseFolder = baseFolder
            };

            books.CloudServices =
                cloudControllers.Where(x => x.IsAutorized)
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
            foreach (var cloud in CloudControllers)
                await RefreshCloudData(cloud);
        }

        public async void AddObervingSource(string folder, string token)
        {
        }

        public async void RemoveSource(AudioBookSource source)
        {
            if (source == null)
                return;
            if (source is AudioBookSourceCloud)
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
            if (
                await
                    notificator.ShowMessage("", "Are you want to delete this book from disk?",
                        ActionButtons.Cancel | ActionButtons.Ok) == ActionButtons.Ok)
                await factory.RemoveSource(contains);
            else
                StorageApplicationPermissions.FutureAccessList.Remove(contains.AccessToken);
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

        public async void Play()
        {
            if (PlayingSource != null)
            {
                //CurrentState.BookName = SelectedFolder.Name;
                if (CurrentState.BookName == PlayingSource.Name)
                {
                    if (PlayingFile == PlayingSource.GetCurrentFile)
                    {
                        if (State != MediaPlaybackFlow.Play)
                        {
                            State = MediaPlaybackFlow.Play;
                            //player.Position = SelectedFolder.Position;
                            player.Play();
                            //player.PlaybackRate = PlayingSource.PlaybackRate;
                            //player.DefaultPlaybackRate = PlayingSource.PlaybackRate;
                            
                        }
                    }
                    else
                    {
                        //SelectedFolder.CurrentFile = 0;
                        //SelectedFolder.Position = TimeSpan.Zero;
                        //PlayingFile = SelectedFolder.Files[SelectedFolder.CurrentFile];
                        //PlayingSource = SelectedFolder;
                        player.Stop();
                        await SetSource(PlayingSource);
                        Play(PlayingFile.Name);
                        
                    }
                }
                else if (PlayingSource.Files.Any())
                {
                    CurrentState.BookName = PlayingSource.Name;
                    //SelectedFolder.CurrentFile = 0;
                    //SelectedFolder.Position = TimeSpan.Zero;
                    //PlayingSource = SelectedFolder;
                    player.Stop();
                    await SetSource(PlayingSource);
                    //PlayingFile = SelectedFolder.Files.First();
                    Play(PlayingFile.Name);
                    PlayingSource.AddHistory(PlayBackHistoryElement.HistoryType.Play);
                }
                ControlStateChanged();
            }
            else
            {
                player.Stop();
            }
        }

        private void ControlStateChanged()
        {
            NextCommand.RaiseCanExecuteChanged();
            PrevCommand.RaiseCanExecuteChanged();
            MoveSecsCommand.RaiseCanExecuteChanged();
            PauseCommand.RaiseCanExecuteChanged();
        }

        private async Task SetSource(string file, AudioBookSource book = null)
        {
            book = book ?? PlayingSource;
            if (book is AudioBookSourceCloud)
            {
                var cloud = (AudioBookSourceCloud) book;
                if (!book.AvalibleFiles.Any(x => x.Name == file))
                    return;
                var link = await cloudControllers.First(x=> x.CloudStamp == cloud.CloudStamp ).GetLink(book.Folder, file);
                if (string.IsNullOrWhiteSpace(link))
                    return;
                player.Source = new Uri(link);
            }
            else
            {
                var stream = await book.GetFileStream(file);
                player.SetSource(stream.Item2, stream.Item1);
            }
        }

        TimeSpan temp = TimeSpan.MinValue;
        //private double playRateTemp = 1;
        private AudioBookSourceWithClouds _playingSource;


        private async Task SetSource(AudioBookSource book)
        {
            PlayingFile = book.GetCurrentFile;
            temp = book.Position;
            //playRateTemp = book.PlaybackRate;
            var afterOpened = new RoutedEventHandler(delegate(object sender, RoutedEventArgs args)
            {
                if (temp == TimeSpan.MinValue)
                    return;
                book.Position = temp;
                temp = TimeSpan.MinValue;
                //book.PlaybackRate = playRateTemp;
                player.Position = book.Position;
                //player.PlaybackRate = book.PlaybackRate;
                //player.DefaultPlaybackRate = book.PlaybackRate;

            });
            player.MediaOpened -= afterOpened;
            player.MediaOpened += afterOpened;
            await SetSource(PlayingFile.Name);
            //player.MediaOpened -= afterOpened;
        }

        public async void Play(string file)
        {
            //await SetSource(file);
            State = MediaPlaybackFlow.Play;
            player.Play();
        }

        public void Pause()
        {
            if (player.CanPause)
            {
                State = MediaPlaybackFlow.Pause;
                player.Pause();
                PlayingSource.AddHistory(PlayBackHistoryElement.HistoryType.Pause);
                foreach (var cloud in CloudControllers)
                    cloud.UploadBookMetadata(PlayingSource);
                //drbController.UploadBookMetadata(SelectedFolder);
                //odController.UploadBookMetadata(SelectedFolder);
            }
        }

        public void Stop()
        {
            player.Stop();
            State = MediaPlaybackFlow.Stop;
            PlayingSource = null;
            CurrentState.BookName = null;
            ControlStateChanged();
            if (PlayingSource != null)
                foreach (var cloud in CloudControllers)
                    cloud.UploadBookMetadata(PlayingSource);
        }

        private async Task PlayFile(AudiBookFile nextFile)
        {
            if (nextFile == null)
            {
                player.Stop();
                ControlStateChanged();
                return;
            }
            player.Stop();
            if (!nextFile.IsAvalible)
            {
                var cloudsFolder = PlayingSource.AdditionSources.FirstOrDefault(
                    s => s.AvalibleFiles.Any(x => x.Name == nextFile.Name && x.Order == nextFile.Order));
                    var fiel = cloudsFolder?.AvalibleFiles.FirstOrDefault(f => f.Name == nextFile.Name);
                    if (fiel != null)
                    {
                        PlayingFile = fiel;
                        await SetSource(PlayingFile.Name, cloudsFolder);
                    PlayingSource.Position = TimeSpan.Zero;
                        player.Play();


                }

            }
            else
            {

                PlayingFile = nextFile;

                await SetSource(PlayingFile.Name);
                PlayingSource.Position = TimeSpan.Zero;
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
            foreach (var cloud in cloudControllers)
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
    }
}