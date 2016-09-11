using System;
using System.IO;
using System.Threading.Tasks;
using Windows.Storage;
using Newtonsoft.Json;
using UWPAudioBookPlayer.DAL.Model;
using UWPAudioBookPlayer.Model;

namespace UWPAudioBookPlayer.DAL
{
    public class JSonRepository : IDataRepository
    {
        public string FileName { get; set; } = "audioBooksStore.json_v1";
        public string RoamingFileName { get; set; } = "clouds.json_v1";
        private bool isBusy = false;

        public async Task Save<T>(StorageFolder folder, string fileName, T data)
        {
            var file =
                    await
                        folder.CreateFileAsync(fileName,
                            CreationCollisionOption.ReplaceExisting);
            string serialized = JsonConvert.SerializeObject(data);
            await FileIO.WriteTextAsync(file, serialized);
        }

        //public async Task SaveClouds(CloudService[] clouds)
        //{
        //    var file =
        //            await
        //                ApplicationData.Current.RoamingFolder.CreateFileAsync(RoamingFileName,
        //                    CreationCollisionOption.ReplaceExisting);
        //    string serialized = JsonConvert.SerializeObject(clouds);
        //    await FileIO.WriteTextAsync(file, serialized);
        //}

        //public async Task SaveLocalData(SaveModel books)
        //{
        //    books.CloudServices = null;
        //    var file =
        //            await
        //                ApplicationData.Current.LocalFolder.CreateFileAsync(FileName,
        //                    CreationCollisionOption.ReplaceExisting);
        //    string serialized = JsonConvert.SerializeObject(books);
        //    await FileIO.WriteTextAsync(file, serialized);
        //}

        public async Task Save(SaveModel books)
        {
            if (isBusy)
                return;
            isBusy = true;
            try
            {
                var clouds = books.CloudServices;
                books.CloudServices = null;
                await Task.WhenAll(Save(ApplicationData.Current.RoamingFolder, RoamingFileName,clouds), 
                    Save(ApplicationData.Current.LocalFolder, FileName ,books));
            }
            finally
            {
                isBusy = false;
            }
        }


        //public async Task<CloudService[]> LoadClouds()
        //{
        //    try
        //    {
        //        var file = await ApplicationData.Current.RoamingFolder.GetFileAsync(RoamingFileName);
        //        string json = await FileIO.ReadTextAsync(file);
        //        var data = JsonConvert.DeserializeObject<CloudService[]>(json);
        //        return data ?? new CloudService[0];
        //    }
        //    catch (JsonSerializationException e)
        //    {
        //        //version of file is different
        //        return new SaveModel();
        //    }
        //    catch (FileNotFoundException e)
        //    {
        //        return new SaveModel();
        //    }
        //}

        public async Task<T> Load<T>(StorageFolder folder,string fileName)
        {
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
            var allData = await Load<SaveModel>(ApplicationData.Current.LocalFolder, FileName) ?? new SaveModel();
            allData.CloudServices = clouds;
            return allData;
        }
    }
}
