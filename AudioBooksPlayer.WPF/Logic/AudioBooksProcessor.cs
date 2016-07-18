using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
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
            var files = Directory.EnumerateFiles(folder).Select(x=> new AudioFileInfo() {FileName = Path.GetFileName(x), FilePath = x}).ToArray();
            return new AudioBooksInfo()
            {
                BookName = Path.GetDirectoryName(folder),
                Files = files
            };
        }
    }
}
