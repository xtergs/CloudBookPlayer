using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using SQLite.Net;
using SQLite.Net.Platform.WinRT;
using UWPAudioBookPlayer.DAL.Model;
using UWPAudioBookPlayer.Model;
using UWPAudioBookPlayer.ModelView;

namespace UWPAudioBookPlayer.DAL
{
    class SqliteRepository : IDataRepository
    {
        public string LocalStorePath { get; set; } = Windows.Storage.ApplicationData.Current.LocalFolder.Path;
        public string RoamingStorePath { get; set; } = Windows.Storage.ApplicationData.Current.RoamingFolder.Path;


        public string LocalFileName { get; set; } = "localdb.sqlite";
        public string RoamingFileName { get; set; } = "roamindb.sqlite";

        private readonly SQLiteConnection db;



        public SqliteRepository()
        {
            string path = Path.Combine(LocalStorePath, LocalFileName);

            db = new SQLiteConnection(new SQLitePlatformWinRT(), path);
            var x= db.CreateCommand("create table AudioBookSources (id integer primary key, name varchar);").ExecuteNonQuery();
            db.CreateTable<AudioBookSource>();
            db.CreateTable<CloudService>();
            db.CreateTable<Folder>();

        }
        public async Task<SaveModel> Load()
        {
            var list = db.Table<AudioBookSourceWithClouds>().ToArray();
            var clouds = db.Table<CloudService>().ToArray();
            var folder = db.Table<Folder>().FirstOrDefault();
            SaveModel result = new SaveModel()
            {
                AudioBooks = list,
                CloudServices = clouds,
                BaseFolder = folder,
            };

            return result;
        }

        public async Task Save(SaveModel books)
        {
            db.InsertOrReplaceAll(books.AudioBooks);
            db.InsertOrReplaceAll(books.CloudServices);
            db.Update(books.BaseFolder);
            db.Commit();
        }

        public BookMark[] BookMarks(AudioBookSourceWithClouds book)
        {
            throw new NotImplementedException();
        }

        public bool AddBookMark(AudioBookSourceWithClouds book, BookMark bookMark)
        {
            throw new NotImplementedException();
        }

        public void UpdateBookMark(AudioBookSourceWithClouds playingSource, BookMark obj)
        {
            throw new NotImplementedException();
        }

        public bool RemoveBookMark(BookMark bookMark, AudioBookSourceWithClouds audioBook)
        {
            throw new NotImplementedException();
        }
    }
}
