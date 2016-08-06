using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Markup;
using Id3;
using AudioBooksPlayer.WPF.Model;

namespace AudioBooksPlayer.WPF.Logic
{
	public static class DictionaryExtension
	{
		public static void IncOrAdd<T>(this Dictionary<T, int> dic,T key)
		{
			if (dic.ContainsKey(key))
				dic[key]++;
			else
				dic.Add(key, 1);
		}

		public static int GetVal<T>(this Dictionary<T, int> dic, T key)
		{
			if (dic.ContainsKey(key))
				return dic[key];
			return 0;
		}
	}

	public struct BooksProgress
	{
		public int TotalCount { get; set; }
		public int Current { get; set; }
	}

    public class AudioBooksProcessor
    {
	    private Dictionary<string, Semaphore> driveThreads = new Dictionary<string, Semaphore>(4);

	    public int MaxThreadPerDrive { get; set; } = 4;

	    private void Check(string drive)
	    {
		    if (driveThreads.ContainsKey(drive))
			    return;
			driveThreads.Add(drive, new Semaphore(MaxThreadPerDrive, MaxThreadPerDrive));
	    }

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
		                Id3Tag[] tags;
	                try
	                {
		                tags = audioTags.GetAllTags();
	                }
	                catch (IndexOutOfRangeException)
	                {
		                return null;
	                }
	                if (tags.Any() && tags[0].Track.IsAssigned)
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
                        Size = new FileInfo(x).Length,
                        Duration = audioTags.Audio.Duration
                    };
                }
            }).ToArray();
			if (files.Length == 0 || files.Any(x=> x == null))
				return null;
            return new AudioBooksInfo()
            {
                BookName = Path.GetDirectoryName(folder),
                FolderPath = folder,
                Files = files,
                TotalDuration = files.Select(x=> x.Duration).Aggregate((x,y)=> y + x),
                CurrentFile =  files.Min(x=> x.Order),
                PositionInFile = 0
            };
        }

	    public Task< AudioBooksInfo > ProcessAudoiBookFolderAsync(string folder)
	    {
		    string disk = Path.GetPathRoot(folder);

		    Check(disk);
		    return Task.Run(() =>
		    {
			    try
			    {
				    driveThreads[disk].WaitOne();
				    return ProcessAudoiBookFolder(folder);
			    }
			    finally
			    {
				    driveThreads[disk].Release();
			    }
		    });
	    }


		public RootFolderAudioBooks ProcessFolderWithBooks(string folder)
	    {
		    RootFolderAudioBooks books = new RootFolderAudioBooks() {Folder = folder};
			var dirs = Directory.EnumerateDirectories(folder).ToArray();
			foreach (var dir in dirs)
			{
				var book = ProcessAudoiBookFolder(dir);
				if (book == null)
					continue;
				books.Books.Add(book);
			}
		    return books;
	    }

	    public async Task<RootFolderAudioBooks> ProcessFolderWithBooksAsync(string folder)
	    {
			RootFolderAudioBooks books = new RootFolderAudioBooks() { Folder = folder };
			var dirs = Directory.EnumerateDirectories(folder).ToArray();
		    var bookss = dirs.Select(ProcessAudoiBookFolderAsync);
		    books.Books = (await Task.WhenAll(bookss)).ToList();
			return books;
		}

	}
}
