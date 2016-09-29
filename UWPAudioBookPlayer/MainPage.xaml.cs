using System;
using System.ComponentModel;
using System.Linq;
using Windows.Foundation;
using Windows.Storage.AccessCache;
using Windows.Storage.Pickers;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Navigation;
using UWPAudioBookPlayer.DAL.Model;
using UWPAudioBookPlayer.ModelView;
using UWPAudioBookPlayer.Service;
using UWPAudioBookPlayer.View;
using Autofac;

// The Blank Page item template is documented at http://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x409

namespace UWPAudioBookPlayer
{
    public class NavigateCntent
    {
        public MainControlViewModel mainViewModel { get; set; }
        public ISettingsService settingsViewModel { get; set; }
    }
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
            viewModel = Global.container.Resolve<MainControlViewModel>();
            Global.MainModelView = viewModel;
            DataContext = viewModel;
            viewModel.NavigateToAuthPage += DrbControllerOnNavigateToAuthPage;
            viewModel.ShowBookDetails += ViewModelOnShowBookDetails;
            viewModel.CloseAuthPage += DrbControllerOnCloseAuthPage;
            //viewModel.LoadData();
            //element = viewModel.Player;

            viewModel.PropertyChanged += ViewModelOnPropertyChanged;

        }

        private async void ViewModelOnPropertyChanged(object sender, PropertyChangedEventArgs propertyChangedEventArgs)
        {
        }

        private void ViewModelOnShowBookDetails(object sender, AudioBookSourcesCombined audioBookSourcesCombined)
        {
            Frame.Navigate(typeof(BookDetailInfo),
                new AudioBookSourceDetailViewModel(audioBookSourcesCombined.MainSource, audioBookSourcesCombined.Clouds));
        }

        private void DrbControllerOnNavigateToAuthPage(object sender, Tuple<Uri, Action<Uri>> tuple)
        {
            webView.Source = tuple.Item1;
            webView.Visibility = Visibility.Visible;
            var del = new TypedEventHandler<WebView,WebViewNavigationCompletedEventArgs>(delegate (WebView view, WebViewNavigationCompletedEventArgs args) { tuple.Item2(args.Uri); });
            webView.NavigationCompleted -= del;
            webView.NavigationCompleted += del;

        }

        private void DrbControllerOnCloseAuthPage(object sender, EventArgs eventArgs)
        {
            webView.Visibility = Visibility.Collapsed; 
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
            //await viewModel.LoadData();
        }

        private async void MainPage_OnUnloaded(object sender, RoutedEventArgs e)
        {
            //await viewModel.SaveData();
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

            Frame.Navigate(typeof(SettingsView), null);

        }

        private void ButtonBase_OnClick(object sender, RoutedEventArgs e)
        {
            OneDriveController controller = new OneDriveController();
            controller.Auth();
        }

            MenuFlyout menu = new MenuFlyout();
        private async void ShowContextMenuDownload(object sender, RoutedEventArgs e)
        {
            var controllers = viewModel.CloudControllers.Where(c => c.IsAutorized).ToArray();
            if (!controllers.Any())
                return;
            if (controllers.Length == 1)
            {
                viewModel.UploadBookToCloudCommand.Execute(controllers[0]);
                return;
            }
            menu.Items.Clear();
           foreach (var cloud in controllers)
                menu.Items.Add(new MenuFlyoutItem() {Text = cloud.ToString(), Command = viewModel.UploadBookToCloudCommand, CommandParameter = cloud});
            menu.ShowAt(((FrameworkElement)sender));
        }

        private void ShowContextMenuUpload(object sender, RoutedEventArgs e)
        {
            var controllers = viewModel.CloudControllers.Where(c => c.IsAutorized).ToArray();
            if (!controllers.Any())
                return;
            if (controllers.Length == 1)
            {
                viewModel.DownloadBookFromCloudCommand.Execute(controllers[0]);
                return;
            }
            menu.Items.Clear();
            foreach (var cloud in controllers)
                menu.Items.Add(new MenuFlyoutItem() { Text = cloud.ToString(), Command = viewModel.DownloadBookFromCloudCommand, CommandParameter = cloud });
            menu.ShowAt(((FrameworkElement)sender));
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            Windows.UI.Core.SystemNavigationManager.GetForCurrentView().AppViewBackButtonVisibility = AppViewBackButtonVisibility.Collapsed;
            base.OnNavigatedTo(e);
        }

        private void ListPickerFlyout_OnItemsPicked(ListPickerFlyout sender, ItemsPickedEventArgs args)
        {
            viewModel.PlayHistoryElementCommand.Execute(args.AddedItems[0]);
        }

        private void LibriVoxButtonclicked(object sender, RoutedEventArgs e)
        {
            Frame.Navigate(typeof(LibrivoxBooksOnlineView));
        }

        private void ShowBookmarksMenu(object sender, RightTappedRoutedEventArgs e)
        {
            if (viewModel.PlayingSource == null)
                return;
            var bookmarks = viewModel.BookMarksForSelectedPlayingBook;
            if (bookmarks == null || !bookmarks.Any())
                return;
            menu.Items.Clear();
            if (bookmarks != null)
            {
                foreach (var bookMark in bookmarks)
                {
                    menu.Items.Add(new MenuFlyoutItem
                    {
                        DataContext = bookMark,
                        Template = (ControlTemplate) this.Resources["BookMarkTemplateKey"]
                    });
                }
                menu.ShowAt((FrameworkElement) sender);
            }
        }

        private void FlyoutBase_OnClosing(FlyoutBase sender, FlyoutBaseClosingEventArgs args)
        {
            args.Cancel = true;
        }

        private void GoToAddBookMark(object sender, RoutedEventArgs e)
        {
            Frame.Navigate(typeof (AddBookMark));
        }
    }
}
