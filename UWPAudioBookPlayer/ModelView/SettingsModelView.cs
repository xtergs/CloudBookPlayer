using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using Windows.ApplicationModel;
using Windows.UI;
using GalaSoft.MvvmLight.Command;
using Microsoft.Toolkit.Uwp;
using PropertyChanged;
using UWPAudioBookPlayer.Annotations;
using UWPAudioBookPlayer.DAL.Model;
using UWPAudioBookPlayer.Service;
using System.Linq;
using GoogleAnalytics.Core;
using UWPAudioBookPlayer.Helper;

namespace UWPAudioBookPlayer.ModelView
{
	[ImplementPropertyChanged]
	public class SettingsModelView : ISettingsService, ITimerSettings, INotifyPropertyChanged
	{
		private IApplicationSettingsHelper helper;
		private readonly INotification _notification;
		private readonly ControllersService _controlersService;

		public SettingsModelView(IApplicationSettingsHelper helper, INotification notification, ControllersService controlersService)
		{
			if (helper == null)
				throw new ArgumentNullException(nameof(helper));
			this.helper = helper;
			_notification = notification;
			_controlersService = controlersService;
			RefreshCloudControllerCommand = new RelayCommand<ICloudController>(RefreshCloudController);

			AddCloudAccountCommand = new RelayCommand<CloudType>(AddDropBoxAccount);
			RemoveCloudController = new RelayCommand<ICloudController>(RemoveCloudAccountAsync);
		}

		private Tracker Tracker => GoogleAnalytics.EasyTracker.GetTracker();

		public RelayCommand<CloudType> AddCloudAccountCommand { get; private set; }
		public async void AddDropBoxAccount(CloudType type)
		{
			if (ControlersService.IsExist(type))
			{
				if (
					await
						_notification.ShowMessage("Already have this type",
							"You already added this type of cloud service. Are want add another?",
							ActionButtons.Cancel, ActionButtons.Ok) != ActionButtons.Ok)
					return;
			}
			var cloudService = await ControlersService.AddNewController(type);
			if (cloudService == null)
				return;
			Tracker.SendEvent(nameof(SettingsModelView), "AddCloudAccount", type.ToString(), 0);
		}

		private async void RemoveCloudAccountAsync(ICloudController obj)
		{
			if (obj == null)
				return;
			ControlersService.RemoveController(obj);
			Tracker.SendEvent(nameof(SettingsModelView), "RemoveCloudAccountAsync", obj.ToString(), 0);
		}

		private async void RefreshCloudController(ICloudController obj)
		{
			await obj.Auth();
			if (obj.IsFailedToAuthenticate)
			{
				await _notification.ShowMessage("Failed", "Account hasn't been authorized");
			}
			else
			{
				ControlersService.Update(obj);
			}
		}

		public void DestroyViewModel()
		{
			helper = null;
		}

		public bool AutomaticaliDeleteFilesFromDrBox
		{
			get { return helper.SimpleGet(false); }
			set { helper.SimpleSet(value); }
		}

		public bool AskBeforeDeletionBook
		{
			get { return helper.SimpleGet(true); }
			set { helper.SimpleSet(value); }
		}

		public int DefaultMaxLengthHistory => 10;

		public int MaxLengthHistory
		{
			get { return helper.SimpleGet(DefaultMaxLengthHistory); }
			set { helper.SimpleSet(value); }
		}

		public string Changelog => $"Changelog: {Environment.NewLine}" +
								   $"1) Handling no internet connection/connection failour{Environment.NewLine}" +
								   $"2) UI for LibriVox{Environment.NewLine}" +
								   $"3) Added sleep timer{Environment.NewLine}" +
								   $"4) Always show actual cloud accounts in settigns{Environment.NewLine}" +
								   $"5) Now storing cloud credentions in sqlite, need to reauthenticate accounts{Environment.NewLine}" +
								   $"6) If authentication of onedrive account is failed, show tip and you can refresh by clicking button Refresh{Environment.NewLine}" +
								   $"7) Fixed animation, now you can close cast screen by clicking back button{Environment.NewLine}" +
								   $"8) Correct way to add DropBox account{Environment.NewLine}";

		public string ChanglogShowedForVersion
		{
			get { return helper.SimpleGet<string>(null); }
			set { helper.SimpleSet(value); }
		}

		public bool StartInCompactMode
		{
			get { return helper.SimpleGet(true); }
			set { helper.SimpleSet(value); }
		}

		public static string GetAppVersion()
		{
			try
			{
				Package package = Package.Current;
				PackageId packageId = package.Id;
				PackageVersion version = packageId.Version;

				return string.Format("{0}.{1}.{2}.{3}", version.Major, version.Minor, version.Build, version.Revision);
			}
			catch (Exception e)
			{
				Debug.WriteLine($"{e.Message}\n{e.StackTrace}");
				return "";
			}
		}

		public string SavedVersion
		{
			get { return helper.SimpleGet(""); }
			set { helper.SimpleSet(value); }
		}

		public bool ShowBooksList
		{
			get { return helper.SimpleGet(true); }
			set { helper.SimpleSet(value); }
		}

		public bool IsDevelopMode { get; set; } = true;

		public bool IsShowBackgroundImage { get; set; } = true;
		//{
		//    get { return helper.SimpleGet(true); }
		//    set { helper.SimpleSet(value); }
		//}

		public bool IsShowPlayingBookImage { get; set; } = true;
		//{
		//    get { return helper.SimpleGet(true); }
		//    set { helper.SimpleSet(value); }
		//}

		public bool IsBlurBackgroundImage { get; set; } = true;
		//{
		//    get { return helper.SimpleGet(true); }
		//    set { helper.SimpleSet(value); }
		//}

		public int ValueToBlurBackgroundImage { get; set; } = 50;
		//{
		//    get { return helper.SimpleGet(10); }
		//    set { helper.SimpleSet(value); }
		//}

		public float BlurControlPanel { get; set; } = 0;
		//{
		//    get { return helper.SimpleGet(10f); }
		//    set { helper.SimpleSet(value); }
		//}
		public bool BlurOnlyOverImage { get; set; } = false;
		//{
		//    get { return helper.SimpleGet(false); }
		//    set { helper.SimpleSet(value); }
		//}
		public bool FillBackgroundEntireWindow { get; set; } = false;
		//{
		//    get { return helper.SimpleGet(false); }
		//    set { helper.SimpleSet(value); }
		//}

		public Color ColorOfUserControlBlur
		{
			get { return Color.FromArgb(64, 255, 255, 255); }
			set { helper.SimpleSet(value.ToInt()); }
		}

		public double OpacityUserBlur { get; set; } = 100;
		//{
		//    get { return helper.SimpleGet(100); }
		//    set { helper.SimpleSet(value); }
		//}


		public string ChangeLogOnce
		{
			get
			{
				var version = GetAppVersion().Trim().ToLower();
				if (version != SavedVersion)
				{
					SavedVersion = version;
					return Changelog;
					;
				}
				return "";
			}
		}

		public string ListDataTemplate
		{
			get { return helper.SimpleGet("TilesDataTemplate"); }
			set { helper.SimpleSet(value); }
		}

		public RelayCommand<ICloudController> RemoveCloudController { get; }
		public RelayCommand<ICloudController> RefreshCloudControllerCommand { get; }

		public ListDataTemplateStruct[] AvaliableListDataTemplages { get; }
			= new[] { new ListDataTemplateStruct() { Value = "TilesDataTemplate", HumanValue = "Tiles", IsWrapItems=true },
					  new ListDataTemplateStruct() { Value =  "DetailDataTemplate", HumanValue = "Details" , IsWrapItems = false} };

		public ListDataTemplateStruct SelectedListDataTemplate
		{
			get
			{
				return AvaliableListDataTemplages.First(x => x.Value == ListDataTemplate);
			}
			set
			{
				ListDataTemplate = value.Value;
			}
		}

		public bool IsWrapListItems => SelectedListDataTemplate.IsWrapItems;

		public string StandartCover
		{
			get
			{
				if (!UseStandartCover && CustomeCoverName != null)
				{
					return "ms-appdata:///local/" + CustomeCoverName;
				}
				var debug = helper.SimpleGet("ms-appx:///Image/no-image-available.jpg");
				return debug;
			}

			set { helper.SimpleSet(value); }
		}

		public string[] AvaliableStandartCovers { get; } = new[] { "no-image-available.jpg", "HDD.png", "DropBoxLogo.png" }.Select(x => "ms-appx:///Image/" + x).ToArray();

		public string CustomeCoverName
		{
			get { return helper.SimpleGet<string>(null); }
			set { helper.SimpleSet(value); }
		}
		public bool UseStandartCover
		{
			get { return helper.SimpleGet(true); }
			set { helper.SimpleSet(value); }
		}

		public ControllersService ControlersService
		{
			get { return _controlersService; }
		}

		public void NotifyCustomeImageChanged()
		{
			OnPropertyChanged(nameof(StandartCover));
		}

		public event PropertyChangedEventHandler PropertyChanged;

		[NotifyPropertyChangedInvocator]
		protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
		{
			PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
		}

		public int TimerMinutes
		{
			get { return helper.SimpleGet(DefaultTimerMinutes); }
			set { helper.SimpleSet(value); }
		}
		public int DefaultTimerMinutes { get; } = 10;
		public bool IsActive {
			get { return helper.SimpleGet(false); }
			set { helper.SimpleSet(value); }
		}
	}
}
