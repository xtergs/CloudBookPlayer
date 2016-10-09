using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Graph;
using Microsoft.OneDrive.Sdk;
using Microsoft.OneDrive.Sdk.Authentication;
using Newtonsoft.Json;
using UWPAudioBookPlayer.Model;
using UWPAudioBookPlayer.ModelView;

namespace UWPAudioBookPlayer.DAL.Model
{
    class OneDriveController : ICloudController
    {
        static class OneDriveErrors
        {
            public const string ItemNotFound = "itemNotFound";
        }
        public bool IsCloud => true;
        public string CloudStamp { get; private set; }
        private OneDriveClient client;

        private string ClientId = @"00000000401AEB0D";
        string mediaInfoFileName = "MediaInfo.json";

        public string AppResponseUrl { get; set; } = @"https://login.live.com/oauth20_desktop.srf";
        private readonly string oneDriveConsumerBaseUrl = "https://api.onedrive.com/v1.0";
        private readonly string[] scopes = new string[] { "onedrive.readwrite", "wl.signin", "wl.offline_access" };

        public CloudType Type => CloudType.OneDrive;
        public bool IsUseExternalBrowser => false;
        public string BaseFolder { get; set; } = @"/AudioBooks";
        public bool IsAutorized => !string.IsNullOrWhiteSpace(Token);
        public string Token { get; set; }
        public event EventHandler CloseAuthPage;
        public event EventHandler<Tuple<Uri, Action<Uri>>> NavigateToAuthPage;

        public OneDriveController()
        {
            
        }

        public async void Auth()
        {
            try
            {
                var msaAuthenticationProvider = new MsaAuthenticationProvider(ClientId, AppResponseUrl,
                    scopes);
                var authTask = msaAuthenticationProvider.AuthenticateUserAsync();
                client = new OneDriveClient(oneDriveConsumerBaseUrl, msaAuthenticationProvider);
                await authTask;
                var session = (((MsaAuthenticationProvider) client.AuthenticationProvider).CurrentAccountSession);
                Token = session.RefreshToken;
                CloudStamp = session.UserId;
                OnCloseAuthPage();
            }
            catch (Exception e)
            {
                
            }
        }

        public async Task DeleteAudioBook(AudioBookSource source)
        {
            try
            {
                await client.Drive.Root.ItemWithPath(BaseFolder + "/" + source.Folder).Request().DeleteAsync();
            }
            catch (ServiceException e)
            {
                if (e.Error.Code == OneDriveErrors.ItemNotFound)
                    return;
                throw;
            }
        }

        public async Task<Stream> DownloadBookFile(string BookName, string fileName)
        {
            try {
#if DEBUG
                var folder = (await client.Drive.Root.ItemWithPath(BaseFolder + "/" + BookName).Request().GetAsync());
                var file =
                    (await
                        client.Drive.Root.ItemWithPath(BaseFolder + "/" + BookName + "/" + fileName)
                            .Request()
                            .GetAsync());
                var file2 =
                    (await
                        client.Drive.Root.ItemWithPath(BaseFolder + "/" + BookName + "/" + fileName).Content
                            .Request()
                            .GetAsync());
#endif
                var stream =  (await client.Drive.Root.ItemWithPath(BaseFolder + "/" + BookName + "/" + fileName).Content.Request().GetAsync());
                return stream;
            }
            catch (ServiceException e)
            {
                if (e.Error.Code == OneDriveErrors.ItemNotFound)
                    return null;
                throw;
            }
        }

        private AudioBookSourceCloud StreamToSource(Stream stream)
        {
            byte[] buffer = new byte[stream.Length];
            var readed = stream.Read(buffer, 0, buffer.Length);
            var str = Encoding.UTF8.GetString(buffer);
            try
            {
                var res = JsonConvert.DeserializeObject<AudioBookSourceCloud>(str);
                return res;
            }
            catch (JsonSerializationException)
            {
                return null;
            }
        }

        public async Task<AudioBookSourceCloud> GetAudioBookInfo(string bookName)
        {
            

            var result = new List<AudioBookSource>();
            var folders = await client.Drive.Root.ItemWithPath(BaseFolder).Children.Request().GetAsync();
            var folder = folders.FirstOrDefault(x => String.Equals(x.Name, bookName, StringComparison.OrdinalIgnoreCase));
           if (folder == null)
                return null;
            AudioBookSourceCloud book = new AudioBookSourceCloud(CloudStamp,CloudType.OneDrive);
            book.Name = folder.Name;
            book.Path = "OneDrive\\" + folder.Name;
            book.Files = new List<AudiBookFile>();
            var files =
                await client.Drive.Root.ItemWithPath(BaseFolder + "/" + folder.Name).Children.Request().GetAsync();
            AudioBookSourceCloud metaData = null;
            foreach (var filesEntry in files)
            {
                if (filesEntry.File == null)
                    continue;
                if (filesEntry.Name == mediaInfoFileName)
                {
                    using (
                var stream =
                (await
                    client.Drive.Root.ItemWithPath(BaseFolder + "/" + folder.Name + "/" + filesEntry.Name ).Content
                        .Request()
                        .GetAsync()))
                    {
                        stream.Position = 0;
                        metaData= StreamToSource(stream);
                    }
                    if (metaData == null)
                        continue;
                    metaData.CloudStamp = CloudStamp;
                    metaData.Type = CloudType.OneDrive;
                    continue;
                }
                book.Files.Add(new AudiBookFile()
                {
                    Name = filesEntry.Name,
                    Size = (ulong)filesEntry.Size.Value,
                    IsAvalible = true
                });
            }
            if (metaData != null)
            {
                var diff = metaData.Files.Except(book.Files).ToList();
                for (int i = 0; i < diff.Count; i++)
                {
                    diff[i].IsAvalible = false;
                }

                for (int i = 0; i < metaData.Files.Count(); i++)
                {
                    var dbFile = book.Files.FirstOrDefault(f => f.Name == metaData.Files[i].Name);
                    if (dbFile != null)
                        metaData.Files[i] = dbFile;
                    else
                        metaData.Files[i].IsAvalible = false;
                }
                return metaData;

            }
            result.Add(book);
            book.CloudStamp = CloudStamp;
            book.Type = Type;
            return book;
        }

        public Task<AudioBookSourceCloud> GetAudioBookInfo(AudioBookSource book)
        {
            return GetAudioBookInfo(book.Folder);
        }

        public async Task<List<AudioBookSourceCloud>> GetAudioBooksInfo()
        {
            if (!IsAutorized)
                return new List<AudioBookSourceCloud>();
            var result = new List<AudioBookSourceCloud>();
            var folders = await client.Drive.Root.ItemWithPath(BaseFolder).Children.Request().GetAsync();
            foreach (var folder in folders)
            {
                var book = await GetAudioBookInfo(folder.Name);
                result.Add(book);
            }
            return result;
        }

        public async Task<ulong> GetFreeSpaceBytes()
        {
            return uint.MaxValue;
        }

        Regex regex  = new Regex(@"(https?:\/\/)?([\da-z\.-]+)\.([a-z\.]{2,6})([\/\w \.-?%&!]*)");

        public async Task<string> GetLink(string bookName, string fileName)
        {
            try
            {
                var link =
                    (client.Drive.Root.ItemWithPath(BaseFolder + "/" + bookName + "/" + fileName).CreateLink("embed"));
                var response = await link.Request().PostAsync();
                var match = regex.Match(response.Link.WebHtml);
                if (match.Success)
                    return match.Value.Replace("embed?", "download?");
                return "";
            }
            catch (ServiceException e)
            {
                if (e.Error.Code == OneDriveErrors.ItemNotFound)
                    return null;
                throw;
            }
        }

        public Task<string> GetLink(AudioBookSourceCloud book, int fileNumber)
        {
            try
            {
                return GetLink(book.Folder, book.Files[fileNumber].Name);
            }
            catch (ServiceException e)
            {
                if (e.Error.Code == OneDriveErrors.ItemNotFound)
                    return null;
                throw;
            }
        }

        public async Task Inicialize()
        {
            if (string.IsNullOrWhiteSpace(Token))
                return;

            AccountSession session = new AccountSession();
            session.ClientId = ClientId;
            session.RefreshToken = Token;
            var _msaAuthenticationProvider = new MsaAuthenticationProvider(ClientId, AppResponseUrl, scopes);
            client = new OneDriveClient(oneDriveConsumerBaseUrl, _msaAuthenticationProvider);
            _msaAuthenticationProvider.CurrentAccountSession = session;
            await _msaAuthenticationProvider.AuthenticateUserAsync();
            CloudStamp = _msaAuthenticationProvider.CurrentAccountSession.UserId;
        }

        public async Task Uploadbook(string BookName, string fileName, Stream stream)
        {
            await client.Drive.Root.ItemWithPath(BaseFolder + "/" + BookName + "/" + fileName).Content.Request().PutAsync<Item>(stream);
        }

        public async Task UploadBookMetadata(AudioBookSource source, string revision = null)
        {
            if (!IsAutorized)
                return;

           
            var str = JsonConvert.SerializeObject(source);
            var byted = Encoding.UTF8.GetBytes(str);
            using (var stream = new MemoryStream(byted, false))
            {
                stream.Position = 0;
               var data = await client.Drive.Root.ItemWithPath(BaseFolder + "/" + source.Folder + "/" + mediaInfoFileName).Content.Request().PutAsync<Item>(stream);

            }
        }

        public override string ToString()
        {
            return "OneDrive";
        }

        protected virtual void OnCloseAuthPage()
        {
            CloseAuthPage?.Invoke(this, EventArgs.Empty);
        }
    }
}
