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

        //public RelayCommand<AudioBookFileDetailWithClouds> UploadFile

        public AudioBookSourceDetailViewModel(AudioBookSource source, AudioBookSource clouds)
        {
            Files = new List<AudioBookFileDetailWithClouds>(source.Files.Count);
            Book = source;
            for (int i = 0; i < source.Files.Count; i++)
            {
                var newFile = new AudioBookFileDetailWithClouds()
                {
                    File = source.Files[i],
                    IsLocalAvalible = false,
                    IsDropBoxAvalible = false
                };
                if (source is AudioBookSourceCloud)
                {
                    newFile.IsDropBoxAvalible = source.Files[i].IsAvalible;
                }
                else
                {
                    newFile.IsLocalAvalible = source.Files[i].IsAvalible;
                    if (clouds != null)
                        newFile.IsDropBoxAvalible = clouds.AvalibleFiles.Any(x => x.Name == source.Files[i].Name);
                }
                Files.Add(newFile);
            }
        }
    }
}
