using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
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
    }

    [ImplementPropertyChanged]
    public class MainControlViewModel
    {
        private Folder baseFolder;
        private IDataRepository repository;
        private DropBoxController drbController;
        private ICloudController odController;
        private AudioBookSourceFactory factory;
        private MediaElement player;
        private CurrentState _currentState = new CurrentState();
        private ISettingsService settings;
        private INotification notificator;

        public ObservableCollection<AudioBookSourceWithClouds> Folders { get; set; }

        public ObservableCollection<UploadProgress> UploadOperations { get; set; } =
            new ObservableCollection<UploadProgress>();

        public AudioBookSourceWithClouds SelectedFolder { get; set; }
        public AudioBookSourceWithClouds PlayingSource { get; set; }
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
                SelectedFolder = Folders.FirstOrDefault(f => f.Name == _currentState.BookName);
                if (SelectedFolder != null && SelectedFolder.CurrentFile >= 0)
                {
                    SetSource(SelectedFolder);
                }
            }
        }

        public bool DropBoxRefreshing { get; private set; }

        public RelayCommand PlayCommand { get; private set; }
        public RelayCommand PauseCommand { get; private set; }
        public RelayCommand NextCommand { get; private set; }
        public RelayCommand PrevCommand { get; private set; }
        public RelayCommand<int> MoveSecsCommand { get; private set; }
        public RelayCommand<AudioBookSource> RemoveAudioBookSource { get; private set; }
        public RelayCommand<CloudType> AddCloudAccountCommand { get; private set; }
        public RelayCommand UploadBookToDrBoxCommand { get; private set; }
        public RelayCommand<Guid> CancelOperationCommand { get; private set; }
        public RelayCommand<Guid> PauseOperationCommand { get; private set; }
        public RelayCommand<Guid> ResumeOperationCommand { get; private set; }
        public RelayCommand DownloadBookFromDrBoxCommand { get; private set; }
        public RelayCommand ShowBookDetailCommand { get; private set; }


        public event EventHandler<AudioBookSourcesCombined> ShowBookDetails;

        public DropBoxController DrbController
        {
            get { return drbController; }
        }


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
            drbController = new DropBoxController();
            odController = new OneDriveController();
            notificator = new UniversalNotification();

            PlayCommand = new RelayCommand(Play);
            PauseCommand = new RelayCommand(Pause);
            NextCommand = new RelayCommand(NextFile,
                () => SelectedFolder != null && SelectedFolder.CurrentFile + 1 < SelectedFolder.Files.Count());
            PrevCommand = new RelayCommand(PrevFile, () => SelectedFolder != null && SelectedFolder.CurrentFile - 1 >= 0);
            MoveSecsCommand = new RelayCommand<int>(MoveSecs, (c) => SelectedFolder != null);
            RemoveAudioBookSource = new RelayCommand<AudioBookSource>(RemoveSource);
            AddCloudAccountCommand = new RelayCommand<CloudType>(AddDropBoxAccount);
            UploadBookToDrBoxCommand = new RelayCommand(UploadBookToDrBox, () => drbController.IsAutorized && !string.IsNullOrWhiteSpace( SelectedFolder?.AccessToken));
            DownloadBookFromDrBoxCommand = new RelayCommand(DownloadBookFromDrBok, () => drbController.IsAutorized);
            ShowBookDetailCommand = new RelayCommand(ShowBookDetailExecute);

            CancelOperationCommand = new RelayCommand<Guid>(CancelOperation);
            PauseOperationCommand = new RelayCommand<Guid>(PauseOperation);
            ResumeOperationCommand = new RelayCommand<Guid>(ResumeOperation);

            drbController.CloseAuthPage += async (sender, args) => { await RefreshDropBoxData(); };
            player.CurrentStateChanged += PlayerOnCurrentStateChanged;
        }

        private async void ShowBookDetailExecute()
        {
            var ev = new AudioBookSourcesCombined() {MainSource = SelectedFolder};
            if (drbController.IsAutorized)
                ev.Cloud = await drbController.GetAudioBookInfo(SelectedFolder);
            OnShowBookDetails(ev);
        }

        private async void DownloadBookFromDrBok()
        {
            if (drbController.IsAutorized)
            {
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
                    tempSelectedFolder = await drbController.GetAudioBookInfo(tempSelectedFolder.Folder);
                }
                if (tempSelectedFolder == null)
                    return;
                UploadProgress progress = new UploadProgress()
                {
                    BookName = tempSelectedFolder.Name,
                    MaximumValue = tempSelectedFolder.Files.Count(),
                    OperationId = Guid.NewGuid(),
                    Status = "Downloading",
                    Type = CloudType.DropBox
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
                                    drbController.DownloadBookFile(tempSelectedFolder.Folder,
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
            else
                notificator.ShowMessage("", "Before procide, pelase authorize in DropBox");
        }

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

        private async void UploadBookToDrBox()
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
                var drBookInfo = await drbController.GetAudioBookInfo(selectedFolderTemp);
                await drbController.UploadBookMetadata(selectedFolderTemp, drBookInfo.Revision);
                if (drBookInfo == null)
                {
                    drBookInfo = new AudioBookSourceCloud();
                }
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
                            var freeSpace = await drbController.GetFreeSpaceBytes();
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
                            drbController.Uploadbook(selectedFolderTemp.Folder, selectedFolderTemp.Files[i].Name,
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

        private void PlayerOnCurrentStateChanged(object sender, RoutedEventArgs routedEventArgs)
        {
            switch (player.CurrentState)
            {
                case MediaElementState.Paused:
                    if (State == MediaPlaybackFlow.Play)
                        if (player.Position == player.NaturalDuration.TimeSpan)
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
            if (loaded.CloudServices.Any(x => x.Name == "DropBox"))
            {
                drbController.Token = loaded.CloudServices[0].Token;
            }
            if (loaded.CloudServices.Any(x => x.Name == "OneDrive"))
                odController.Token = loaded.CloudServices.First(x => x.Name == "OneDrive").Token;
            drbController.Inicialize();
            odController.Inicialize();
            baseFolder = loaded.BaseFolder;
            CheckBaseFolder();
            RefreshDropBoxData();
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
        async Task RefreshDropBoxData()
        {
            DropBoxRefreshing = true;
            try
            {
                drFolders = await drbController.GetAudioBooksInfo();
                var oldFolders = Folders.OfType<AudioBookSourceCloud>().ToList();
                foreach (var old in oldFolders)
                    Folders.Remove(old);
                foreach (var f in drFolders)
                {
                    var inFolders = Folders.FirstOrDefault(x => x.Folder == f.Folder) as AudioBookSourceWithClouds;
                    if (inFolders == null)
                        continue;
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
                        await drbController.UploadBookMetadata(inFolders);
                    }
                    inFolders.CountFilesDropBox = f.Files.Count();
                    inFolders.CountFilesDropBoxTotal = f.AvalibleCount;
                    inFolders.IsHaveDropBox = true;
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
                UploadBookToDrBoxCommand.RaiseCanExecuteChanged();
                DownloadBookFromDrBoxCommand.RaiseCanExecuteChanged();
            }
            finally
            {
                DropBoxRefreshing = false;
            }
        }

        async Task CheckBaseFolder()
        {
            if (string.IsNullOrWhiteSpace(baseFolder?.AccessToken))
                return;

            var dir = await StorageApplicationPermissions.FutureAccessList.GetFolderAsync(baseFolder.AccessToken);

            var localBookFolders = await dir.GetFoldersAsync();
            foreach (var folder in localBookFolders)
            {
                if (!Folders.Any(f => f.Folder == folder.Name))
                {
                    string accessToken = StorageApplicationPermissions.FutureAccessList.Add(folder);
                    AddPlaySource(folder.Name, accessToken);
                    continue;
                }
                var fold = await StorageApplicationPermissions.FutureAccessList.GetFolderAsync(baseFolder.AccessToken);
                var alreadyBook = Folders.First(f => f.Folder == folder.Name);
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
                        StorageApplicationPermissions.FutureAccessList.Remove(alreadyBook.AccessToken);
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
            if (drbController.IsAutorized)
            {
                Folders.AsParallel().Select(async x =>
                        await drbController.UploadBookMetadata(x));
            }
            CloudService drBoxService = new CloudService();
            CloudService OneDriveService = new CloudService();
            if (drbController.IsAutorized)
            {
                drBoxService.Name = "DropBox";
                drBoxService.Token = drbController.Token;
            }
            if (odController.IsAutorized)
            {
                OneDriveService.Name = "OneDrive";
                OneDriveService.Token = odController.Token;
            }
            var books = new SaveModel
            {
                AudioBooks = Folders.Where(x => x.GetType() == typeof(AudioBookSourceWithClouds)).ToArray(),
                CurrentState = CurrentState,
                BaseFolder = baseFolder
            };
            if (!string.IsNullOrWhiteSpace(drBoxService.Name))
            {
                books.CloudServices = new[] {drBoxService, OneDriveService };
            }

            await repository.Save(books);
        }

        public async void AddPlaySource(string folder, string token)
        {
            if (Folders.Any(f => f.Path == folder))
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
            RefreshDropBoxData();
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
                    await drbController.DeleteAudioBook(source);
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
                    notificator.ShowMessage("", "Do you want to delete this book from cloud?",
                        ActionButtons.Cancel | ActionButtons.Ok) == ActionButtons.Ok)
                await drbController.DeleteAudioBook(contains);
        }

        public async void Play()
        {
            if (SelectedFolder != null)
            {
                //CurrentState.BookName = SelectedFolder.Name;
                if (CurrentState.BookName == SelectedFolder.Name)
                {
                    if (PlayingFile == SelectedFolder.Files[SelectedFolder.CurrentFile])
                    {
                        State = MediaPlaybackFlow.Play;
                        player.Position = SelectedFolder.Position;
                        player.Play();
                    }
                    else
                    {
                        //SelectedFolder.CurrentFile = 0;
                        //SelectedFolder.Position = TimeSpan.Zero;
                        //PlayingFile = SelectedFolder.Files[SelectedFolder.CurrentFile];
                        PlayingSource = SelectedFolder;
                        await SetSource(PlayingSource);
                        Play(PlayingFile.Name);
                    }
                }
                else if (SelectedFolder.Files.Any())
                {
                    CurrentState.BookName = SelectedFolder.Name;
                    //SelectedFolder.CurrentFile = 0;
                    //SelectedFolder.Position = TimeSpan.Zero;
                    PlayingSource = SelectedFolder;
                    await SetSource(PlayingSource);
                    //PlayingFile = SelectedFolder.Files.First();
                    Play(PlayingFile.Name);
                }
                ControlStateChanged();
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
            book = book ?? SelectedFolder;
            if (book is AudioBookSourceCloud)
            {
                var link = await drbController.GetLink(book.Folder, file);
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

        private async Task SetSource(AudioBookSource book)
        {
            PlayingFile = SelectedFolder.Files[SelectedFolder.CurrentFile];
            temp = SelectedFolder.Position;
            var afterOpened = new RoutedEventHandler(delegate(object sender, RoutedEventArgs args)
            {
                if (temp == TimeSpan.MinValue)
                    return;
                SelectedFolder.Position = temp;
                player.Position = SelectedFolder.Position;
                temp = TimeSpan.MinValue;
            });
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
                drbController.UploadBookMetadata(SelectedFolder);
                odController.UploadBookMetadata(SelectedFolder);
            }
        }

        public void Stop()
        {
            player.Stop();
            State = MediaPlaybackFlow.Stop;
            SelectedFolder = null;
            CurrentState.BookName = null;
            ControlStateChanged();
            drbController.UploadBookMetadata(SelectedFolder);
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
                if (SelectedFolder.IsHaveDropBox)
                {
                    var cloudsFolder = drFolders.FirstOrDefault(x => x.Name == SelectedFolder.Name);
                    var fiel = cloudsFolder?.AvalibleFiles.FirstOrDefault(f => f.Name == nextFile.Name);
                    if (fiel != null)
                    {
                        PlayingFile = fiel;
                        await SetSource(PlayingFile.Name, cloudsFolder);
                        SelectedFolder.Position = TimeSpan.Zero;
                        player.Play();


                    }
                }

            }
            else
            {

                PlayingFile = nextFile;

                await SetSource(PlayingFile.Name);
                SelectedFolder.Position = TimeSpan.Zero;
                player.Play();
            }
        }

        private async void NextFile()
        {
            if (SelectedFolder.CurrentFile + 1 < SelectedFolder.Files.Count)
            {
                SelectedFolder.CurrentFile++;
                var nextFile = SelectedFolder.GetCurrentFile;
                await PlayFile(nextFile);
            }
            ControlStateChanged();
        }

        private async void PrevFile()
        {
            if (SelectedFolder.CurrentFile - 1 >= 0)
            {
                SelectedFolder.CurrentFile--;
                var nextFile = SelectedFolder.GetCurrentFile;
                await PlayFile(nextFile);
            }
            ControlStateChanged();
        }

        public async void MoveSecs(int secs)
        {
            if (CurrentState == null)
                return;
            SelectedFolder.Position = SelectedFolder.Position.Add(TimeSpan.FromSeconds(secs));
            ControlStateChanged();
        }

        public void AddDropBoxAccount(CloudType type)
        {
            if (type == CloudType.DropBox)
                drbController.Auth();
            if (type==CloudType.OneDrive)
                odController.Auth();
        }

        public void AddOneDriveAccount()
        {
            
        }

        public async void AddBaseFolder(string folder, string accessToken)
        {
            baseFolder = new Folder() {Path = folder, AccessToken = accessToken};

            await CheckBaseFolder();
            await RefreshDropBoxData();
        }

        protected virtual void OnShowBookDetails(AudioBookSourcesCombined e)
        {
            ShowBookDetails?.Invoke(this, e);
        }
    }
}