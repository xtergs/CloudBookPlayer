using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UWPAudioBookPlayer.DAL;
using UWPAudioBookPlayer.DAL.Model;
using UWPAudioBookPlayer.Model;
using UWPAudioBookPlayer.ModelView;

namespace UWPAudioBookPlayer.Service
{
    public class ManageSources
    {
        private readonly IDataRepository repository;
        private readonly string fileName = "audioBooksStore.json_v2";
        public ObservableAudioBooksCollection<AudioBookSourceWithClouds> Folders { get; private set; }
        public AudioBookSourceWithClouds[] LocalFolders => Folders.Where(x => !(x is AudioBookSourceCloud)).ToArray();

        public ManageSources(IDataRepository repository)
        {
            if (repository == null)
                throw new ArgumentNullException(nameof(repository));
            this.repository = repository;
            Folders = new ObservableAudioBooksCollection<AudioBookSourceWithClouds>();
        }

        public ICloudController[] GetControllersForDownload(AudioBookSourceWithClouds book,
            IEnumerable<ICloudController> controllers)
        {
            return GetSupportedControllers(book, controllers.ToArray());
        }

        public ICloudController[] GetControllersForUpload(AudioBookSourceWithClouds book,
            IEnumerable<ICloudController> controllers)
        {
            if (book is AudioBookSourceCloud)
                return new ICloudController[0];
            return controllers.Where(x => x != null && x.IsCloud).ToArray();
        }

        private ICloudController[] GetSupportedControllers(AudioBookSourceWithClouds book,
            ICloudController[] controllers)
        {
            AudioBookSourceCloud loudData = book as AudioBookSourceCloud;
            if (loudData == null)
                return new ICloudController[0];
            var llist = new[] {loudData}.Union(book.AdditionSources?.OfType<AudioBookSourceCloud>());
            ICloudController[] louds =
                controllers.Join(llist, controller => controller.CloudStamp, cloud => cloud.CloudStamp,
                    (controller, cloud) => controller).ToArray();

            return louds;
        }

        public AudioBookSourceWithClouds GetSource(string bookName)
        {
            return Folders.FirstOrDefault(f => f.Name == bookName);
        }

        public void RemoveByCloudStamp(string cloudStamp)
        {
            foreach (
                var folder in
                    Folders.OfType<AudioBookSourceCloud>().Where(x => x.CloudStamp == cloudStamp).ToArray())
            {
                Folders.Remove(folder);
            }
        }

        public void AddSource(AudioBookSourceWithClouds audioBookSourceWithClouds)
        {
            if (audioBookSourceWithClouds == null)
                throw new ArgumentNullException(nameof(audioBookSourceWithClouds));

            var source = audioBookSourceWithClouds;
            var inFolders = Folders.FirstOrDefault(x => x.Name == source.Name);
            if (inFolders == null)
            {
                if (source.AvalibleCount > 0)
                    Folders.Add(source);
                return;
            }

            var fromCloud = inFolders as AudioBookSourceCloud;
            //Same entry from cloud already persist
            if (fromCloud != null && fromCloud.CloudStamp == (source as AudioBookSourceCloud)?.CloudStamp)
            {
                fromCloud.Files = MergeFilesLists(source.Files, fromCloud.Files);
                return;
            }
            var isCloud = source as AudioBookSourceCloud;
            if (isCloud != null)
            {
                if (
                    inFolders.AdditionSources.OfType<AudioBookSourceCloud>()
                        .Any(
                            s =>
                                s.CloudStamp == isCloud.CloudStamp &&
                                s.CreationDateTimeUtc == isCloud.CreationDateTimeUtc &&
                                s.ModifiDateTimeUtc == isCloud.ModifiDateTimeUtc))
                    return;
                var old = inFolders.AdditionSources.OfType<AudioBookSourceCloud>()
                    .Where(s => s.CloudStamp == isCloud.CloudStamp)
                    .ToList();
                foreach (var s in old)
                    inFolders.AdditionSources.Remove(s);
            }
            if (inFolders.CreationDateTimeUtc > source.CreationDateTimeUtc)
            {
                UpdateAudioBookWithClouds(inFolders, source);

            }
            else if (inFolders.CreationDateTimeUtc < source.CreationDateTimeUtc)
            {
                var tempLocal = source;
                UpdateAudioBookWithClouds(source, inFolders);
                //tasks.Add(controller.UploadBookMetadata(inFolders));
            }
            else if (inFolders.ModifiDateTimeUtc < source.ModifiDateTimeUtc)
            {
                UpdateAudioBookWithClouds(inFolders, source);
            }
            else if (inFolders.ModifiDateTimeUtc > source.ModifiDateTimeUtc)
            {
                UpdateAudioBookWithClouds(source, inFolders);
                //                    var tempLocal = f;
                //                    tasks.Add(controller.UploadBookMetadata(inFolders));
            }
            if (source.AvalibleCount <= 0)
                return;
            if (source is AudioBookSourceCloud)
                inFolders.AdditionSources.Add(source);
            else
            {
                source.AdditionSources.Add(inFolders);
                foreach (var f in inFolders.AdditionSources)
                    source.AdditionSources.Add(f);
                Folders.Remove(inFolders);
                Folders.Add(source);
            }
        }

        private void UpdateAudioBookWithClouds(AudioBookSourceWithClouds dest, AudioBookSourceWithClouds source)
        {
            dest.Position = source.Position;
            dest.Images = source.Images;
            dest.CurrentFile = source.CurrentFile;
            dest.ExternalLinks = source.ExternalLinks;
            dest.IsLocked = source.IsLocked;
            dest.PlaybackRate = source.PlaybackRate;
            dest.Files = MergeFilesLists(source.Files, dest.Files);
            dest.ModifiDateTimeUtc = source.ModifiDateTimeUtc;
            dest.CreationDateTimeUtc = source.CreationDateTimeUtc;
        }

        public void AddSources(AudioBookSourceWithClouds[] soures)
        {
            if (soures == null)
                return;
            foreach (var source in soures)
            {
                AddSource(source);
            }
        }

        List<AudiBookFile> MergeFilesLists(List<AudiBookFile> newList, List<AudiBookFile> oldList)
        {
            for (int i = 0; i < newList.Count; i++)
            {
                newList[i].IsAvalible = oldList.Any(x => x.Name == newList[i].Name && x.IsAvalible);
            }
            return newList;
        }

        internal void AddSource(AudioBookSourceWithClouds originalSelectedFolder,
            AudioBookSourceWithClouds tempSelectedFolder)
        {
            var index = Folders.IndexOf(tempSelectedFolder);
            if (index >= 0)
            {
                Folders.Insert(index, originalSelectedFolder);
                Folders.Remove(tempSelectedFolder);
            }
            else
                Folders.Add(originalSelectedFolder);
        }

        public AudioBookSourceWithClouds RemoveByName(string name)
        {
            var contains = Folders.FirstOrDefault(f => f.Name == name);
            if (contains == null)
                return null;
            Folders.Remove(contains);
            return contains;
        }

        public AudioBookSourceWithClouds GetFromLocalByFolder(string name)
        {
            return LocalFolders.FirstOrDefault(x => x.Folder == name);
        }

        public void Remove(AudioBookSourceWithClouds alreadyBook)
        {
            Folders.Remove(alreadyBook);
        }

        public Task Save()
        {
            var modelToSave = new SaveModel()
            {
                AudioBooks =
                    Folders.Where(
                        x => !string.IsNullOrWhiteSpace(x.AccessToken) &&
                             x.GetType() == typeof (AudioBookSourceWithClouds)).ToArray(),
                OnlineBooks =
                    Folders.Concat(Folders.SelectMany(x => x.AdditionSources)).OfType<OnlineAudioBookSource>().ToArray(),
            };
            repository.LocalFileName = fileName;
            repository.RoamingFileName = null;
            return repository.Save(modelToSave);
        }

        public async Task<bool> Load()
        {
            repository.LocalFileName = fileName;
            repository.RoamingFileName = null;
            var loaded = await repository.Load();
            Folders =
                    new ObservableAudioBooksCollection<AudioBookSourceWithClouds>();
            AddSources(loaded.AudioBooks.Select(x =>
            {
                x.IgnoreTimeOfChanges = false;
                return x;
            }).ToArray());
            AddSources(loaded.OnlineBooks.Select(x =>
            {
                x.CloudStamp = "LibriVox";
                x.Type = CloudType.Online;
                x.IgnoreTimeOfChanges = false;
                return x;
            }).OfType<AudioBookSourceWithClouds>().ToArray());
            return true;
        }
    }
}
