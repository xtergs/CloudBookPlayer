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
        public string FileName { get; set; } = "db.sqlite";
        private readonly SQLiteConnection db;

        public SqliteRepository()
        {
            string path = Path.Combine(Windows.Storage.ApplicationData.Current.LocalFolder.Path, FileName);

            db = new SQLiteConnection(new SQLitePlatformWinRT(), path);
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
    }
}
