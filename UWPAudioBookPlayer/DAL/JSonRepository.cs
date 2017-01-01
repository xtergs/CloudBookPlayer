using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Windows.Storage;
using Newtonsoft.Json;
using UWPAudioBookPlayer.DAL.Model;
using UWPAudioBookPlayer.Model;

namespace UWPAudioBookPlayer.DAL
{
    public class JSonRepository : IDataRepository
    {
        public string LocalFileName { get; set; } = "general.json_v1";
        public string RoamingFileName { get; set; } = "general.json_v1";
        private bool isBusy = false;

        private async Task Save<T>(StorageFolder folder, string fileName, T data)
        {
            if (fileName == null)
                return;
            var file =
                    await
                        folder.CreateFileAsync(fileName,
                            CreationCollisionOption.ReplaceExisting);
            string serialized = JsonConvert.SerializeObject(data);
            await FileIO.WriteTextAsync(file, serialized);
        }

        public async Task Save(SaveModel books)
        {
            if (isBusy)
                return;
            isBusy = true;
            try
            {
                var clouds = books.CloudServices;
                books.CloudServices = null;
                List<Task> tasks = new List<Task>(2)
                {
                    Save(ApplicationData.Current.LocalFolder, LocalFileName ,books)
                };
                if (clouds.Length > 0)
                    tasks.Add(Save(ApplicationData.Current.RoamingFolder, RoamingFileName, clouds));
                await Task.WhenAll(tasks);
            }
            finally
            {
                isBusy = false;
            }
        }

        public BookMark[] BookMarks(AudioBookSourceWithClouds book)
        {
            return book.BookMarks.ToArray();
        }

        public bool AddBookMark(AudioBookSourceWithClouds book, BookMark bookMark)
        {
            bookMark.Order = book.BookMarks.Count;
            book.BookMarks.Add(bookMark);
            return true;
        }

        public void UpdateBookMark(AudioBookSourceWithClouds playingSource, BookMark obj)
        {
            var already = playingSource.BookMarks.Find(p => p.Order == obj.Order);
            already.FileName = obj.FileName;
            already.Position = obj.Position;
            already.Title = obj.Title;
            already.Description = obj.Description;
        }

        public bool RemoveBookMark(BookMark bookMark, AudioBookSourceWithClouds audioBook)
        {
            audioBook.BookMarks.Remove(
                audioBook.BookMarks.FirstOrDefault(x => x.Order == bookMark.Order && x.Title == bookMark.Title));
            return true;
        }


        public async Task<T> Load<T>(StorageFolder folder,string fileName)
        {
            if (fileName == null)
                return default(T);
            try
            {
                var file = await folder.GetFileAsync(fileName);
                string json = await FileIO.ReadTextAsync(file);
                var data = JsonConvert.DeserializeObject<T>(json);
                return data;
            }
            catch (JsonSerializationException e)
            {
                //version of file is different
                return default(T);
            }
            catch (FileNotFoundException e)
            {
                return default(T);
            }
        }

        public async Task<SaveModel> Load()
        {
            var clouds = await Load<CloudService[]>(ApplicationData.Current.RoamingFolder,RoamingFileName) ?? new CloudService[0];
            var allData = await Load<SaveModel>(ApplicationData.Current.LocalFolder, LocalFileName) ?? new SaveModel();
            allData.CloudServices = clouds;
            return allData;
        }
    }
}
