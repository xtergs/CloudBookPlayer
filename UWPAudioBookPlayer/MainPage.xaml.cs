using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Graphics.DirectX;
using Windows.Graphics.Effects;
using Windows.Media.DialProtocol;
using Windows.Storage.AccessCache;
using Windows.Storage.Pickers;
using Windows.UI;
using Windows.UI.Composition;
using Windows.UI.Core;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Hosting;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml.Navigation;
using UWPAudioBookPlayer.DAL.Model;
using UWPAudioBookPlayer.ModelView;
using UWPAudioBookPlayer.Service;
using UWPAudioBookPlayer.View;
using Autofac;
using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Effects;
using Microsoft.Graphics.Canvas.Text;
using Microsoft.Graphics.Canvas.UI.Composition;
using Microsoft.Toolkit.Uwp.UI.Controls;
using UWPAudioBookPlayer.Model;
using Microsoft.Toolkit.Uwp.UI;

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
        private CastService castService;
        public MainPage(/*SettingsModelView settingsModelView*/)
        {
            //if (settingsModelView == null)
            //    throw new ArgumentNullException(nameof(settingsModelView));
            //this._settingsModelView = settingsModelView;
            viewModel = Global.container.Resolve<MainControlViewModel>();
            viewModel.Settings = Global.container.Resolve<ISettingsService>();
            this.InitializeComponent();
            InitComposition();
            Global.MainModelView = viewModel;
            SetDataTemplateForList();
            DataContext = viewModel;
            viewModel.NavigateToAuthPage += DrbControllerOnNavigateToAuthPage;
            viewModel.ShowBookDetails += ViewModelOnShowBookDetails;
            viewModel.CloseAuthPage += DrbControllerOnCloseAuthPage;
            //viewModel.LoadData();
            //element = viewModel.Player;

            viewModel.PropertyChanged += ViewModelOnPropertyChanged;

            viewModel.StartObserveDevices();

            castService = new CastService(viewModel.Player);

            if (viewModel.Settings?.StartInCompactMode == true)
            {
                ApplicationView.PreferredLaunchViewSize = new Size(480, 800);
                ApplicationView.PreferredLaunchWindowingMode = ApplicationViewWindowingMode.PreferredLaunchViewSize;
            }
            else
            {
                ApplicationView.PreferredLaunchWindowingMode = ApplicationViewWindowingMode.Auto;
            }


            if (viewModel.Settings != null)
                viewModel.Settings.PropertyChanged += SettingsOnPropertyChanged;

            this.Loaded += OnLoaded;
            this.Unloaded += OnUnloaded;
            Rectagle.SizeChanged += RectagleOnSizeChanged;
        }

        void SetDataTemplateForList()
        {
            if (Resources.ContainsKey(viewModel.Settings.ListDataTemplate))
            {
                bookListView.ItemTemplate = Resources[viewModel.Settings.ListDataTemplate] as DataTemplate;
                if (viewModel.Settings.IsWrapListItems)
                {
                    bookListView.ItemsPanel = Resources["listWrapPanel"] as ItemsPanelTemplate;
                }
            }
        }

        private void RectagleOnSizeChanged(object sender, SizeChangedEventArgs sizeChangedEventArgs)
        {
            if (visual == null)
                return;
            if (viewModel.Settings.BlurOnlyOverImage)
                visual.Size = new Vector2((float)Rectagle.ActualWidth, (float)Rectagle.ActualHeight);
            else
                visual.Size = new Vector2((float)BackgroundImage.ActualWidth, (float)Rectagle.ActualHeight);
        }

        SpriteVisual visual;
        GaussianBlurEffect blurEffect;

        public float BlurAmount
        {
            get { return viewModel.Settings.BlurControlPanel; }
            
        }

        private void InitComposition()
        {
            var compositor = ElementCompositionPreview.GetElementVisual(Rectagle).Compositor;
            
            visual = compositor.CreateSpriteVisual();

            

            
            blurEffect = new GaussianBlurEffect()
            {
                Name = "Blur",
                BlurAmount = BlurAmount, // You can place your blur amount here.
                BorderMode = EffectBorderMode.Hard,
                Optimization = EffectOptimization.Balanced,
                Source = new CompositionEffectSourceParameter("source"),
            };

            BlendEffect blendEffect = new BlendEffect
            {
                Background = blurEffect,
                Foreground = new ColorSourceEffect { Name = "Color", Color = Color.FromArgb((byte)viewModel.Settings.OpacityUserBlur, 255, 255, 255) },
                Mode = BlendEffectMode.SoftLight
            };

            var effectFactory = compositor.CreateEffectFactory(blendEffect, new[] { "Blur.BlurAmount" });

            var effectBrush = effectFactory.CreateBrush();
            effectBrush.SetSourceParameter("source", compositor.CreateBackdropBrush());
            visual.Brush = effectBrush;
            
            visual.Brush = effectBrush;

            ElementCompositionPreview.SetElementChildVisual(Rectagle, visual);
        }

    private void OnUnloaded(object sender, RoutedEventArgs routedEventArgs)
        {
            this.Unloaded -= OnUnloaded;
            viewModel.NavigateToAuthPage -= DrbControllerOnNavigateToAuthPage;
            viewModel.ShowBookDetails -= ViewModelOnShowBookDetails;
            viewModel.CloseAuthPage -= DrbControllerOnCloseAuthPage;

            viewModel.PropertyChanged -= ViewModelOnPropertyChanged;
            if (viewModel.Settings != null)
            viewModel.Settings.PropertyChanged -= SettingsOnPropertyChanged;
            viewModel = null;
        }


        public AudioBookSourceWithClouds RightSelectedItemContext { get; private set; }

        private void SettingsOnPropertyChanged(object sender, PropertyChangedEventArgs propertyChangedEventArgs)
        {
            switch (propertyChangedEventArgs.PropertyName)
            {
                case nameof(viewModel.Settings.StartInCompactMode):
                    if (viewModel.Settings.StartInCompactMode == true)
                    {
                        ApplicationView.PreferredLaunchViewSize = new Size(480, 800);
                        ApplicationView.PreferredLaunchWindowingMode = ApplicationViewWindowingMode.PreferredLaunchViewSize;
                    }
                    else
                    {
                        ApplicationView.PreferredLaunchWindowingMode = ApplicationViewWindowingMode.Auto;
                    }
                    break;
            }
        }

        private void OnLoaded(object sender, RoutedEventArgs routedEventArgs)
        {
            Loaded -= OnLoaded;
            var changelog = viewModel.Settings.ChangeLogOnce;
            if (!string.IsNullOrWhiteSpace(changelog))
                viewModel.Notificator.ShowMessageAsync(changelog);
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

        private async void AddFolderClick(object sender, RoutedEventArgs e)
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
            await viewModel.SaveData().ConfigureAwait(false);
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
            fileImageCache.Clear();
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
        private DialDevicePicker picker;

        private async void ShowContextMenuDownload(object sender, RoutedEventArgs e)
        {
            FillUploadList(menu.Items);
            if (menu.Items.Any())
                menu.ShowAt(((FrameworkElement)sender));
        }

        private void ShowContextMenuUpload(object sender, RoutedEventArgs e)
        {
            FillDownloadList(menu.Items);
            if (menu.Items.Any())
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
            Global.BookMarksViewModel = Global.container.Resolve<BookMarksViewModel>();
            Global.BookMarksViewModel.AudioBook = viewModel.PlayingSource;
            Global.BookMarksViewModel.BookMarks = viewModel.BookMarksForSelectedPlayingBook.ToList();
            Frame.Navigate(typeof(BookMarksView));
            //Frame.Navigate(typeof (BookMarksView));
            //if (viewModel.PlayingSource == null)
            //    return;
            //var bookmarks = viewModel.BookMarksForSelectedPlayingBook;
            //if (bookmarks == null || !bookmarks.Any())
            //    return;
            //menu.Items.Clear();
            //if (bookmarks != null)
            //{
            //    foreach (var bookMark in bookmarks)
            //    {
            //        menu.Items.Add(new MenuFlyoutItem
            //        {
            //            DataContext = bookMark,
            //            Template = (ControlTemplate) this.Resources["BookMarkTemplateKey"]
            //        });
            //    }
            //    menu.ShowAt((FrameworkElement) sender);
            //}
        }

        private void FlyoutBase_OnClosing(FlyoutBase sender, FlyoutBaseClosingEventArgs args)
        {
            args.Cancel = true;
        }

        private void GoToAddBookMark(object sender, RoutedEventArgs e)
        {
            Frame.Navigate(typeof (AddBookMark));
        }

        private void RemoteDevicesPicked(ListPickerFlyout sender, ItemsPickedEventArgs args)
        {
            if (args.AddedItems.Any())
                this.viewModel.StreamToDeviceCommand.Execute(args.AddedItems[0]);
            sender.SelectedItem = null;
        }

        private async void DeviceCastClick(object sender, RoutedEventArgs e)
        {
            castService.ShowPicker();
        }

        private void DialAppClick(object sender, RoutedEventArgs e)
        {
            picker = new DialDevicePicker();

            //Add the DIAL Filter, so that the application only shows DIAL devices that have 
            // the application installed or advertise that they can install them.
            picker.Filter.SupportedAppNames.Add("cloudbookplayer");

            //Hook up device selected event
            picker.DialDeviceSelected += Picker_DeviceSelected;

            //Hook up the picker disconnected event
            picker.DisconnectButtonClicked += Picker_DisconnectButtonClicked;

            //Hook up the picker dismissed event
            picker.DialDevicePickerDismissed += Picker_DevicePickerDismissed;
            picker.Show(new Rect(0, 0, 300, 300));
        }

        private void Picker_DevicePickerDismissed(DialDevicePicker sender, object args)
        {
            
        }

        private void Picker_DisconnectButtonClicked(DialDevicePicker sender, DialDisconnectButtonClickedEventArgs args)
        {
            
        }

        private void Picker_DeviceSelected(DialDevicePicker sender, DialDeviceSelectedEventArgs args)
        {
            
        }

        private void GoToViewBookMarks(object sender, HoldingRoutedEventArgs e)
        {

        }

        private void BookListView_OnRightTapped(object sender, RightTappedRoutedEventArgs e)
        {
            try
            {
                RightSelectedItemContext = (AudioBookSourceWithClouds) ((FrameworkElement) e.OriginalSource).DataContext;
                Bindings.Update();
            }
            catch (InvalidCastException ex)
            {
                Debug.WriteLine($"{ex.Message}\n{ex.StackTrace}");
            }
        }

        private void ButtonBase_OnClick1(object sender, RoutedEventArgs e)
        {
            showBooks.IsChecked = false;
        }

        private void FillDownloadSubContextMenu(object sender, RoutedEventArgs e)
        {
            var element = sender as MenuFlyoutSubItem;
            if (element == null)
                return;
            FillDownloadList(element.Items);
            if (!element.Items.Any())
                element.Visibility = Visibility.Collapsed;
            else
                element.Visibility = Visibility.Visible;

        }

        private void FillUploadSubContextMenu(object sender, RoutedEventArgs e)
        {
            var element = sender as MenuFlyoutSubItem;
            if (element == null)
                return;
            FillUploadList(element.Items);
            if (!element.Items.Any())
                element.Visibility = Visibility.Collapsed;
            else
                element.Visibility = Visibility.Visible;

        }

        private void FillUploadList(IList<MenuFlyoutItemBase> items)
        {
            items.Clear();
            foreach (var controller in viewModel.GetuploadControllers(RightSelectedItemContext))
            {
                items.Add(new MenuFlyoutItem() { Text = controller.ToString(), Command = viewModel.UploadBookToCloudCommand, CommandParameter = controller });
            }
        }


        private void FillDownloadList(IList<MenuFlyoutItemBase> items)
        {
            items.Clear();
            foreach (var controller in viewModel.GetDownloadController(RightSelectedItemContext))
            {
                items.Add(new MenuFlyoutItem() { Text = controller.ToString(), Command = viewModel.DownloadBookFromCloudCommand, CommandParameter = controller });
            }
        }

        private Dictionary<string, BitmapImage> fileImageCache = new Dictionary<string, BitmapImage>();
        private float _blurAmount;

        private async void FrameworkElement_OnDataContextChanged(FrameworkElement sender, DataContextChangedEventArgs args)
        {
            var source = args.NewValue as AudioBookSourceWithClouds;
            if (source == null)
                return;
            var img = sender as ImageEx;
            if (img == null)
                return;
            string cover = source.GetAnyCover();
            if (cover == null)
            {
                SetDefaultImage(img);             
                return;
            }
            if (source.IsLink(cover))
            {
                try
                {
                    var Bitmap = await ImageCache.Instance.GetFromCacheAsync(new Uri(cover, UriKind.Absolute), Guid.NewGuid().ToString(), true);
                    img.Source = Bitmap;
                    return;
                }catch(Exception e)
                {
                    SetDefaultImage(img);
                }
            }
            string cachekey = source.Name + cover;
            try
            {
                BitmapImage btmimage;
                if (fileImageCache.TryGetValue(cachekey, out btmimage))
                {
                    img.Source = btmimage;
                    return;
                }
                var streamResult = await source.GetFileStream(cover);
                btmimage = new BitmapImage();
                streamResult.Item2.Seek(0);
                await btmimage.SetSourceAsync(streamResult.Item2);
                streamResult.Item2.Dispose();
                fileImageCache[cachekey] = btmimage;
                img.Source = btmimage;
                streamResult = null;
            }
            catch (Exception e)
            {
                
            }
        }

        private void SetDefaultImage(ImageEx img)
        {
            BitmapImage btm;
            if (fileImageCache.TryGetValue("default", out btm))
            {
                img.Source = btm;
                return;
            }
            if (viewModel?.Settings?.StandartCover == null)
                return;
            btm = new BitmapImage(new Uri(viewModel.Settings.StandartCover));
            fileImageCache["defualt"] = btm;
            img.Source = btm;
        }

        private void ShowAttachedContextMenuClick(object sender, RoutedEventArgs e)
        {
            (sender as FrameworkElement).ContextFlyout.ShowAt(sender as FrameworkElement);
        }
    }



    public class BackDrop : Control
    {
        Compositor m_compositor;
        SpriteVisual m_blurVisual;
        CompositionBrush m_blurBrush;
        Visual m_rootVisual;

#if SDKVERSION_14393
        bool m_setUpExpressions;
        CompositionSurfaceBrush m_noiseBrush;
#endif

        public BackDrop()
        {
            m_rootVisual = ElementCompositionPreview.GetElementVisual(this as UIElement);
            Compositor = m_rootVisual.Compositor;

            m_blurVisual = Compositor.CreateSpriteVisual();

#if SDKVERSION_14393
            m_noiseBrush = Compositor.CreateSurfaceBrush();

            CompositionEffectBrush brush = BuildBlurBrush();
            brush.SetSourceParameter("source", m_compositor.CreateBackdropBrush());
            m_blurBrush = brush;
            m_blurVisual.Brush = m_blurBrush;

            BlurAmount = 9;
            TintColor = Colors.Transparent;
#else
            m_blurBrush = Compositor.CreateColorBrush(Colors.White);
            m_blurVisual.Brush = m_blurBrush;
#endif
            ElementCompositionPreview.SetElementChildVisual(this as UIElement, m_blurVisual);

            this.Loading += OnLoading;
            this.Unloaded += OnUnloaded;
        }

        public const string BlurAmountProperty = nameof(BlurAmount);
        public const string TintColorProperty = nameof(TintColor);

        public double BlurAmount
        {
            get
            {
                float value = 0;
#if SDKVERSION_14393
                m_rootVisual.Properties.TryGetScalar(BlurAmountProperty, out value);
#endif
                return value;
            }
            set
            {
#if SDKVERSION_14393
                if (!m_setUpExpressions)
                {
                    m_blurBrush.Properties.InsertScalar("Blur.BlurAmount", (float)value);
                }
                m_rootVisual.Properties.InsertScalar(BlurAmountProperty, (float)value);
#endif
            }
        }

        public Color TintColor
        {
            get
            {
                Color value;
#if SDKVERSION_14393
                m_rootVisual.Properties.TryGetColor("TintColor", out value);
#else
                value = ((CompositionColorBrush)m_blurBrush).Color;
#endif
                return value;
            }
            set
            {
#if SDKVERSION_14393
                if (!m_setUpExpressions)
                {
                    m_blurBrush.Properties.InsertColor("Color.Color", value);
                }
                m_rootVisual.Properties.InsertColor(TintColorProperty, value);
#else
                ((CompositionColorBrush)m_blurBrush).Color = value;
#endif
            }
        }

        public Compositor Compositor
        {
            get
            {
                return m_compositor;
            }

            private set
            {
                m_compositor = value;
            }
        }

#pragma warning disable 1998
        private async void OnLoading(FrameworkElement sender, object args)
        {
            this.SizeChanged += OnSizeChanged;
            OnSizeChanged(this, null);

#if SDKVERSION_14393
            m_noiseBrush.Surface = await SurfaceLoader.LoadFromUri(new Uri("ms-appx:///Assets/Noise.jpg"));
            m_noiseBrush.Stretch = CompositionStretch.UniformToFill;
#endif
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            this.SizeChanged -= OnSizeChanged;
        }


        private void OnSizeChanged(object sender, Windows.UI.Xaml.SizeChangedEventArgs e)
        {
            if (m_blurVisual != null)
            {
                m_blurVisual.Size = new System.Numerics.Vector2((float)this.ActualWidth, (float)this.ActualHeight);
            }
        }

#if SDKVERSION_14393
        private void SetUpPropertySetExpressions()
        {
            m_setUpExpressions = true;

            var exprAnimation = Compositor.CreateExpressionAnimation();
            exprAnimation.Expression = $"sourceProperties.{BlurAmountProperty}";
            exprAnimation.SetReferenceParameter("sourceProperties", m_rootVisual.Properties);

            m_blurBrush.Properties.StartAnimation("Blur.BlurAmount", exprAnimation);

            exprAnimation.Expression = $"sourceProperties.{TintColorProperty}";

            m_blurBrush.Properties.StartAnimation("Color.Color", exprAnimation);
        }


        private CompositionEffectBrush BuildBlurBrush()
        {
            GaussianBlurEffect blurEffect = new GaussianBlurEffect()
            {
                Name = "Blur",
                BlurAmount = 0.0f,
                BorderMode = EffectBorderMode.Hard,
                Optimization = EffectOptimization.Balanced,
                Source = new CompositionEffectSourceParameter("source"),
            };

            BlendEffect blendEffect = new BlendEffect
            {
                Background = blurEffect,
                Foreground = new ColorSourceEffect { Name = "Color", Color = Color.FromArgb(64, 255, 255, 255) },
                Mode = BlendEffectMode.SoftLight
            };

            SaturationEffect saturationEffect = new SaturationEffect
            {
                Source = blendEffect,
                Saturation = 1.75f,
            };

            BlendEffect finalEffect = new BlendEffect
            {
                Foreground = new CompositionEffectSourceParameter("NoiseImage"),
                Background = saturationEffect,
                Mode = BlendEffectMode.Screen,
            };

            var factory = Compositor.CreateEffectFactory(
                finalEffect,
                new[] { "Blur.BlurAmount", "Color.Color" }
                );

            CompositionEffectBrush brush = factory.CreateBrush();
            brush.SetSourceParameter("NoiseImage", m_noiseBrush);
            return brush;
        }

        public CompositionPropertySet VisualProperties
        {
            get
            {
                if (!m_setUpExpressions)
                {
                    SetUpPropertySetExpressions();
                }
                return m_rootVisual.Properties;
            }
        }

#endif

    }

    public delegate CompositionDrawingSurface LoadTimeEffectHandler(CanvasBitmap bitmap, CompositionGraphicsDevice device, Size sizeTarget);

    public class SurfaceLoader
    {
        private static bool _intialized;
        private static Compositor _compositor;
        private static CanvasDevice _canvasDevice;
        private static CompositionGraphicsDevice _compositionDevice;

        static public void Initialize(Compositor compositor)
        {
            Debug.Assert(!_intialized || compositor == _compositor);

            if (!_intialized)
            {
                _compositor = compositor;
                _canvasDevice = new CanvasDevice();
                _compositionDevice = CanvasComposition.CreateCompositionGraphicsDevice(_compositor, _canvasDevice);

                _intialized = true;
            }
        }

        static public void Uninitialize()
        {
            _compositor = null;

            if (_compositionDevice != null)
            {
                _compositionDevice.Dispose();
                _compositionDevice = null;
            }

            if (_canvasDevice != null)
            {
                _canvasDevice.Dispose();
                _canvasDevice = null;
            }

            _intialized = false;
        }

        static public bool IsInitialized
        {
            get
            {
                return _intialized;
            }
        }

        static public async Task<CompositionDrawingSurface> LoadFromUri(Uri uri)
        {
            return await LoadFromUri(uri, Size.Empty);
        }

        static public async Task<CompositionDrawingSurface> LoadFromUri(Uri uri, Size sizeTarget)
        {
            Debug.Assert(_intialized);

            CanvasBitmap bitmap = await CanvasBitmap.LoadAsync(_canvasDevice, uri);
            Size sizeSource = bitmap.Size;

            if (sizeTarget.IsEmpty)
            {
                sizeTarget = sizeSource;
            }

            CompositionDrawingSurface surface = _compositionDevice.CreateDrawingSurface(sizeTarget,
                                                            DirectXPixelFormat.B8G8R8A8UIntNormalized, DirectXAlphaMode.Premultiplied);
            using (var ds = CanvasComposition.CreateDrawingSession(surface))
            {
                ds.Clear(Color.FromArgb(0, 0, 0, 0));
                ds.DrawImage(bitmap, new Rect(0, 0, sizeTarget.Width, sizeTarget.Height), new Rect(0, 0, sizeSource.Width, sizeSource.Height));
            }

            return surface;
        }

        static public CompositionDrawingSurface LoadText(string text, Size sizeTarget, CanvasTextFormat textFormat, Color textColor, Color bgColor)
        {
            Debug.Assert(_intialized);

            CompositionDrawingSurface surface = _compositionDevice.CreateDrawingSurface(sizeTarget,
                                                            DirectXPixelFormat.B8G8R8A8UIntNormalized, DirectXAlphaMode.Premultiplied);
            using (var ds = CanvasComposition.CreateDrawingSession(surface))
            {
                ds.Clear(bgColor);
                ds.DrawText(text, new Rect(0, 0, sizeTarget.Width, sizeTarget.Height), textColor, textFormat);
            }

            return surface;
        }

        static public async Task<CompositionDrawingSurface> LoadFromUri(Uri uri, Size sizeTarget, LoadTimeEffectHandler loadEffectHandler)
        {
            Debug.Assert(_intialized);

            if (loadEffectHandler != null)
            {
                CanvasBitmap bitmap = await CanvasBitmap.LoadAsync(_canvasDevice, uri);
                return loadEffectHandler(bitmap, _compositionDevice, sizeTarget);
            }
            else
            {
                return await LoadFromUri(uri, sizeTarget);
            }
        }
    }
}
