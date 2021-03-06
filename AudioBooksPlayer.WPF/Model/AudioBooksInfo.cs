﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Markup;

namespace AudioBooksPlayer.WPF.Model
{
    public class AudioBooksInfo
    {
        public string BookName { get; set; }
        public string Author { get; set; }
        public string FolderPath { get; set; }

        public AudioFileInfo[] Files { get; set; }
        public long PositionInFile { get; set; }
        public int CurrentFile { get; set; }
        public TimeSpan TotalDuration { get; set; }


	    public AudioFileInfo CurrentFileInfo
	    {
		    get
		    {
			    if (CurrentFile == Files.Length)
				    return null;
			    return Files[CurrentFile];
		    }
	    }
    }

	public class RootFolderAudioBooks
	{
		public string Folder { get; set; }
		public List<AudioBooksInfo> Books { get; set; }
	}

    public static class AudioBooksInfoExtension
    {
        public struct Position
        {
            public int CurFile { get; set; }
            public long Pos { get; set; }
        }
        public static int LeftFilesToPlay(this AudioBooksInfo book)
        {
            if (book == null)
                return 0;
            return book.Files.Length - book.CurrentFile;
        }
    }
}
