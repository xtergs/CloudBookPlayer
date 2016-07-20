using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AudioBooksPlayer.WPF.Model
{
    public class AudioBooksInfo
    {

        public string BookName { get; set; }
        public AudioFileInfo[] Files { get; set; }
        public long PositionInFile { get; set; }
        public int CurrentFile { get; set; }
        public TimeSpan TotalDuration { get; set; }

    }
}
