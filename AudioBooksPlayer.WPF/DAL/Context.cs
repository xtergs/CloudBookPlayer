using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AudioBooksPlayer.WPF.Model;
using Newtonsoft.Json;

namespace AudioBooksPlayer.WPF.DAL
{
    public class Context
    {
        private string FilePath = "db.json";
        private List<AudioBooksInfo> _audioBooks = new List<AudioBooksInfo>(5);
		private List<RootFolderAudioBooks> _folders = new List<RootFolderAudioBooks>(5); 
        public AudioBooksInfo[] AudioBooks
        {
            get { return _audioBooks.ToArray(); }
            private set { _audioBooks = new List<AudioBooksInfo>(value); }
        }

        public void AddAudioBook(AudioBooksInfo bookInfo)
        {
            _audioBooks.Add(bookInfo);
        }

        public void LoadData()
        {
            if (!File.Exists(FilePath))
                return;
	        try
	        {
		        using (var stream = File.OpenText(FilePath))
		        using (var rd = new JsonTextReader(stream))
		        {
			        var ser = new JsonSerializer();
			        var des = ser.Deserialize<Tuple<AudioBooksInfo[], RootFolderAudioBooks[]>>(rd);
			        AudioBooks = des.Item1;
			        _folders = des.Item2.ToList();
		        }
	        }
	        catch (JsonSerializationException e)
	        {
		        Debug.WriteLine("Error while deserializing json");
	        }
        }

        public void SaveData()
        {
            using (var stream = File.CreateText(FilePath))
            using (var wr = new JsonTextWriter(stream))
            {
	            Tuple<AudioBooksInfo[], RootFolderAudioBooks[]> an = new Tuple<AudioBooksInfo[], RootFolderAudioBooks[]>(
		            AudioBooks, _folders.ToArray());
                var conv = new JsonSerializer();
                conv.Serialize(wr, an);
            }
        }

        public void RemoteAudioBook(AudioBooksInfo selectedAudioBook)
        {
            _audioBooks.Remove(selectedAudioBook);
        }

	    public void AddRootFolder(RootFolderAudioBooks folder)
	    {
		    if (_folders.Any(x => x.Folder == folder.Folder))
			    return;
		    _folders.Add(folder);
	    }

	    public void RemoveRootFolder(string path)
	    {
		    if (_folders.Any(x => x.Folder == path))
			    _folders.Remove(_folders.First(x => x.Folder == path));
	    }
    }
}
