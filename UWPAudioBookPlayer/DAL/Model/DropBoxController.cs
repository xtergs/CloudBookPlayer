using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Windows.Storage.Streams;
using Dropbox.Api;
using Dropbox.Api.Files;
using UWPAudioBookPlayer.Model;
using Newtonsoft.Json;
using UWPAudioBookPlayer.ModelView;

namespace UWPAudioBookPlayer.DAL.Model
{
    public class AudioBookSourceCloud : AudioBookSourceWithClouds
    {
        public AudioBookSourceCloud(string cloudStamp, CloudType type)
        {
            this.CloudStamp = cloudStamp;
            this.Type = type;
        }
        [JsonIgnore]
        public string Revision { get; set; }
        [JsonIgnore]
        public string CloudStamp { get; set; }
        [JsonIgnore]
        public CloudType Type { get; set; }

        public override async Task<Tuple<string, IRandomAccessStream>> GetFileStream(string fileName)
        {
            byte[] bytes;
            if (string.IsNullOrWhiteSpace(fileName))
                return new Tuple<string, IRandomAccessStream>("", null);
            using (HttpClient client = new HttpClient())
                bytes = await client.GetByteArrayAsync(fileName);
            var stream = new MemoryStream(bytes);
            return new Tuple<string, IRandomAccessStream>("link", stream.AsRandomAccessStream());
        }
    }
    public class DropBoxController : ICloudController
    {
        public bool IsCloud => true;
        public string CloudStamp { get; private set; }
        public string AppCode { get; set; } = "kuld6fsmktlczbf";
        public string AppSercret { get; set; } = "ks3jyuotinwz2zz";
        public string Token { get; set; } = null;
        public string AppResponseUrl { get; set; } = @"https://www.dropbox.com/1/oauth2/redirect_receiver";
        public CloudType Type => CloudType.DropBox;
        public bool IsUseExternalBrowser => true;
        public string BaseFolder { get; set; } = @"/AudioBooks";

        public bool IsAutorized => !string.IsNullOrWhiteSpace(Token) && client != null;

        public event EventHandler<Tuple<Uri, Action<Uri>>>  NavigateToAuthPage;
        public event EventHandler CloseAuthPage;

        private DropboxClient client;

        public async Task Inicialize()
        {
            if (!string.IsNullOrWhiteSpace(Token))
            {
                client = new DropboxClient(Token);
                CloudStamp = (await client.Users.GetCurrentAccountAsync()).AccountId;
            }
        }

        public void Auth()
        {
            Uri authDrBox = DropboxOAuth2Helper.GetAuthorizeUri(OAuthResponseType.Code, AppCode,
               AppResponseUrl, disableSignup: true);
            OnNavigateToAuthPage(new Tuple<Uri, Action<Uri>>(authDrBox, Item2));
        }

        private async void Item2(Uri uri)
        {
            if (uri.OriginalString.Contains(@"error=access_denied"))
            {
                OnCloseAuthPage();
            }
            if (uri.OriginalString.Contains(AppResponseUrl))
            {
                var resp = await DropboxOAuth2Helper.ProcessCodeFlowAsync(uri, AppCode,AppSercret, AppResponseUrl);
                Token = resp.AccessToken;
                client = new DropboxClient(Token);
                CloudStamp = (await client.Users.GetCurrentAccountAsync()).AccountId;
                OnCloseAuthPage();
            }
        }

        protected virtual void OnNavigateToAuthPage(Tuple<Uri, Action<Uri>> e)
        {
            NavigateToAuthPage?.Invoke(this, e);
        }

        protected virtual void OnCloseAuthPage()
        {
            CloseAuthPage?.Invoke(this, EventArgs.Empty);
        }

        public Task UploadBookMetadata(AudioBookSource source, string revision = null)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));
            //source.AccessToken = null;
            var fileName = "MediaInfo.json";
            var str = JsonConvert.SerializeObject(source);
            var byted = Encoding.UTF8.GetBytes(str);
            using (var stream = new MemoryStream(byted,false))
            {
                stream.Position = 0;
                WriteMode.Update writeMode;
                if (revision != null)
                    writeMode = new WriteMode.Update(revision);
                else
                    writeMode = null;

                return client.Files.UploadAsync(BaseFolder + "/" + source.Folder + "/" + fileName, Dropbox.Api.Files.WriteMode.Overwrite.Instance, body: stream);
            }
        }

        public Task Uploadbook(string BookName, string fileName, Stream stream)
        {
            return client.Files.UploadAsync(BaseFolder + "/" + BookName + "/" + fileName, body: stream);
        }

        public async Task<Stream> DownloadBookFile(string BookName, string fileName)
        {
            return await
                (await client.Files.DownloadAsync(BaseFolder + "/" + BookName + "/" + fileName)).GetContentAsStreamAsync
                    ();
        }

        public Task<AudioBookSourceCloud> GetAudioBookInfo(AudioBookSource book)
        {
            return GetAudioBookInfo(book.Folder);
        }

        public async Task<AudioBookSourceCloud> GetAudioBookInfo(string bookName)
        {
            if (!IsAutorized)
                return null;
            var result = new List<AudioBookSource>();
            var folders = await client.Files.ListFolderAsync(BaseFolder);
            var folder = folders.Entries.FirstOrDefault(f => String.Equals(f.Name, bookName, StringComparison.OrdinalIgnoreCase));
            if (folder == null)
                return null;
            AudioBookSourceCloud book = new AudioBookSourceCloud(CloudStamp, Type);
            book.Name = folder.Name;
            book.Path = "DropBox\\" + folder.Name;
            book.Files = new List<AudiBookFile>();
            var files = await client.Files.ListFolderAsync(BaseFolder + "/" + folder.Name, includeMediaInfo: true);
            AudioBookSourceCloud metaData = null;
            foreach (var filesEntry in files.Entries)
            {
                if (!filesEntry.IsFile)
                    continue;
                if (filesEntry.Name == "MediaInfo.json")
                {
                    var data =
                        (await client.Files.DownloadAsync(BaseFolder + "/" + folder.Name + "/" + filesEntry.Name));
                    var config = (await data
                        
                            .GetContentAsStringAsync());
                    try
                    {
                        metaData = JsonConvert.DeserializeObject<AudioBookSourceCloud>(config);
                        metaData.Revision = data.Response.Rev;
                        metaData.Type = CloudType.DropBox;
                        metaData.CloudStamp = CloudStamp;
                    }
                    catch (JsonSerializationException e)
                    {
                        //Not consistent version
                        continue;
                    }
                    continue;
                }
                book.Files.Add(new AudiBookFile()
                {
                    Name = filesEntry.AsFile.Name,
                    Size = filesEntry.AsFile.Size,
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
            return book;
        }

        public async Task<string> GetLink(string bookName, string fileName)
        {
            if (string.IsNullOrWhiteSpace(bookName) || string.IsNullOrWhiteSpace(fileName))
                return null;
            return (await client.Files.GetTemporaryLinkAsync(BaseFolder + "/" + bookName + "/" + fileName)).Link;
        }

        public Task<string> GetLink(AudioBookSourceCloud book, int fileNumber)
        {
            return GetLink(book.Folder, book.Files[fileNumber].Name);
        }

        public async Task<List<AudioBookSourceCloud>>  GetAudioBooksInfo()
        {
            if (!IsAutorized)
                return new List<AudioBookSourceCloud>();
            var result = new List<AudioBookSourceCloud>();
            var folders = await client.Files.ListFolderAsync(BaseFolder);
            foreach (var folder in folders.Entries)
            {
                var book = await GetAudioBookInfo(folder.Name);
                result.Add(book);
            }
            return result;
        }

        public async Task DeleteAudioBook(AudioBookSource source)
        {
            if (!IsAutorized)
                return;

            var result = await client.Files.DeleteAsync(BaseFolder + "/" + source.Folder);
        }

        public async Task<ulong> GetFreeSpaceBytes()
        {
            if (!IsAutorized)
                return 0;

            var space = await client.Users.GetSpaceUsageAsync();
            if (space.Allocation.IsIndividual)
                return space.Allocation.AsIndividual.Value.Allocated - space.Used;
            return 0;
        }

        public override string ToString()
        {
            return "DropBox";
        }
    }
}
