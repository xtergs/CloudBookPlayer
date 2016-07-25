using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Id3;
using AudioBooksPlayer.WPF.Model;

namespace AudioBooksPlayer.WPF.Logic
{
    public class AudioBooksProcessor
    {
        public AudioBooksInfo ProcessAudoiBookFolder(string folder)
        {
            if (folder == null)
                throw new ArgumentNullException(nameof(folder));
            if (!Directory.Exists(folder))
                throw new DirectoryNotFoundException($"path can't be found, {folder}");
            var files = Directory.EnumerateFiles(folder, "*.mp3").OrderBy(Path.GetFileNameWithoutExtension).Select(x =>
            {
                using (var audioTags = new Mp3File(x, Mp3Permissions.Read))
                {
                    int order = 0;
                    if (audioTags.GetAllTags().Any() && audioTags.GetAllTags()[0].Track.IsAssigned)
                    {
                        var asInt = audioTags.GetAllTags()[0].Track.AsInt;
                        if (asInt != null)
                            order = asInt.Value;
                    }

                    return new AudioFileInfo()
                    {
                        FileName = Path.GetFileName(x),
                        FilePath = x,
                        Order = order,
                        Duration = audioTags.Audio.Duration
                    };
                }
            }).ToArray();

            return new AudioBooksInfo()
            {
                BookName = Path.GetDirectoryName(folder),
                Files = files,
                TotalDuration = files.Select(x=> x.Duration).Aggregate((x,y)=> y + x),
                CurrentFile =  files.Min(x=> x.Order),
                PositionInFile = 0
            };
        }
    }
}
