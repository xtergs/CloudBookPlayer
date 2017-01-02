using System;
using System.IO;
using System.Threading.Tasks;
using SQLite.Net;
using SQLite.Net.Platform.WinRT;
using UWPAudioBookPlayer.DAL.Model;
using UWPAudioBookPlayer.Model;

namespace UWPAudioBookPlayer.DAL
{
    public interface IControllersRepository : IDataRepository
    {
        void AddCloudService(CloudService service);
        void RemoveCloudSevice(CloudService service);
        void UpdateCloudService(CloudService service);
    }
    class SqliteRepository : IControllersRepository
    {
        public string LocalStorePath { get; set; } = Windows.Storage.ApplicationData.Current.LocalFolder.Path;
        public string RoamingStorePath { get; set; } = Windows.Storage.ApplicationData.Current.RoamingFolder.Path;


        public string LocalFileName { get; set; } = "localdb.sqlite";
        public string RoamingFileName { get; set; } = "roamindb.sqlite";

        private SQLiteConnection db;
        private SQLiteCommand getAllServicesCommand;
        SQLiteCommand createTable;

        private readonly object o = new object();

        private SQLiteConnection CreateConnection()
        {
            if (db == null)
                lock (o)
                {
                    if (db != null)
                        return db;
                    string path;
                    if (RoamingFileName != null)
                        path = Path.Combine(RoamingStorePath, RoamingFileName);
                    else
                        path = Path.Combine(LocalStorePath, LocalFileName);

                    db = new SQLiteConnection(new SQLitePlatformWinRT(), path);
                    createTable = db.CreateCommand(
                        "create table CloudControllers( Id integer primary key, Name varchar not null, Token varchar not null, CloudStamp varchar not null unique);");
                    getAllServicesCommand = db.CreateCommand("select * from CloudControllers;");

                    try
                    {
                        createTable.ExecuteNonQuery();
                    }
                    catch (Exception e)
                    {
                        
                    }

                }
            return db;
        }

        public SqliteRepository()
        {
        }

        public async Task<SaveModel> Load()
        {
            CreateConnection();
            var services = getAllServicesCommand.ExecuteQuery<CloudService>().ToArray();
            SaveModel result = new SaveModel()
            {
                CloudServices = services,
            };

            return result;
        }

        public async Task Save(SaveModel books)
        {
            CreateConnection();
            db.BeginTransaction();
            try
            {
                db.CreateCommand("drop table CloudControllers").ExecuteNonQuery();
                createTable.ExecuteNonQuery();
                foreach (var service in books.CloudServices)
                {
                    var saveAllServicesCommand =
                        db.CreateCommand($"replace into CloudControllers (Name, Token, CloudStamp) values ('{service.Name}', '{service.Token}', '{service.CloudStamp}');");
                    var result = saveAllServicesCommand.ExecuteNonQuery();
                }
                db.Commit();
            }
            catch (Exception ex)
            {
                db.Rollback();
                throw;
            }

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

        private SQLiteCommand ReplaceService(CloudService service)
        {
            return db.CreateCommand(
                        $"replace into CloudControllers (Name, Token, CloudStamp) values ('{service.Name}', '{service.Token}', '{service.CloudStamp}');");
        }

        private SQLiteCommand UpdateService(CloudService service)
        {
            return db.CreateCommand(
                        $"update CloudControllers set Token = '{service.Token}' where CloudStamp = '{service.CloudStamp}');");
        }

        private SQLiteCommand RemoveService(CloudService service)
        {
            return db.CreateCommand(
                        $"delete from CloudControllers where CloudStamp = '{service.CloudStamp}' and Name = '{service.Name}';");
        }

        public void AddCloudService(CloudService service)
        {
            CreateConnection();
            db.BeginTransaction();
            try
            {
                var saveAllServicesCommand = ReplaceService(service);
                var result = saveAllServicesCommand.ExecuteNonQuery();

                db.Commit();
            }
            catch (Exception ex)
            {
                db.Rollback();
                throw;
            }
            finally
            {
                db.Close();
                db = null;
            }
        }

        public void RemoveCloudSevice(CloudService service)
        {
            CreateConnection();
            db.BeginTransaction();
            try
            {
                var saveAllServicesCommand = RemoveService(service);
                var result = saveAllServicesCommand.ExecuteNonQuery();

                db.Commit();
            }
            catch (Exception ex)
            {
                db.Rollback();
                throw;
            }
            finally
            {
                db.Close();
                db = null;
            }
        }

        public void UpdateCloudService(CloudService service)
        {
            CreateConnection();
            db.BeginTransaction();
            try
            {
                var saveAllServicesCommand = UpdateService(service);
                var result = saveAllServicesCommand.ExecuteNonQuery();

                db.Commit();
            }
            catch (Exception ex)
            {
                db.Rollback();
                throw;
            }
            finally
            {
                db.Close();
                db = null;
            }
        }
    }
}
