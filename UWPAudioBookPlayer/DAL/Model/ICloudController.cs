using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using UWPAudioBookPlayer.Model;
using UWPAudioBookPlayer.ModelView;

namespace UWPAudioBookPlayer.DAL.Model
{
    public interface ICloudController
    {
        bool IsCloud { get; }
        string CloudStamp { get; }
        CloudType Type { get; }
        bool IsUseExternalBrowser { get; }
        string BaseFolder { get; set; }
        bool IsAutorized { get; }
        string Token { get; set; }

        event EventHandler CloseAuthPage;
        event EventHandler<Tuple<Uri, Action<Uri>>> NavigateToAuthPage;

        void Auth();
        Task DeleteAudioBook(AudioBookSource source);
        Task<Stream> DownloadBookFile(string BookName, string fileName);
        Task<AudioBookSourceCloud> GetAudioBookInfo(string bookName);
        Task<AudioBookSourceCloud> GetAudioBookInfo(AudioBookSource book);
        Task<List<AudioBookSourceCloud>> GetAudioBooksInfo();
        Task<ulong> GetFreeSpaceBytes();
        Task<string> GetLink(string bookName, string fileName);
        Task<string> GetLink(AudioBookSourceCloud book, int fileNumber);
        Task Inicialize();
        Task Uploadbook(string BookName, string fileName, Stream stream);
        Task Uploadfile(AudioBookSourceWithClouds book, string fileName, Stream stream, string subPath = "");
        Task UploadBookMetadata(AudioBookSource source, string revision = null);

        bool IsChangesObserveAvalible { get; }
        event EventHandler<FileChangedStruct> FileChanged;
        event EventHandler<AudioBookSourceCloud> MediaInfoChanged;
    }

    public static class CloudControllerBase
    {
        
        public static readonly string[] ImageExtensions = new[] { ".jpg", ".png" };
        public static bool IsImage(this ICloudController controller, string fileName)
        {
            string ext = Path.GetExtension(fileName);
            return ImageExtensions.Any(x => x == ext);
        }
    }

}