using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;
using UWPAudioBookPlayer.ModelView;
using Autofac;

// The Blank Page item template is documented at http://go.microsoft.com/fwlink/?LinkId=234238

namespace UWPAudioBookPlayer.View
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class LibrivoxBooksOnlineView : Page
    {
        private LibrivoxOnlineBooksViewModel viewModel;
        public LibrivoxBooksOnlineView()
        {
            this.InitializeComponent();
            viewModel = Global.container.Resolve<LibrivoxOnlineBooksViewModel>();
            var mainModel = Global.MainModelView;
            viewModel.AddSourceToLibrary = mainModel.AddSourceToLibraryCommand;
            viewModel.AddAndPlayBook = mainModel.StartPlaySourceCommand;
            this.Loading += OnLoading;
            DataContext = viewModel;
        }

        private async void OnLoading(FrameworkElement sender, object args)
        {
            await viewModel.LoadData();
        }


        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            Windows.UI.Core.SystemNavigationManager.GetForCurrentView().AppViewBackButtonVisibility = AppViewBackButtonVisibility.Visible;
            Windows.UI.Core.SystemNavigationManager.GetForCurrentView().BackRequested += OnBackRequested;
            base.OnNavigatedTo(e);
        }

        private void OnBackRequested(object sender, BackRequestedEventArgs backRequestedEventArgs)
        {
            if (viewModel.BackIfCan())
            {
                backRequestedEventArgs.Handled = true;
                return;
            }
            Windows.UI.Core.SystemNavigationManager.GetForCurrentView().BackRequested -= OnBackRequested;
            if (Frame.CanGoBack)
            {
                Frame.GoBack();
                backRequestedEventArgs.Handled = true;
            }
        }
    }
}
