using System;
using System.Collections.Generic;
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
           using (var stream = File.OpenText(FilePath))
           using (var rd = new JsonTextReader(stream))
           {
               var ser = new JsonSerializer();
               AudioBooks = ser.Deserialize<AudioBooksInfo[]>(rd);
           } 
        }

        public void SaveData()
        {
            using (var stream = File.CreateText(FilePath))
            using (var wr = new JsonTextWriter(stream))
            
            {
                var conv = new JsonSerializer();
                conv.Serialize(wr, AudioBooks);
            }
        }

        public void RemoteAudioBook(AudioBooksInfo selectedAudioBook)
        {
            _audioBooks.Remove(selectedAudioBook);
        }
    }
}
