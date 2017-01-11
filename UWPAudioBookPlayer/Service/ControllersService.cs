using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using PropertyChanged;
using UWPAudioBookPlayer.DAL;
using UWPAudioBookPlayer.DAL.Model;
using UWPAudioBookPlayer.Model;
using UWPAudioBookPlayer.ModelView;

namespace UWPAudioBookPlayer.Service
{
    [ImplementPropertyChanged]
    public class ControllersService
    {
        private readonly IControllersRepository repository;
        private readonly string fileName = "clouds.db_v2";
        public event EventHandler<FileChangedStruct> FileChanged;
        public event EventHandler<AudioBookSourceCloud> MediaInfoChanged;
        public event EventHandler AccountAlreadyAdded;
        public event EventHandler<ICloudController> ControllerDelted;

        public ImmutableList<ICloudController> Controllers { get; private set; } = ImmutableList<ICloudController>.Empty;
        public ICloudController[] Clouds => GetOnlyClouds();

        public async Task<bool> Load()
        {
            repository.RoamingFileName = fileName;
            repository.LocalFileName = null;
            var loaded = await repository.Load();
            await InicializeControllers(loaded.CloudServices);
            return true;
        }

        public ControllersService(IControllersRepository repository)
        {
            if (repository == null)
                throw new ArgumentNullException(nameof(repository));
            this.repository = repository;
        }

        public async Task InicializeControllers(CloudService[] services)
        {
            
            List<ICloudController> newControlelrs = new List<ICloudController>();
            foreach (var service in services)
            {
                var cloud = (GetCloudController(service));
                await cloud.Inicialize();
                if (cloud.IsChangesObserveAvalible)
                {
                    cloud.MediaInfoChanged += MediaInfoChanged;
                    cloud.FileChanged += FileChanged;
                }
                newControlelrs.Add(cloud);
            }
            newControlelrs.Add(new OnlineController("LibriVox"));

            Controllers = newControlelrs.ToImmutableList();
        }

        public ICloudController[] GetOnlyClouds()
        {
            return Controllers.Where(x => x.IsCloud).ToArray();
        }

        public ICloudController GetCloudController(CloudService service)
        {
            if (service.Name == "DropBox")
                return new DropBoxController()
                {
                    Token = service.Token,
                };
            if (service.Name == "OneDrive")
                return new OneDriveController()
                {
                    Token = service.Token
                };
            return null;
        }

        public ICloudController GetController(string cloudStamp)
        {
            return Controllers.FirstOrDefault(x => x.CloudStamp == cloudStamp);
        }

		public ICloudController GetContorller(AudioBookSource source)
		{
			var cloudSource = source as AudioBookSourceCloud;
			if (cloudSource == null)
				return null;
			return Controllers.FirstOrDefault(x => x.CanHandleSource(cloudSource));
		}

		public CloudService[] GetDataToSave()
        {
            return
                Controllers.Where(x => x.IsAutorized && x.IsCloud)
                    .Select(x => new CloudService() {Name = x.ToString(), Token = x.Token, CloudStamp = x.CloudStamp})
                    .ToArray();
        }

        public CloudService GetDataToSave(ICloudController controlelr)
        {
            return new CloudService() { Name = controlelr.ToString(), Token = controlelr.Token, CloudStamp = controlelr.CloudStamp };
        }

        public ICloudController GetCloudController(CloudType service)
        {
            switch (service)
            {
                case CloudType.DropBox:
                    return new DropBoxController();
                case CloudType.OneDrive:
                    return new OneDriveController();
                default:
                    break;
            }
            return null;
        }

        public bool IsExist(CloudType type)
        {
            return Controllers.Any(x => x.Type == type);
        }

        public async Task<ICloudController> AddNewController(CloudType type)
        {
            var cloudService = GetCloudController(type);
            await cloudService.Auth();
            if (!cloudService.IsAutorized)
                return null;
            if (Controllers.Any(x => x.CloudStamp == cloudService.CloudStamp))
            {
                OnAccountAlreadyAdded();
                return null;
            }
            Controllers = Controllers.Add(cloudService);
            repository.AddCloudService(GetDataToSave(cloudService));
            return cloudService;
        }

        protected virtual void OnAccountAlreadyAdded()
        {
            AccountAlreadyAdded?.Invoke(this, EventArgs.Empty);
        }

        public void RemoveController(ICloudController cloudController)
        {
            if (cloudController == null)
                return;
            Controllers = Controllers.Remove(cloudController);
            repository.RemoveCloudSevice(GetDataToSave(cloudController));
            OnControllerDelted(cloudController);
        }

        protected virtual void OnControllerDelted(ICloudController e)
        {
            ControllerDelted?.Invoke(this, e);
        }

        public async Task Save()
        {
//            var x = GetDataToSave();
//            repository.LocalFileName = null;
//            repository.RoamingFileName = fileName;
//
//            return repository.Save(new SaveModel()
//            {
//                CloudServices = x,
//            });
        }

        public void Update(ICloudController cloudController)
        {
            repository.UpdateCloudService(GetDataToSave(cloudController));
        }

	    public Task<AudioBookSourceCloud[]> GetBookInfoFromAllControllers(AudioBookSource book)
	    {
		    List<Task <AudioBookSourceCloud >> tasks = new List<Task<AudioBookSourceCloud>>(Controllers.Count);
		    foreach (var cloudController in Controllers)
		    {
			    tasks.Add(cloudController.GetAudioBookInfo(book));
		    }
		    return Task.WhenAll(tasks);
	    }
    }
}
