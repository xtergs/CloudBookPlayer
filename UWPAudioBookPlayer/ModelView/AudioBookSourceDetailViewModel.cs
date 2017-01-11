using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Windows.Storage.AccessCache;
using Windows.UI.Xaml.Media.Imaging;
using PropertyChanged;
using UWPAudioBookPlayer.Annotations;
using UWPAudioBookPlayer.DAL.Model;
using UWPAudioBookPlayer.Model;
using UWPAudioBookPlayer.Service;

namespace UWPAudioBookPlayer.ModelView
{
	[ImplementPropertyChanged]
	public class AudioBookSourceDetailViewModel : INotifyPropertyChanged
	{
		public ControllersService Controllers1 { get; }
		public ISettingsService Settings { get; set; }
		public AudioBookSourceFactory factory;
		public ObservableCollection<AudioBookFileDetailWithClouds> Files { get; set; }
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

		public delegate AudioBookSourceDetailViewModel Factory(AudioBookSource source);

		public AudioBookSourceDetailViewModel(AudioBookSource source, ControllersService controllers, ISettingsService settings)
		{
			Controllers1 = controllers;
			Settings = settings;
			Files = new ObservableCollection<AudioBookFileDetailWithClouds>();
			Book = source;
			
		}

		public bool Loading { get; set; }

		public async Task LoadCloudData()
		{
			Loading = true;
			try
			{
				var clouds = await Controllers1.GetBookInfoFromAllControllers(Book);
				var drCloud = clouds?.OfType<AudioBookSourceCloud>().FirstOrDefault(x => x.Type == CloudType.DropBox);
				var oneDrive = clouds?.OfType<AudioBookSourceCloud>().FirstOrDefault(x => x.Type == CloudType.OneDrive);
				var online = clouds?.OfType<OnlineAudioBookSource>().FirstOrDefault(x => x.Type == CloudType.Online);

				IsShowDropBoxFiles = drCloud != null;
				IsShowOneDriveFiles = oneDrive != null;
				IsShowOnlineFiles = online != null;

				for (int i = 0; i < Book.Files.Count; i++)
				{
					var newFile = new AudioBookFileDetailWithClouds()
					{
						File = Book.Files[i],
						IsLocalAvalible = false,
						IsDropBoxAvalible = false
					};
					{
						if (!(Book is AudioBookSourceCloud))
							newFile.IsLocalAvalible = Book.Files[i].IsAvalible;
						if (clouds != null)
						{
							newFile.IsDropBoxAvalible = drCloud?.AvalibleFiles.Any(x => x.Name == Book.Files[i].Name) ??
														false;
							newFile.IsOneDriveAvalible = oneDrive?.AvalibleFiles.Any(x => x.Name == Book.Files[i].Name) ??
														false;
							newFile.IsOnlineAvalible = online?.AvalibleFiles.Any(x => x.Name == Book.Files[i].Name) ??
														false;
						}
					}
					Files.Add(newFile);
				}
			}
			finally
			{
				OnPropertyChanged(nameof(Book));
				OnPropertyChanged(nameof(Files));
				Loading = false;
			}
		}

		public event PropertyChangedEventHandler PropertyChanged;

		[NotifyPropertyChangedInvocator]
		protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
		{
			PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
		}
	}
}
