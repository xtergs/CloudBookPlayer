using System;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;
using UWPAudioBookPlayer.ModelView;

// The Blank Page item template is documented at http://go.microsoft.com/fwlink/?LinkId=234238

namespace UWPAudioBookPlayer.View
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class BookMarksView : Page
    {
        private BookMarksViewModel viewModel;
        public BookMarksView()
        {
            viewModel = Global.BookMarksViewModel;
            viewModel.ExternalPlayCommand = Global.MainModelView.StartPlaySourceCommand;
            this.InitializeComponent();
            DataContext = viewModel;
            this.Loaded += OnLoaded;
        }

        private void OnLoaded(object sender, RoutedEventArgs routedEventArgs)
        {
            viewModel.MediaPlayer = this.player.MediaPlayer;
        }


        public BookMarksViewModel ViewModel => viewModel;


        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            if (Frame.CanGoBack)
            {
                SystemNavigationManager.GetForCurrentView().AppViewBackButtonVisibility = AppViewBackButtonVisibility.Visible;
                SystemNavigationManager.GetForCurrentView().BackRequested += OnBackRequested;
            }

            await viewModel.CheckRealBookMarks().ConfigureAwait(false);
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            base.OnNavigatedFrom(e);
            SystemNavigationManager.GetForCurrentView().AppViewBackButtonVisibility = AppViewBackButtonVisibility.Collapsed;
            SystemNavigationManager.GetForCurrentView().BackRequested -= OnBackRequested;
        }

        private void OnBackRequested(object sender, BackRequestedEventArgs backRequestedEventArgs)
        {
            if (Frame.CanGoBack)
            {
                SystemNavigationManager.GetForCurrentView().BackRequested -= OnBackRequested;
                SystemNavigationManager.GetForCurrentView().AppViewBackButtonVisibility = AppViewBackButtonVisibility.Collapsed;
                Frame.GoBack();
            }
        }

        private void ButtonBase_OnClick(object sender, RoutedEventArgs e)
        {
            Frame.Navigate(typeof (AddBookMark), ViewModel.AudioBook);
        }
    }
}
