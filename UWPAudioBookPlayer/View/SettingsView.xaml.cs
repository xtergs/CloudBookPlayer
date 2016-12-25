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
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Navigation;
using UWPAudioBookPlayer.ModelView;
using Autofac;
using UWPAudioBookPlayer.Helper;
using UWPAudioBookPlayer.Service;
using Windows.Storage;

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

            SystemNavigationManager.GetForCurrentView().BackRequested += SettingsView_BackRequested;

            if (Microsoft.Services.Store.Engagement.StoreServicesFeedbackLauncher.IsSupported())
            {
                this.feedbackButton.Visibility = Visibility.Visible;
            }

            //CompactState = SomeSettings.Name;
            VisualStateManager.GoToState(this, SomeSettings.Name, false);

            this.Unloaded += OnUnloaded;
        }

        private void OnUnloaded(object sender, RoutedEventArgs routedEventArgs)
        {
            DataContext = null;
            _modelView = null;
        }

        private void SettingsView_BackRequested(object sender, BackRequestedEventArgs e)
        {

            if (CompactState != "" && this.ActualWidth < WidthCompactTrashhold)
            {
                CompactState = "";
                e.Handled = true;
                ChangeVisualState();
                return;
            }
            else
                if (Frame.CanGoBack)
                {
                    SystemNavigationManager.GetForCurrentView().BackRequested -= SettingsView_BackRequested;
                    Frame.GoBack();
                    e.Handled = true;
                }
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            try
            {
                m_remoteSystemWatcher?.Stop();
                m_remoteSystemWatcher = null;
            }
            catch { }
            base.OnNavigatedFrom(e);
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

        private void ButtonBase_OnClick(object sender, RoutedEventArgs e)
        {
            Global.container.Resolve<INotification>().ShowMessageAsync(_modelView.Changelog);
        }

        private async void feedbackButton_Click(object sender, RoutedEventArgs e)
        {
            var launcher = Microsoft.Services.Store.Engagement.StoreServicesFeedbackLauncher.GetDefault();
            await launcher.LaunchAsync();
        }

        private void UIElement_OnPointerPressed(object sender, PointerRoutedEventArgs e)
        {
            
        }

        private void Hyperlink_Click(Windows.UI.Xaml.Documents.Hyperlink sender, Windows.UI.Xaml.Documents.HyperlinkClickEventArgs args)
        {
            AddMoreAcountButton.Flyout.ShowAt(AddMoreAcountButton);
        }

        public int WidthCompactTrashhold = 720;
        private string CompactState = "";
        private void page_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            ChangeVisualState();
        }

        private void ChangeLeftPanelUI(double width, double height)
        {
            if (width >= WidthCompactTrashhold)
            {
                VisualStateManager.GoToState(this, ShowPanel.Name, true);
            }
            else if (CompactState == "")
            {
                VisualStateManager.GoToState(this, ShowPanel.Name, true);
            }
            else
                VisualStateManager.GoToState(this, HidePanel.Name, true);
        }

        private void ChangeRightPartUI(double width, double height)
        {
            if (width >= WidthCompactTrashhold)
            {
                if (listView1.SelectedIndex < 0)
                {
                    var index = listView1.Items.IndexOf(CompactState);
                    if (index >= 0)
                        listView1.SelectedIndex = index;
                    else
                        listView1.SelectedIndex = 1;
                }
                if (CompactState == CoverViewState.Name)
                    VisualStateManager.GoToState(this, FullWindowImages.Name, true);
            }
            else
            {
                if (CompactState == "")
                {
                    listView1.SelectedIndex = -1;
                    VisualStateManager.GoToState(this, Compact.Name, true);
                }
                else if (CompactState == CoverViewState.Name)
                {
                    VisualStateManager.GoToState(this, CompactState, true);

                }
            }
        }

        private void ChangeVisualState(bool changeState = true)
        {
            ChangeLeftPanelUI(ActualWidth, ActualHeight);
            ChangeRightPartUI(ActualWidth, ActualHeight);
        }

        private void listView1_ItemClick(object sender, ItemClickEventArgs e)
        {
            var selectedSettings = (e.ClickedItem as string);
            var listView = (ListView)e.OriginalSource;

            CompactState = (string)listView.Items.OfType<ListViewItem>().First(x=> x.Content as string == selectedSettings).Tag;
            VisualStateManager.GoToState(this, CompactState, true);
            ChangeVisualState();
        }

        private async void PickUpImageCoverClickAsync(object sender, RoutedEventArgs e)
        {
            PickCustomImageRing.Visibility = Visibility.Visible;
            try
            {
                var picker = new Windows.Storage.Pickers.FileOpenPicker();
                picker.FileTypeFilter.Add(".png");
                picker.FileTypeFilter.Add(".jpg");

                picker.ViewMode = Windows.Storage.Pickers.PickerViewMode.Thumbnail;

                picker.SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.PicturesLibrary;
                var file = await picker.PickSingleFileAsync();
                var oldName = (_modelView as SettingsModelView).CustomeCoverName;
                var copied = await file.CopyAsync(ApplicationData.Current.LocalFolder, file.Name, NameCollisionOption.GenerateUniqueName);
                (_modelView as SettingsModelView).CustomeCoverName = copied.Name;
                (_modelView as SettingsModelView).NotifyCustomeImageChanged();
                var oldFile = await ApplicationData.Current.LocalFolder.GetFileAsync(oldName);
                await oldFile.DeleteAsync();
            }
            catch(Exception eee)
            {

            }
            finally
            {
                PickCustomImageRing.Visibility = Visibility.Collapsed;
            }
        }
    }
}
