using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Net;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using Windows.Storage.Streams;
using Dropbox.Api;
using Dropbox.Api.Files;
using UWPAudioBookPlayer.Model;
using Newtonsoft.Json;
using UWPAudioBookPlayer.ModelView;
using System.Diagnostics;
using Windows.ApplicationModel.AppService;
using Dropbox.Api.Stone;
using Dropbox.Api.Users;
using UWPAudioBookPlayer.Controllers;

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
        public string CloudStamp { get; set; }
        public CloudType Type { get; set; }

        public override async Task<Tuple<string, IRandomAccessStream>> GetFileStream(string fileName)
        {
            byte[] bytes;
            if (string.IsNullOrWhiteSpace(fileName))
                return new Tuple<string, IRandomAccessStream>("", null);
            try
            {
                using (HttpClient client = new HttpClient())
                    bytes = await client.GetByteArrayAsync(fileName);
            }
            catch (Exception ex)
            {
                return new Tuple<string, IRandomAccessStream>("", null);
            }
            var stream = new MemoryStream(bytes);
            return new Tuple<string, IRandomAccessStream>("link", stream.AsRandomAccessStream());
        }
    }

    public struct FileChangedStruct
    {
        public string FolderName { get; set; }
        public string FileName { get; set; }
    }

    public struct AccountInfo
    {
        public bool IsAccountInfoAvaliable { get; set; }
        public string Name { get; set; }
        public bool IsNameAvaliable { get; set; }
        public string Email { get; set; }
        public bool IsEmailAvaliable { get; set; }
        public string UserPhotoUrl { get; set; }
        public bool IsUserPhotoUrlAvaliable { get; set; }

        public ulong UsedSpace { get; set; }
        public ulong AllSpace { get; set; }
        public bool IsFreeSpaceAvaliable { get; set; }
    }

    public class DropBoxController : ICloudController
    {
        public bool IsFailedToAuthenticate { get; } = false;
        public bool IsCloud => true;
        public string CloudStamp { get; private set; }
        public string AppCode { get; set; } = "kuld6fsmktlczbf";
        public string AppSercret { get; set; } = "ks3jyuotinwz2zz";
        public string Token { get; set; } = null;
        public string AppResponseUrl { get; set; } = @"https://www.dropbox.com/1/oauth2/redirect_receiver";
        public CloudType Type => CloudType.DropBox;
        public bool IsUseExternalBrowser => true;
        public string BaseFolder { get; set; } = @"/AudioBooks";
        public string MediaInfoFileName { get; set; } = "MediaInfo.json";


        public bool StoreUploadedRevisions { get; set; }
        private Dictionary<string, string> _uploadedFiles = new Dictionary<string, string>(10);
        public bool CanHandleSource(AudioBookSourceCloud source)
        {
            return CloudStamp == source.CloudStamp && IsAutorized;
        }

        public bool IsChangesObserveAvalible => true;
        public event EventHandler<FileChangedStruct> FileChanged;
        public event EventHandler<AudioBookSourceCloud> MediaInfoChanged;


        public bool IsAutorized => !string.IsNullOrWhiteSpace(Token) && client != null;

        public event EventHandler<Tuple<Uri, Action<Uri>>>  NavigateToAuthPage;
        public event EventHandler CloseAuthPage;

        private DropboxClient client;

        public async Task Inicialize()
        {
            if (!string.IsNullOrWhiteSpace(Token))
            {
                client = new DropboxClient(Token);
                var account = await client.Users.GetCurrentAccountAsync();
                Account = FullAccountInfo(account);
                CloudStamp = account.AccountId;
                try
                {
                    var folder = await client.Files.CreateFolderAsync(BaseFolder);
                }
                catch (ApiException<CreateFolderError> ex)
                {
                    
                }
                StartListenChanges();


            }
        }


        private AccountInfo FullAccountInfo(FullAccount info)
        {
            var result = new AccountInfo
            {
                Email = info.Email,
                IsEmailAvaliable = true,
                IsAccountInfoAvaliable = true,
                Name = info.Name.DisplayName,
                IsNameAvaliable = true,
                UserPhotoUrl = info.ProfilePhotoUrl,
                IsUserPhotoUrlAvaliable = info.ProfilePhotoUrl != null,
            };

            return result;
        }

        public AccountInfo Account { get; private set; }

        private async void GetAccountInfo()
        {
//            var space = await client.Users.GetSpaceUsageAsync();
//
//            if (space.Allocation.IsIndividual)
//            {
//                Account.IsFreeSpaceAvaliable = true;
//                Account.AllSpace = space.Allocation.AsIndividual.Value.Allocated;
//                Account.UsedSpace = space.Used;
//            }

        }

        private async Task StartListenChanges()
        {
            try
            {
                var cursor = await client.Files.ListFolderAsync(BaseFolder, true);
                string cursorS = cursor.Cursor;
                while (true)
                {
                    var res = await client.Files.ListFolderLongpollAsync(cursorS);
                    if (res.Changes)
                    {
                        ListFolderResult poll;
                        do
                        {
                            poll = await client.Files.ListFolderContinueAsync(cursorS);
                            cursorS = poll.Cursor;
                            ProcessChangeList(poll.Entries);
                        } while (poll.HasMore);
                    }
                    if (res.Backoff.HasValue)
                        await Task.Delay(new TimeSpan((long) res.Backoff.Value));
                }
            }
            catch (Exception ex)
            {
                await Task.Delay(TimeSpan.FromSeconds(10));
                StartListenChanges();
            }
        }

        private async Task ProcessChangeList(IList<Metadata> list)
        {
            string value;
            foreach (var metadata in list)
            {
                if (metadata.IsFile)
                {
                    var file = metadata.AsFile;
                    try
                    {
                        string strFolder = file.PathDisplay.Split(new []{ '/'}, StringSplitOptions.RemoveEmptyEntries)[1];
                        if (_uploadedFiles.TryGetValue(file.Id, out value) && value == file.Rev)
                            continue;
                        if (file.Name == MediaInfoFileName)
                            OnMediaInfoChanged(await GetAudioBookInfo(strFolder));
                        else
                            OnFileChanged(new FileChangedStruct() {FileName = file.Name, FolderName = strFolder});
                    }
                    catch (IndexOutOfRangeException ex)
                    {
                        Debug.WriteLine($"{ex.Message}\n{ex.StackTrace}");
                    }
                }
            }
        }

        public async Task Auth()
        {
            Uri authDrBox = DropboxOAuth2Helper.GetAuthorizeUri(OAuthResponseType.Code, AppCode,
               AppResponseUrl, disableSignup: true);
            var dialog = new DropBoxAuthDialog(authDrBox, AppResponseUrl, AppCode, AppSercret);
            var result = await dialog.ShowAsync();
            if (dialog.Result == ResultEnum.Authenticated)
            {
                Token = dialog.Token;
                await Inicialize();
                OnCloseAuthPage();

            }
            //OnNavigateToAuthPage(new Tuple<Uri, Action<Uri>>(authDrBox, Item2));
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
                await Inicialize();
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

        public async Task Uploadfile(AudioBookSourceWithClouds book, string fileName, Stream stream, string subPath = "")
        {
            //throw new NotImplementedException();
        }

        public async Task<bool> UploadBookMetadata(AudioBookSource source, string revision = null)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));
            //source.AccessToken = null;
            var fileName = MediaInfoFileName;
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

                try
                {
                    var metadata =
                        await
                            client.Files.UploadAsync(BaseFolder + "/" + source.Folder + "/" + fileName,
                                Dropbox.Api.Files.WriteMode.Overwrite.Instance, body: stream);

                    _uploadedFiles[metadata.Id] = metadata.Rev;
                }
                catch (ApiException<UploadError> ex)
                {
                    //Ignore
                    return false;
                }
                return true;
            }
        }

        public async Task Uploadbook(string BookName, string fileName, Stream stream)
        {
            try
            {
                var metadata =
                    await client.Files.UploadAsync(BaseFolder + "/" + BookName + "/" + fileName, body: stream);
                _uploadedFiles[metadata.Id] = metadata.Rev;
            }
            catch (ApiException<UploadError> ex)
            {
                throw;
            }
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
            //var result = new List<AudioBookSource>();
            //var folders = await client.Files.ListFolderAsync(BaseFolder);
            //var folder = folders.Entries.FirstOrDefault(f => String.Equals(f.Name, bookName, StringComparison.OrdinalIgnoreCase));
            //if (folder == null)
            //    return null;
            AudioBookSourceCloud book = new AudioBookSourceCloud(CloudStamp, Type);
            book.Name = bookName;
            book.Path = "DropBox\\" + bookName;
            book.Files = new List<AudiBookFile>();
            ListFolderResult files;
            try
            {
                files = await client.Files.ListFolderAsync(BaseFolder + "/" + bookName);
            }
            catch (ApiException<ListFolderError> e)
            {
                Debug.WriteLine($"{e.Message}\n{e.StackTrace}");
                return null;
            }
            AudioBookSourceCloud metaData = null;
            List<FileMetadata> images = new List<FileMetadata>();
            //string bookFolder = BaseFolder + "/" + bookName + "/";
            foreach (var filesEntry in files.Entries)
            {
                if (!filesEntry.IsFile)
                    continue;
                if (filesEntry.Name == MediaInfoFileName)
                {
                    IDownloadResponse<FileMetadata> data;
                    try
                    {

                        data = (await client.Files.DownloadAsync(filesEntry.PathDisplay));
                    }
                    catch (ApiException<DownloadError>)
                    {
                        metaData = null;
                        continue;
                    }
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
                if (this.IsImage(filesEntry.AsFile.Name))
                    images.Add(filesEntry.AsFile);
                else
                book.Files.Add(new AudiBookFile()
                {
                    Name = filesEntry.AsFile.Name,
                    Size = filesEntry.AsFile.Size,
                    IsAvalible = true
                });
            }
            var links = await Task.WhenAll(images.Select(async x => new {Link = await GetLink(bookName, x.Name), FileName = x.Name}));
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
                        metaData.Files[i].IsAvalible = true;
                    else
                        metaData.Files[i].IsAvalible = false;
                }
                metaData.Images = links.Select(x => new ImageStruct(x.FileName, x.Link)).ToArray();
                //metaData.Cover = links.FirstOrDefault();
                return metaData;

            }
            //result.Add(book);
            book.Images = links.Select(x => new ImageStruct(x.FileName, x.Link)).ToArray();
            //book.Cover = links.FirstOrDefault();
            book.CloudStamp = CloudStamp;
            book.Type = Type;
            return book;
        }

        public async Task<string> GetLink(string bookName, string fileName)
        {
            if (string.IsNullOrWhiteSpace(bookName) || string.IsNullOrWhiteSpace(fileName))
                return null;
            try
            {
                return (await client.Files.GetTemporaryLinkAsync(BaseFolder + "/" + bookName + "/" + fileName)).Link;
            }
            catch (Exception ex)
            {
                return null;
            }
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
            ListFolderResult folders;
            try
            {
                folders = await client.Files.ListFolderAsync(BaseFolder);
            }
            catch (Exception ex)
            {
                if (ex.Message == @"path/not_found/...")
                    return new List<AudioBookSourceCloud>(0);
                throw;
            }
            List<Task<AudioBookSourceCloud>> tasks = new List<Task<AudioBookSourceCloud>>(folders.Entries.Count);

            tasks.AddRange(folders.Entries.Select(folder => GetAudioBookInfo(folder.Name)));
            var totalResult = await Task.WhenAll(tasks);

            result.AddRange(totalResult.Where(x=>x != null).ToArray());
            return result;
        }

        public async Task DeleteAudioBook(AudioBookSource source)
        {
            if (!IsAutorized)
                return;

            try
            {
                var result = await client.Files.DeleteAsync(BaseFolder + "/" + source.Folder);
            }
            catch (Exception ex)
            {
                throw;
            }
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

        protected virtual void OnFileChanged(FileChangedStruct e)
        {
            FileChanged?.Invoke(this, e);
        }

        protected virtual void OnMediaInfoChanged(AudioBookSourceCloud e)
        {
            MediaInfoChanged?.Invoke(this, e);
        }
    }
}
