using System;
using Windows.Foundation;
using Windows.Storage.AccessCache;
using Windows.Storage.Pickers;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;
using UWPAudioBookPlayer.DAL.Model;
using UWPAudioBookPlayer.ModelView;
using UWPAudioBookPlayer.Service;
using UWPAudioBookPlayer.View;

// The Blank Page item template is documented at http://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x409

namespace UWPAudioBookPlayer
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : Page
    {
        private MainControlViewModel viewModel;
        private SettingsModelView _settingsModelView;
        public MainPage(/*SettingsModelView settingsModelView*/)
        {
            //if (settingsModelView == null)
            //    throw new ArgumentNullException(nameof(settingsModelView));
            //this._settingsModelView = settingsModelView;
            this.InitializeComponent();
            viewModel = MainControlViewModelFactory.GetMainControlViewModel(mainPlayer, new SettingsModelView(new UniversalApplicationSettingsHelper()));
            DataContext = viewModel;
            viewModel.DrbController.NavigateToAuthPage += DrbControllerOnNavigateToAuthPage;
            viewModel.ShowBookDetails += ViewModelOnShowBookDetails;
        }

        private void ViewModelOnShowBookDetails(object sender, AudioBookSourcesCombined audioBookSourcesCombined)
        {
            Frame.Navigate(typeof(BookDetailInfo),
                new AudioBookSourceDetailViewModel(audioBookSourcesCombined.MainSource, audioBookSourcesCombined.Cloud));
        }

        private void DrbControllerOnNavigateToAuthPage(object sender, Tuple<Uri, Action<Uri>> tuple)
        {
            webView.Source = tuple.Item1;
            webView.Visibility = Visibility.Visible;
            var del = new TypedEventHandler<WebView,WebViewNavigationCompletedEventArgs>(delegate (WebView view, WebViewNavigationCompletedEventArgs args) { tuple.Item2(args.Uri); });
            viewModel.DrbController.CloseAuthPage += DrbControllerOnCloseAuthPage;
            viewModel.DrbController.CloseAuthPage += (o, args) => webView.NavigationCompleted -= del; 
            webView.NavigationCompleted += del;

        }

        private void DrbControllerOnCloseAuthPage(object sender, EventArgs eventArgs)
        {
            webView.Visibility = Visibility.Collapsed;
            viewModel.DrbController.CloseAuthPage -= DrbControllerOnCloseAuthPage;
        }

        private async void AppBarButton_Click(object sender, RoutedEventArgs e)
        {
            FolderPicker folderPicker = new FolderPicker();
            folderPicker.FileTypeFilter.Add(".mp3");
            folderPicker.FileTypeFilter.Add(".wav");

            var folder = await folderPicker.PickSingleFolderAsync();
            if (folder == null)
                return;
            string pickedFolderToken = StorageApplicationPermissions.FutureAccessList.Add(folder);
           // ApplicationData.Current.LocalSettings.Values.Add(FolderTokenSettingsKey, pickedFolderToken);
            viewModel.AddPlaySource(folder.Path, pickedFolderToken);
        }

        private void MainPlayer_OnCurrentStateChanged(object sender, RoutedEventArgs e)
        {
            
        }

        private async void MainPage_OnLoading(FrameworkElement sender, object args)
        {
            await viewModel.LoadData();
        }

        private async void MainPage_OnUnloaded(object sender, RoutedEventArgs e)
        {
            await viewModel.SaveData();
        }

        private async void Page_LostFocus(object sender, RoutedEventArgs e)
        {
            //await viewModel.SaveData();
        }

        private async void SelectBaseFolderClick(object sender, RoutedEventArgs e)
        {
            FolderPicker folderPicker = new FolderPicker();
            folderPicker.FileTypeFilter.Add(".mp3");
            

            var folder = await folderPicker.PickSingleFolderAsync();
            if (folder == null)
                return;
            string pickedFolderToken = StorageApplicationPermissions.FutureAccessList.Add(folder);
            viewModel.AddBaseFolder(folder.Path, pickedFolderToken);
        }

        protected override async void OnNavigatingFrom(NavigatingCancelEventArgs e)
        {
            await viewModel.SaveData();
            base.OnNavigatingFrom(e);
        }

        private void OpenSettingsClick(object sender, RoutedEventArgs e)
        {
            Frame.Navigate(typeof(SettingsView), _settingsModelView);
        }

        private void ButtonBase_OnClick(object sender, RoutedEventArgs e)
        {
            OneDriveController controller = new OneDriveController();
            controller.Auth();
        }
    }
}
