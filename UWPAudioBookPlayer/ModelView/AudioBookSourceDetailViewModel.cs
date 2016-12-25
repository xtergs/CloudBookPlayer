using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.Storage.AccessCache;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Media.Imaging;
using PropertyChanged;
using UWPAudioBookPlayer.DAL.Model;
using UWPAudioBookPlayer.Model;
using Binding = System.ServiceModel.Channels.Binding;

namespace UWPAudioBookPlayer.ModelView
{
    [ImplementPropertyChanged]
    public class AudioBookSourceDetailViewModel
    {
        public AudioBookSourceFactory factory;
        public List<AudioBookFileDetailWithClouds> Files { get; set; }
        public AudioBookSource Book { get; set; }

        public bool IsShowDropBoxFiles { get; set; }
        public bool IsShowOneDriveFiles { get; set; }
        public bool IsShowOnlineFiles { get; set; }

        //public RelayCommand<AudioBookFileDetailWithClouds> UploadFile

        public object Cover { get
            ;
            set;
        }

        public async Task<object> GetCover()
        {
            if (Book.Cover.IsValide)
            {
                if (Book.Cover.Url.StartsWith("http"))
                {
                    Cover =  Book.Cover.Url;
                    return Cover;
                }
                if (Book.Cover.IsValide)
                    Cover =  await LoadPicture(Book.Cover.Url);
                return Cover;

            }
            return null;
        }

        private async Task<object>  LoadPicture(string filename)
        {
            try
            {
                var folder = await StorageApplicationPermissions.FutureAccessList.GetFolderAsync(Book.AccessToken);
                var file = await folder.GetFileAsync(filename);
                BitmapImage image = new BitmapImage();
                await image.SetSourceAsync(await file.OpenReadAsync());
                return image;
            }
            catch (Exception e)
            {
                Debug.WriteLine($"{e.Message} \n{e.StackTrace}");
                return null;
            }

        }

        public AudioBookSourceDetailViewModel(AudioBookSource source, AudioBookSource[] clouds)
        {
            Files = new List<AudioBookFileDetailWithClouds>(source.Files.Count);
            Book = source;
            var drCloud = clouds?.OfType<AudioBookSourceCloud>().FirstOrDefault(x => x.Type == CloudType.DropBox);
            var oneDrive = clouds?.OfType<AudioBookSourceCloud>().FirstOrDefault(x => x.Type == CloudType.OneDrive);
            var online = clouds?.OfType<OnlineAudioBookSource>().FirstOrDefault(x => x.Type == CloudType.Online);

            IsShowDropBoxFiles = drCloud != null;
            IsShowOneDriveFiles = oneDrive != null;
            IsShowOnlineFiles = online != null;

            for (int i = 0; i < source.Files.Count; i++)
            {
                var newFile = new AudioBookFileDetailWithClouds()
                {
                    File = source.Files[i],
                    IsLocalAvalible = false,
                    IsDropBoxAvalible = false
                };
                {
                    if (!(source is AudioBookSourceCloud))
                        newFile.IsLocalAvalible = source.Files[i].IsAvalible;
                    if (clouds != null)
                    {
                        newFile.IsDropBoxAvalible = drCloud?.AvalibleFiles.Any(x => x.Name == source.Files[i].Name) ??
                                                    false;
                        newFile.IsOneDriveAvalible = oneDrive?.AvalibleFiles.Any(x => x.Name == source.Files[i].Name) ??
                                                    false;
                        newFile.IsOnlineAvalible = online?.AvalibleFiles.Any(x => x.Name == source.Files[i].Name) ??
                                                    false;
                    }
                }
                Files.Add(newFile);
            }
        }
    }
}
