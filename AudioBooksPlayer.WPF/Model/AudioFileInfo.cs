using System;
using System.Collections.Generic;

namespace AudioBooksPlayer.WPF.Model
{
    public class AudioFileInfo
    {
        public string Title { get; set; }
        public string Author { get; set; }
        public string Genre { get; set; }
        public string FileName { get; set; }
        public string FilePath { get; set; }
        public int Order { get; set; }

        public TimeSpan Duration { get; set; }
        public long Size { get; set; }
        public int Bitrate { get; set; }
        public int Frequesncy { get; set; }
        public DateTime? Year { get; set; }
        public List<string> Artists { get; set; }
    }
}
