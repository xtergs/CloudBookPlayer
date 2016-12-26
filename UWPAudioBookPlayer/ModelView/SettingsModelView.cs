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

namespace UWPAudioBookPlayer.ModelView
{
    [ImplementPropertyChanged]
    public class SettingsModelView : ISettingsService, INotifyPropertyChanged
    {
        private IApplicationSettingsHelper helper;
        private MainControlViewModel mainViewModel;
        private readonly ControllersService _controlersService;

        public SettingsModelView(IApplicationSettingsHelper helper, MainControlViewModel mainViewModel, ControllersService controlersService)
        {
            if (helper == null)
                throw new ArgumentNullException(nameof(helper));
            this.helper = helper;
            if (mainViewModel == null)
                throw new ArgumentNullException(nameof(mainViewModel));
            this.mainViewModel = mainViewModel;
            _controlersService = controlersService;
           // this.mainViewModel.PropertyChanged += MainViewModel_PropertyChanged;

            RemoveCloudController = new RelayCommand<ICloudController>((controller) =>
            {
                if (controller == null)
                    return;
                this.mainViewModel.RemoveCloudAccountCommand.Execute(controller);
//                OnPropertyChanged(nameof(Controllers));
//                OnPropertyChanged(nameof(ControllersCount));
            });
        }

        private void MainViewModel_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
         
        }

        public void DestroyViewModel()
        {
            mainViewModel.PropertyChanged -= MainViewModel_PropertyChanged;
            mainViewModel = null;
            helper = null;
        }

//        public List<ICloudController> Controllers
//        {
//            get { return mainViewModel.OnlyCloudControolers; }
//        }

        //public int ControllersCount => Controllers.Count;

        public bool AutomaticaliDeleteFilesFromDrBox {
            get { return helper.SimpleGet(false); }
            set { helper.SimpleSet(value); }
          }

        public bool AskBeforeDeletionBook {
            get { return helper.SimpleGet(true); }
            set { helper.SimpleSet(value); }
        }

        public int DefaultMaxLengthHistory => 10;

        public int MaxLengthHistory {
            get { return helper.SimpleGet(DefaultMaxLengthHistory); }
            set { helper.SimpleSet(value); }
        }

        public string Changelog => $"Changelog: {Environment.NewLine}" +
                                   $"1) Responsive settings view{Environment.NewLine}" +
                                   $"2) Choose from standart covers or set your own{Environment.NewLine}" +
                                   $"3) Volum bar hided in contextFlyout{Environment.NewLine}" +
                                   $"4) Show list with books with respect to window height{Environment.NewLine}" +
                                   $"5) Show button Operations if needed and use for that overlay{Environment.NewLine}" +
                                   $"6) Show services for book in tile view{Environment.NewLine}" +
                                   $"7) Show cover from file tag if cover's file hasn't found{Environment.NewLine}" +
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

        public Color ColorOfUserControlBlur {
            get { return Color.FromArgb(64,255,255,255);}
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

        public MainControlViewModel MainViewModel
        {
            get { return mainViewModel; }
        }

        public ListDataTemplateStruct[] AvaliableListDataTemplages { get;}
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
            get {
                if (!UseStandartCover && CustomeCoverName!= null)
                {
                    return "ms-appdata:///local/" + CustomeCoverName;
                }
                var debug =  helper.SimpleGet("ms-appx:///Image/no-image-available.jpg");
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
        public bool UseStandartCover {
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
    }
}
