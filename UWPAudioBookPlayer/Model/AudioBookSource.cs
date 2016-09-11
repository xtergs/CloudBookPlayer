using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.Storage.AccessCache;
using Windows.Storage.Streams;
using Newtonsoft.Json.Serialization;
using PropertyChanged;
using SQLite.Net.Attributes;
using UWPAudioBookPlayer.Model;

namespace UWPAudioBookPlayer.Model
{
    [ImplementPropertyChanged]
    public class AudioBookSourceWithClouds : AudioBookSource
    {
        [Newtonsoft.Json.JsonIgnore]
        public bool IsHaveDropBox { get; set; }
        [Newtonsoft.Json.JsonIgnore]
        public int CountFilesDropBox { get; set; }
        [Newtonsoft.Json.JsonIgnore]
        public int CountFilesDropBoxTotal { get; set; }

        public override async Task<Tuple<string, IRandomAccessStream>> GetFileStream(string fileName)
        {
            var dir = await StorageApplicationPermissions.FutureAccessList.GetFolderAsync(this.AccessToken);
            var fl = await dir.GetFileAsync(fileName);
            var stream = await fl.OpenAsync(FileAccessMode.Read);
            return new Tuple<string, IRandomAccessStream>(fl.ContentType, stream);
        }
    }
    [ImplementPropertyChanged]
    public class AudioBookSource
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }
        private TimeSpan _position = TimeSpan.Zero;
        public int CurrentFile { get; set; } = 0;

        public TimeSpan Position
        {
            get { return _position; }
            set
            {
                _position = value;
                UpdateModifyDateTime();
            }
        }

        public string AccessToken { get; set; }
        public string Path { get; set; }
        public string Name { get; set; }
        public string Cover { get; set; }
        public TimeSpan TotalDuration { get; set; }
        public List<AudiBookFile> Files { get; set;}

        public DateTime CreationDateTimeUtc { get; set; }
        public DateTime ModifiDateTimeUtc { get; set; }

        public bool IsLocked { get; set; }



        public void UpdateModifyDateTime()
        {
            ModifiDateTimeUtc = DateTime.UtcNow;
        }

        public virtual Task<Tuple<string, IRandomAccessStream>> GetFileStream(string fileName)
        {
            return null;
        }

        public string Folder => System.IO.Path.GetFileName(Path);
        public int AvalibleCount => Files.Count(f => f.IsAvalible);
        public AudiBookFile[] AvalibleFiles => Files.Where(f => f.IsAvalible).ToArray();

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

    }

    [ImplementPropertyChanged]
    public class AudiBookFile
    {
        public string Name { get; set; }
        public uint Order { get; set; }
        public TimeSpan Duration { get; set; }
        public ulong Size { get; set; }
        public bool IsAvalible { get; set; } = false;
    }

    public class AudioBooksRootFolder
    {
        public string Path { get; set; }
        public string AccessToken { get; set; }
        public string Name { get; set; }
    }

    public class AudioBookSourceFactory
    {
        public string[] Extensions { get; set; } = new[] {".mp3", ".vaw", ".m3u"};
        public Task<AudioBookSourceWithClouds> GetFromLocalFolderAsync(string folderPath, string token, IProgress<Tuple<int,int>> progress)
        {
            return Task.Run(async () =>
            {
                var op = new Tuple<int, int>(0, 0);
                try
                {
                    var dir = await StorageApplicationPermissions.FutureAccessList.GetFolderAsync(token);

                    var files = (await dir.GetFilesAsync()).Where(f => Extensions.Contains(f.FileType)).ToList();
                    if (!files.Any())
                        return null;
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


            var dir = await StorageApplicationPermissions.FutureAccessList.GetFolderAsync(source.AccessToken);
            await dir.DeleteAsync(StorageDeleteOption.Default);
            StorageApplicationPermissions.FutureAccessList.Remove(source.AccessToken);
        }
    }
}
