using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Windows.System;
using Windows.System.RemoteSystems;
using Windows.UI.Core;
using Windows.UI.Notifications;
using Microsoft.Toolkit.Uwp.Notifications; // Notifications library
using Microsoft.QueryStringDotNET; // QueryString.NET
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Navigation;
using UWPAudioBookPlayer.ModelView;
using Autofac;
using UWPAudioBookPlayer.Service;

// The Blank Page item template is documented at http://go.microsoft.com/fwlink/?LinkId=234238

namespace UWPAudioBookPlayer.View
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class SettingsView : Page
    {
        private ISettingsService _modelView;
        private RemoteSystemWatcher m_remoteSystemWatcher;

        public SettingsView()
        {
            this.InitializeComponent();
            _modelView = Global.container.Resolve<ISettingsService>();
            DataContext = _modelView;
            Windows.UI.Core.SystemNavigationManager.GetForCurrentView().AppViewBackButtonVisibility = AppViewBackButtonVisibility.Visible;

            Windows.UI.Core.SystemNavigationManager.GetForCurrentView().BackRequested += (s, a) =>
            {

                if (Frame.CanGoBack)
                {
                    Frame.GoBack();
                    a.Handled = true;
                }
            };
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
        }


        private bool discovering = false;

        private async void StartDiscoveryListening(object sender, RoutedEventArgs e)
        {
            if (discovering)
            {
                m_remoteSystemWatcher.Stop();
                m_remoteSystemWatcher.RemoteSystemAdded -= RemoteSystemWatcher_RemoteSystemAdded;

                // Subscribing to the event raised when a previously found remote system is no longer available.
                m_remoteSystemWatcher.RemoteSystemRemoved -= RemoteSystemWatcher_RemoteSystemRemoved;
                discovering = false;

                DevicesCollection.Clear();
                this.DevicesLists.Visibility = Visibility.Collapsed;

                if (sender is Button)
                    (sender as Button).Content = "Start discovering";
            }
            else
            {
                this.DevicesLists.ItemsSource = DevicesCollection;
                var binding = new Binding()
                {
                    Path = new PropertyPath(nameof(SelectedSystem)),
                    Mode = BindingMode.TwoWay,
                    Source = this
                };
                BindingOperations.SetBinding(DevicesLists, ListView.SelectedItemProperty, binding);
                RemoteSystemAccessStatus accessStatus = await RemoteSystem.RequestAccessAsync();

                if (accessStatus == RemoteSystemAccessStatus.Allowed)
                {
                    m_remoteSystemWatcher = RemoteSystem.CreateWatcher();

                    // Subscribing to the event raised when a new remote system is found by the watcher.
                    m_remoteSystemWatcher.RemoteSystemAdded += RemoteSystemWatcher_RemoteSystemAdded;

                    // Subscribing to the event raised when a previously found remote system is no longer available.
                    m_remoteSystemWatcher.RemoteSystemRemoved += RemoteSystemWatcher_RemoteSystemRemoved;

                    m_remoteSystemWatcher.Start();
                    if (sender is Button)
                        (sender as Button).Content = "Stop discovering";

                    discovering = true;

                    this.DevicesLists.Visibility = Visibility.Visible;

                    try
                    {
                       // var device = await getDeviceByAddressAsync("192.168.0.113");
                    }
                    catch { }

                }

            }
        }

        private async Task<RemoteSystem> getDeviceByAddressAsync(string IPaddress)
        {
            // construct a HostName object
            Windows.Networking.HostName deviceHost = new Windows.Networking.HostName(IPaddress);

            // create a RemoteSystem object with the HostName
            RemoteSystem remotesys = await RemoteSystem.FindByHostNameAsync(deviceHost);

            return remotesys;
        }

        private ObservableCollection<RemoteSystem> DevicesCollection = new ObservableCollection<RemoteSystem>();
        public RemoteSystem SelectedSystem = null;

        private void RemoteSystemWatcher_RemoteSystemRemoved(RemoteSystemWatcher sender, RemoteSystemRemovedEventArgs args)
        {
            Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                DevicesCollection.Remove(
                    DevicesCollection.FirstOrDefault(
                        d => d.Id == args.RemoteSystemId));
            }).AsTask().ConfigureAwait(false);
        }

        private void RemoteSystemWatcher_RemoteSystemAdded(RemoteSystemWatcher sender, RemoteSystemAddedEventArgs args)
        {
            Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                DevicesCollection.Add(args.RemoteSystem);
            }).AsTask().ConfigureAwait(false);
        }

        private void DeviceSelected(object sender, ItemClickEventArgs e)
        {
            this.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, async () =>
            {
                var selec = e.ClickedItem as RemoteSystem;
                if (selec == null)
                    return;
                RemoteLaunchUriStatus launchUriStatus =
                    await
                        RemoteLauncher.LaunchUriAsync(new RemoteSystemConnectionRequest(selec),
                            uri: new Uri("cloudbookplayer:?str"));


                ToastVisual visual = new ToastVisual()
                {
                    BindingGeneric = new ToastBindingGeneric()
                    {
                        Children =
                        {
                            new AdaptiveText()
                            {
                                Text = "Launch remote app"
                            },

                            new AdaptiveText()
                            {
                                Text = launchUriStatus.ToString()
                            },

                        },


                    }
                };

                ToastContent toastContent = new ToastContent()
                {
                    Visual = visual,
                };

                ToastNotificationManager.CreateToastNotifier().Show(new ToastNotification(toastContent.GetXml()));
            });
        }
    }
}
