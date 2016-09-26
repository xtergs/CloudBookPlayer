using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UWPAudioBookPlayer.Model;
using UWPAudioBookPlayer.ModelView;

namespace UWPAudioBookPlayer.DAL.Model
{
    class OnlineController : ICloudController
    {
        public bool IsCloud => false;
        public string CloudStamp { get; set; }
        public CloudType Type => CloudType.Online;
        public bool IsUseExternalBrowser => false;
        public string BaseFolder { get; set; }
        public bool IsAutorized => true;
        public string Token { get; set; }
        public event EventHandler CloseAuthPage;
        public event EventHandler<Tuple<Uri, Action<Uri>>> NavigateToAuthPage;

        public OnlineController(string cloudStamp)
        {
            if (string.IsNullOrWhiteSpace(cloudStamp))
                throw new ArgumentException(nameof(cloudStamp));
            CloudStamp = cloudStamp;
        }

        public void Auth()
        {
        }

        public async Task DeleteAudioBook(AudioBookSource source)
        {
            
        }

        public Task<Stream> DownloadBookFile(string BookName, string fileName)
        {
            throw new NotImplementedException();
        }

        public Task<AudioBookSourceCloud> GetAudioBookInfo(string bookName)
        {
            throw new NotImplementedException();
        }

        public async Task<AudioBookSourceCloud> GetAudioBookInfo(AudioBookSource book)
        {
            return (book as AudioBookSourceCloud) ??
                   (book as AudioBookSourceWithClouds)?.AdditionSources.FirstOrDefault(x => x is OnlineAudioBookSource)
                       as OnlineAudioBookSource;
        }

        public async Task<List<AudioBookSourceCloud>> GetAudioBooksInfo()
        {
            return new List<AudioBookSourceCloud>(0);
        }

        public Task<ulong> GetFreeSpaceBytes()
        {
            throw new NotImplementedException();
        }

        public Task<string> GetLink(string bookName, string fileName)
        {
            throw new NotImplementedException();
        }

        public async Task<string> GetLink(AudioBookSourceCloud book, int fileNumber)
        {
            return (book as OnlineAudioBookSource)?.Files[fileNumber].Path;
        }

        public async Task Inicialize()
        {
            
        }

        public async Task Uploadbook(string BookName, string fileName, Stream stream)
        {
        }

        public async Task UploadBookMetadata(AudioBookSource source, string revision = null)
        {
            
        }
    }
}
