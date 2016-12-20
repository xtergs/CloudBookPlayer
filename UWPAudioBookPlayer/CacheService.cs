using System;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.UI.Xaml.Media.Imaging;
using Microsoft.Toolkit.Uwp;

namespace UWPAudioBookPlayer
{
    public class CacheService
    {
        private static readonly object o = new object();

        public static CacheService Instance
        {
            get
            {
                if (instance != null)
                    return instance;
                lock (o)
                {
                    if (instance != null)
                        return instance;
                    instance = new CacheService();
                    return instance;
                }
            }
        }

        private static CacheService instance;

        private CacheService()
        {
            // Method intentionally left empty.
        }

        

        private StorageFolder folder = ApplicationData.Current.TemporaryFolder;
        public async Task<BitmapImage> GetImageForUrl(string url)
        {
            var hash = MD5.Create().ComputeHash(Encoding.UTF32.GetBytes(url));
            string strHash = Encoding.UTF32.GetString(hash);
            if (await folder.IsFileExistsAsync(strHash))
            {
                return await GetImageFromDisk(strHash);
            }
            using (HttpClient client = new HttpClient())
            {
                using (var strean = await client.GetStreamAsync(url))
                {
                    var file = await folder.CreateFileAsync(strHash, CreationCollisionOption.ReplaceExisting);
                    var image = new BitmapImage();
                    using (var strFile = (await file.OpenStreamForWriteAsync()))
                        await strean.CopyToAsync(strFile);
                }
                return await GetImageFromDisk(strHash);
            }
        }

        private async Task<BitmapImage> GetImageFromDisk(string name)
        {
            var file = await folder.GetFileAsync(name);
            var image = new BitmapImage();
            var openFiles = await file.OpenReadAsync();
            await image.SetSourceAsync(openFiles);
            return image;
        }
    }
}
