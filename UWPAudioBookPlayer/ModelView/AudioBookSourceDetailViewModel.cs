using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using GalaSoft.MvvmLight.Command;
using UWPAudioBookPlayer.DAL.Model;
using UWPAudioBookPlayer.Model;

namespace UWPAudioBookPlayer.ModelView
{
    public class AudioBookSourceDetailViewModel
    {
        public List<AudioBookFileDetailWithClouds> Files { get; set; }
        public AudioBookSource Book { get; set; }

        public bool IsShowDropBoxFiles { get; set; }
        public bool IsShowOneDriveFiles { get; set; }
        public bool IsShowOnlineFiles { get; set; }

        //public RelayCommand<AudioBookFileDetailWithClouds> UploadFile

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
