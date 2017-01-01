using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using Windows.ApplicationModel.Activation;
using Windows.Storage;
using Windows.Storage.AccessCache;
using Windows.Storage.FileProperties;
using Windows.Storage.Streams;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using PropertyChanged;
using SQLite.Net.Attributes;
using UWPAudioBookPlayer.Converter;
using UWPAudioBookPlayer.DAL.Model;
using UWPAudioBookPlayer.Model;
using UWPAudioBookPlayer.ModelView;

namespace UWPAudioBookPlayer.Model
{
    static class AudioBookSourceWithCloudsHelper
    {
        public static AudioBookSourceWithClouds ToGeneralSource(this AudioBookSourceWithClouds source)
        {
            var general = new AudioBookSourceWithClouds()
            {
                Position = source.Position,
                AdditionSources = source.AdditionSources,
                TotalDuration = source.TotalDuration,
                PlaybackRate = source.PlaybackRate,
                Name = source.Name,
                IsLocked = source.IsLocked,
                CurrentFile = source.CurrentFile,
                CreationDateTimeUtc = source.CreationDateTimeUtc,
                ModifiDateTimeUtc = source.ModifiDateTimeUtc,

            };
            return general;
        }
    }
    public class ObservableAudioBooksCollection<T> : ObservableCollection<T> where T : AudioBookSource
    {
        protected override void InsertItem(int index, T item)
        {
            int i = 0;
            bool found = false;
            for (; i < Items.Count; i++)
            {
                if (item.ModifiDateTimeUtc.Date >= Items[i].ModifiDateTimeUtc.Date && (long)item.ModifiDateTimeUtc.TimeOfDay.TotalSeconds >= (long)Items[i].ModifiDateTimeUtc.TimeOfDay.TotalSeconds)
                {
                    found = true;
                    break;
                }
            }

            if (!found)
                i = Items.Count;
            base.InsertItem(i, item);
        }
    }

    [ImplementPropertyChanged]
    public class OnlineAudioBookSource : AudioBookSourceCloud
    {
        public string Link { get; set; }
        public string HostLink { get; set; }

        public OnlineAudioBookSource(string cloudStamp, CloudType type) : base(cloudStamp, type)
        {
        }

        public override async Task<string> GetImage(string name)
        {
            return Cover.Url;
        }

        
    }


    public static class AudioBookSourceWithCloudsLocalExtension
    {
        public static async Task<StorageFile> GetFile(this AudioBookSourceWithClouds book, string fileName)
        {
            if (book.AccessToken == null || !StorageApplicationPermissions.FutureAccessList.ContainsItem(book.AccessToken))
                return null;
            try
            {
                var dir = await StorageApplicationPermissions.FutureAccessList.GetFolderAsync(book.AccessToken);
                return await dir.GetFileAsync(fileName);
            }
            catch (FileNotFoundException ex)
            {
                return null;
            }
            catch (Exception ex)
            {
                throw;
            }
        }

        public static bool IsOnlineBook(this AudioBookSource book)
        {
            return book is OnlineAudioBookSource;
        }

        public static bool IsCloudBook(this AudioBookSource book)
        {
            return book is AudioBookSourceCloud;
        }
    }


    [ImplementPropertyChanged]
    public class AudioBookSourceWithClouds : AudioBookSource
    {
        [Newtonsoft.Json.JsonIgnore]
        public bool IsHaveDropBox { get; set; }
        [Newtonsoft.Json.JsonIgnore]
        public int CountFilesDropBox { get; set; }
        [Newtonsoft.Json.JsonIgnore]
        public int CountFilesDropBoxTotal { get; set; }

        [JsonIgnore]
        public ObservableCollection<AudioBookSource> AdditionSources { get; set; } = new ObservableCollection<AudioBookSource>();

        

        public override async Task<Tuple<string, IRandomAccessStream>> GetFileStream(string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName))
                return new Tuple<string, IRandomAccessStream>("", new InMemoryRandomAccessStream());
            var dir = await StorageApplicationPermissions.FutureAccessList.GetFolderAsync(this.AccessToken);
            var fl = await dir.GetFileAsync(fileName);
            var stream = await fl.OpenAsync(FileAccessMode.Read);
            return new Tuple<string, IRandomAccessStream>(fl.ContentType, stream);
        }

        public override async Task<string> GetImage(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return null;
            var image = await base.GetImage(name);
            if (image == null)
                return null;
            var dir = await StorageApplicationPermissions.FutureAccessList.GetFolderAsync(this.AccessToken);
            var fl = await dir.GetFileAsync(image);
            //var stream = await fl.OpenAsync(FileAccessMode.Read);
            return fl.Path;
        }

        [JsonIgnore]
        public CloudType[] Clouds
            => AdditionSources.OfType<AudioBookSourceCloud>().Select(x => x.Type).Distinct().ToArray();

    }

    public struct ImageStruct
    {
        public ImageStruct(string title, string url = null)
        {
            Title = title;
            Url = url;
        }

        public string Title { get; set; }
        public string Url { get; set; }

        public bool IsValide => !string.IsNullOrWhiteSpace(Title);
    }
    [ImplementPropertyChanged]
    public class AudioBookSource : INotifyPropertyChanged
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }
        private TimeSpan _position = TimeSpan.Zero;
        private double _playbackRate = 1;
        public int CurrentFile { get; set; } = 0;

        public TimeSpan Position
        {
            get { return _position; }
            set
            {
                if (_position == value)
                    return;
                if (value == TimeSpan.Zero)
                {
                    var st = "dkfjdkf";
                }
                _position = value;
                UpdateModifyDateTime();
            }
        }

        public double PlaybackRate
        {
            get { return _playbackRate; }
            set { _playbackRate = value; }
        }

        public string AccessToken { get; set; }
        public string Path { get; set; }
        public string Name { get; set; }

        public ImageStruct Cover
        {
            get
            {
                if (Images == null || Images.Length <= 0)
                    return new ImageStruct();
                return Images.First();
            }
        }
        public ImageStruct[] Images { get; set; }
        public TimeSpan TotalDuration { get; set; }
        public List<AudiBookFile> Files { get; set;} = new List<AudiBookFile>();

        public DateTime CreationDateTimeUtc { get; set; } = DateTime.UtcNow;
        [JsonIgnore]
        public DateTime CreationDateTimeLocal => CreationDateTimeUtc.ToLocalTime();
        public DateTime ModifiDateTimeUtc { get; set; } = DateTime.UtcNow;

        public bool IsLocked { get; set; }
        public List<string> ExternalLinks { get; set; } = new List<string>(0);

        public List<BookMark> BookMarks { get; set; } = new List<BookMark>();
        public List<PlayBackHistoryElement> History { get; set; } = new List<PlayBackHistoryElement>(10);

        [JsonIgnore]
        public List<PlayBackHistoryElement> OrderedHistory => History?.OrderByDescending(x => x.TimeStampUtc).ToList();

        public void AddHistory(PlayBackHistoryElement.HistoryType historyType)
        {
            var playBackHistoryElement = new PlayBackHistoryElement()
            {
                FileName = GetCurrentFile.Name,
                Position = Position,
                TimeStampUtc = DateTime.UtcNow,
                Type = historyType
            };

            History.Add(playBackHistoryElement);
            OnPropertyChanged(nameof(OrderedHistory));
        }
        public void UpdateModifyDateTime()
        {
            if (IgnoreTimeOfChanges)
                return;
            ModifiDateTimeUtc = DateTime.UtcNow;
        }
        public virtual Task<Tuple<string, IRandomAccessStream>> GetFileStream(string fileName)
        {
            return null;
        }

        public virtual async Task<string> GetImage(string name)
        {
            return Images?.FirstOrDefault(x => x.Url == name).Url;
        }

        [JsonIgnore]
        public bool IgnoreTimeOfChanges { get; set; } = false;
        [JsonIgnore]
        public string Folder => System.IO.Path.GetFileName(Path);
        [JsonIgnore]
        public int AvalibleCount => Files.Count(f => f.IsAvalible);
        [JsonIgnore]
        public AudiBookFile[] AvalibleFiles => Files?.Where(f => f.IsAvalible).ToArray() ?? new AudiBookFile[0];
        [JsonIgnore]
        public AudiBookFile GetCurrentFile
        {
            get
            {
                if (Files == null)
                    return null;
                if (CurrentFile >= Files.Count || CurrentFile < 0)
                    return null;
                return Files[CurrentFile];
            }
        }

        public void SetPosition(TimeSpan position)
        {
            if (_position == position)
                return;
            if (position == TimeSpan.Zero)
            {
                var skfj = "dkjf";
            }
            _position = position;
            UpdateModifyDateTime();
        }

        public void SetPlayBackRate(double playBackRate)
        {
            _playbackRate = playBackRate;
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public ImageStruct GetAnyCover()
        {
            return Images.FirstOrDefault();
        }

        public bool IsLink(string url)
        {
            if (url.StartsWith("http", StringComparison.CurrentCultureIgnoreCase))
                return true;
            return false;
        }
    }

    public class PlayBackHistoryElement
    {
        public enum HistoryType
        {
            Play, Pause, Stop
        }
        public DateTime TimeStampUtc { get; set; } = DateTime.UtcNow;

        [JsonIgnore]
        public DateTime TimeStampLocal => TimeStampUtc.ToLocalTime();
        public HistoryType Type { get; set; }
        public string FileName { get; set; }
        public TimeSpan Position { get; set; }
    }

    [ImplementPropertyChanged]
    public class BookMark
    {
        public int Order { get; set; }
        public TimeSpan Position { get; set; }
        public bool IsRange { get; set; }
        public TimeSpan EndPosition { get; set; }
        public string Description { get; set; }
        public string Title { get; set; }
        public string FileName { get; set; }
        public DateTime TimeStampUtc { get; set; } = DateTime.UtcNow;

        [JsonIgnore]
        public DateTime TimeStampLocal => TimeStampUtc.ToLocalTime();

        [JsonIgnore]
        public TimeSpan Duration
        {
            get
            {
                if (IsRange)
                {
                    return EndPosition - Position;
                }
                return Position;
            }
        }
    }

    [ImplementPropertyChanged]
    public class AudiBookFile
    {
        private string _chapter;
        public string Name { get; set; }

        public string Chapter
        {
            get
            {
                if (string.IsNullOrWhiteSpace(_chapter))
                    return Name;
                return _chapter;
            }
            set { _chapter = value; }
        }

        public uint Order { get; set; }
        public TimeSpan Duration { get; set; }
        public ulong Size { get; set; }
        public string Path { get; set; } = null;
        public bool IsAvalible { get; set; } = false;

        public AudiBookFile Clone()
        {
            var cloned =  (AudiBookFile)this.MemberwiseClone();
            return cloned;
        }
    }

    public class AudioBooksRootFolder
    {
        public string Path { get; set; }
        public string AccessToken { get; set; }
        public string Name { get; set; }
    }

    public interface IImageController
    {
        Task<Stream> GetImageAsStream(AudioBookSourceWithClouds book,string imageName);
    }

    public class AudioBookSourceFactory : IImageController
    {
        public string BookMarksFolder { get; set; } = "Bookmarks";
        public string[] Extensions { get; set; } = new[] {".mp3", ".vaw", "m4p"};
        public string[] ImageExtensions { get; private set; } = new[] {".jpg", ".png"};
        public Task<AudioBookSourceWithClouds> GetFromLocalFolderAsync(string folderPath, string token, IProgress<Tuple<int,int>> progress)
        {
            return Task.Run(async () =>
            {
                if (string.IsNullOrWhiteSpace(token))
                    return null;
                var op = new Tuple<int, int>(0, 0);
                try
                {
                    var dir = await StorageApplicationPermissions.FutureAccessList.GetFolderAsync(token);

                    var files = (await dir.GetFilesAsync()).Where(f => Extensions.Contains(f.FileType)).ToList();
                    if (!files.Any())
                        return null;
                    var covers = (await dir.GetFilesAsync()).Where(f => ImageExtensions.Contains(f.FileType)).ToList();
                    progress.Report(new Tuple<int, int>(0, files.Count()));
                    var proper = await files.First().Properties.GetMusicPropertiesAsync();
                    var filesWithPropertyes =
                        files.Select(
                            async x =>
                            {
                                return new {File = x, Propertyes = await x.Properties.GetMusicPropertiesAsync()};
                            })
                            .ToList();
                    var result = new AudioBookSourceWithClouds()
                    {
                        CreationDateTimeUtc = DateTime.UtcNow,
                        ModifiDateTimeUtc = DateTime.UtcNow,
                        AccessToken = token,
                        Path = folderPath,
                        Name = proper.Album ?? dir.Name,
                        Files = filesWithPropertyes
                            
                            .OrderBy(f => f.Result.File.Name)
                            .Select((f, i) =>
                            {
                                progress.Report(new Tuple<int, int>(i, files.Count()));
                                var order = f.Result.Propertyes.TrackNumber;
                                return new AudiBookFile()
                                {
                                    Duration = f.Result.Propertyes.Duration,
                                    Name = f.Result.File.Name,
                                    Order = (uint)i,
                                    IsAvalible = true,
                                };
                            }).ToList(),
                        Images = covers.Select(x=> new ImageStruct() {Title = x.Name, Url = x.Name}).ToArray(),
                        //Cover = covers.FirstOrDefault()?.Name,

                    };
                    result.TotalDuration = TimeSpan.FromSeconds(result.Files.Sum(x => x.Duration.TotalSeconds));
                    return result;
                }
                catch (Exception e)
                {
                    throw;
                }
            });
        }

        public void ReorderFiles(AudioBookSource source)
        {
            source.Files = source.Files.OrderBy(x => x.Name).Select((x, i) =>
            {
                x.Order = (uint) i;
                return x;
            }).ToList();
        }

        public async Task RemoveSource(AudioBookSource source)
        {

            if (string.IsNullOrWhiteSpace(source.AccessToken))
                return;
            try
            {
                var dir = await StorageApplicationPermissions.FutureAccessList.GetFolderAsync(source.AccessToken);
                await dir.DeleteAsync(StorageDeleteOption.Default);
                StorageApplicationPermissions.FutureAccessList.Remove(source.AccessToken);
            }
            catch (FileNotFoundException e)
            {
                Debug.WriteLine(e.Message);
            }
        }





        public string GetBookMarkFileName(BookMark bookmark)
        {
            return $"{bookmark.Order}_{bookmark.Title}";
        }

        public BookMark GetBookMarkFromFileName(string fileName)
        {
            var str1 = fileName.Split(new[] {'_'}, StringSplitOptions.RemoveEmptyEntries);
            return new BookMark
            {
                Order = int.Parse(str1[0]),
                Title = str1[1],
                IsRange =  true,
                FileName = fileName,
            };
        }

        public async Task<KeyValuePair<string, Stream>> GetBookMark(AudioBookSourceWithClouds book, BookMark bookMark)
        {
            if (!bookMark.IsRange)
            {
                var res = await book.GetFileStream(bookMark.FileName);
                return new KeyValuePair<string, Stream>(res.Item1, res.Item2.AsStream());
            }
            if (string.IsNullOrWhiteSpace(book.AccessToken))
                throw new ArgumentNullException(nameof(book.AccessToken));

            var folder = await StorageApplicationPermissions.FutureAccessList.GetFolderAsync(book.AccessToken);
            try
            {
                folder = await folder.GetFolderAsync(BookMarksFolder);
                var files = (await folder.GetFilesAsync()).ToArray();
                var name = bookMark.FileName;
                var file =  files.FirstOrDefault(x=> x.Name == name);
                return new KeyValuePair<string, Stream>(file.Name, await file.OpenStreamForReadAsync());
            }
            catch (FileNotFoundException ex)
            {
                Debug.WriteLine($"{ex.Message}, {bookMark.Order}_{bookMark.Title}");
                return new KeyValuePair<string, Stream>("", null);
            }
        }

        public async Task<string[]> GetBookMarks(AudioBookSourceWithClouds book)
        {
            if (string.IsNullOrWhiteSpace(book.AccessToken))
                throw new ArgumentNullException(nameof(book.AccessToken));

            var folder = await StorageApplicationPermissions.FutureAccessList.GetFolderAsync(book.AccessToken);
            try
            {
                folder = await folder.GetFolderAsync(BookMarksFolder);
                var files = await folder.GetFilesAsync();
                var result = new string[files.Count()];
                for (int i = 0; i < files.Count(); i++)
                    result[i] = files[i].Name;

                return result;
            }
            catch (FileNotFoundException ex)
            {
                Debug.WriteLine($"{ex.Message}\n{ex.StackTrace}");
                return new string[0];
            }

        }

        public async Task<string[]> GetBookFiles(AudioBookSourceWithClouds book)
        {
            var dir = await StorageApplicationPermissions.FutureAccessList.GetFolderAsync(book.AccessToken);

            var files =
                (await dir.GetFilesAsync()).Where(f => Extensions.Contains(f.FileType)).Select(x => x.Name).ToArray();
            return files;
        }

        public async Task<bool> ClearAllBookMarks(bool deleteFiles, AudioBookSourceWithClouds book)
        {
            var folder = await StorageApplicationPermissions.FutureAccessList.GetFolderAsync(book.AccessToken);
            try
            {
                folder = await folder.GetFolderAsync(BookMarksFolder);
            }
            catch (FileNotFoundException ex)
            {
                Debug.WriteLine($"{ex.Message}\n{ex.StackTrace}");
                book.BookMarks.Clear();
                return true;
            }
            var files = await folder.GetFilesAsync();

            try
            {
                List<Task> tasks = new List<Task>(files.Count + 1);
                tasks.AddRange(files.Select(file => file.DeleteAsync().AsTask()));
                await Task.WhenAll(tasks);
                await folder.DeleteAsync(StorageDeleteOption.Default);
                book.BookMarks.Clear();
            }
            catch (System.UnauthorizedAccessException ex)
            {
                return false;
            }
            catch (Exception e)
            {
                throw;
            }
            return true;
        }

        public async Task DeleteBookMarks(BookMark bookMark, AudioBookSourceWithClouds book)
        {
            var folder = await StorageApplicationPermissions.FutureAccessList.GetFolderAsync(book.AccessToken);
            folder = await folder.GetFolderAsync(BookMarksFolder);
            var file = await folder.GetFileAsync(bookMark.FileName);
            if (file != null)
                await file.DeleteAsync();
        }


        public async Task<Stream> GetImageAsStream(AudioBookSourceWithClouds book, string imageName)
        {
            var result = await book.GetFileStream(imageName);
            return result.Item2.AsStream();
        }

        public ICloudController SelectContorller(AudioBookSource source, IEnumerable<ICloudController> activeContorllers)
        {
            var cloudSource = source as AudioBookSourceCloud;
            if (cloudSource == null)
                return null;
            return activeContorllers.SingleOrDefault(x => x.CanHandleSource(cloudSource));
        }
    }

}
