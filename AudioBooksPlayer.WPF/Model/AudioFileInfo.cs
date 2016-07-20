using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AudioBooksPlayer.WPF.Model
{
    public class AudioFileInfo
    {
        public string FileName { get; set; }
        public string FilePath { get; set; }
        public int Order { get; set; }
        public TimeSpan Duration { get; set; }
    }
}
