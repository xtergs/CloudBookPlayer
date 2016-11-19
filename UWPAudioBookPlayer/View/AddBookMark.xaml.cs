using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Windows.Media.Core;
using Windows.Media.Playback;
using Windows.Media.SpeechRecognition;
using Windows.UI.Core;
using Windows.UI.Popups;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;
using Autofac;
using GalaSoft.MvvmLight.Command;
using PropertyChanged;
using UWPAudioBookPlayer.Annotations;
using UWPAudioBookPlayer.DAL.Model;
using UWPAudioBookPlayer.Model;
using UWPAudioBookPlayer.ModelView;

// The User Control item template is documented at http://go.microsoft.com/fwlink/?LinkId=234236

namespace UWPAudioBookPlayer.View
{
    [ImplementPropertyChanged]
    public sealed partial class AddBookMark : Page, INotifyPropertyChanged
    {
        private BookMark bookmark;
        private AudioBookSourceWithClouds book;
        private AudiBookFile file;
        private MainControlViewModel viewModel;

       public bool OpeningMedia { get; private set; }

        public AddBookMark()
        {
            this.InitializeComponent();
            this.Loading += OnLoading;
            this.Loaded += OnLoaded;
            viewModel = Global.container.Resolve<MainControlViewModel>();
            book = viewModel.PlayingSource;
            file = book.GetCurrentFile;
            player.MediaPlayer.MediaOpened += PlayerOnMediaOpened;
            player.MediaPlayer.MediaFailed += PlayerOnMediaFailed;
            SaveCommand = viewModel.AddBookMarkCommand;
        }

        private async void PlayerOnMediaFailed(MediaPlayer mediaPlayer, MediaPlayerFailedEventArgs args)
        {
            await Windows.ApplicationModel.Core.CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, async () =>
            {
                player.MediaPlayer.MediaFailed -= PlayerOnMediaFailed;
                player.MediaPlayer.MediaOpened -= PlayerOnMediaOpened;

                await new MessageDialog("Occured error opening media!").ShowAsync();
            });

            Frame.GoBack();
        }

        public TimeSpan MaxDurations { get; set; }

        private async void PlayerOnMediaOpened(MediaPlayer mediaPlayer, object args)
        {
            await Windows.ApplicationModel.Core.CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                if (player == null)
                    return;
                player.MediaPlayer.MediaFailed -= PlayerOnMediaFailed;
                player.MediaPlayer.MediaOpened -= PlayerOnMediaOpened;
                MaxDurations = player.MediaPlayer.NaturalDuration;
                bookmark.EndPosition = player.MediaPlayer.NaturalDuration;
                player.MediaPlayer.Position = bookmark.Position;
                OpeningMedia = false;
                //Bindings.Update();
            });
        }

        private async void OnLoaded(object sender, RoutedEventArgs routedEventArgs)
        {
            bookmark = new BookMark()
            {
                FileName = book.GetCurrentFile.Name,
                IsRange = false,
                Position = book.Position,
                Title = "",
                Description = ""
            };
            DataContext = bookmark;
            TextBox.Focus(FocusState.Keyboard);
            OpeningMedia = true;
            if (book is AudioBookSourceCloud)
            {
                var controllers  = Global.container.Resolve<MainControlViewModel>().CloudControllers;
                var controler = controllers.FirstOrDefault(x => x.CloudStamp == (book as AudioBookSourceCloud).CloudStamp);
                if (controler == null)
                    return;
                var link = await controler.GetLink(book as AudioBookSourceCloud, book.CurrentFile);

                player.MediaPlayer.Source = MediaSource.CreateFromUri(new Uri(link));
            }
            else
            {
                var stream = await book.GetFileStream(file.Name);
                player.MediaPlayer.Source = MediaSource.CreateFromStream(stream.Item2, stream.Item1);
                
            }
            Windows.UI.Core.SystemNavigationManager.GetForCurrentView().AppViewBackButtonVisibility =
                AppViewBackButtonVisibility.Visible;
            SystemNavigationManager.GetForCurrentView().BackRequested += OnBackRequested;
        }

        private void OnBackRequested(object sender, BackRequestedEventArgs backRequestedEventArgs)
        {
            SystemNavigationManager.GetForCurrentView().BackRequested -= OnBackRequested;
            Frame.GoBack();
        }

        protected override void OnNavigatingFrom(NavigatingCancelEventArgs e)
        {
            base.OnNavigatingFrom(e);
            player.MediaPlayer.Pause();
            player.Source = null;
            player.PosterSource = null;
            player = null;
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            if (e.Parameter is AudioBookSourceWithClouds)
            {
                book = e.Parameter as AudioBookSourceWithClouds;
                file = book.GetCurrentFile;
            }
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            base.OnNavigatedFrom(e);
            SystemNavigationManager.GetForCurrentView().BackRequested -= OnBackRequested;
        }

        private void OnLoading(FrameworkElement sender, object args)
        {
            
        }

        public RelayCommand<BookMark> SaveCommand
        {
            get { return (RelayCommand< BookMark>) GetValue(SaveCommandProperty); }
            set { SetValue(SaveCommandProperty, value); }
        }

        public static readonly DependencyProperty SaveCommandProperty = DependencyProperty.Register(nameof(SaveCommand), typeof(RelayCommand<BookMark>),
            typeof(AddBookMark), new PropertyMetadata(null, PropertyChangedCallback));

        private static void PropertyChangedCallback(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs dependencyPropertyChangedEventArgs)
        {
            if (dependencyPropertyChangedEventArgs.NewValue == null &&
                dependencyPropertyChangedEventArgs.OldValue != null)
                dependencyObject.SetValue(dependencyPropertyChangedEventArgs.Property,
                    dependencyPropertyChangedEventArgs.OldValue);

        }


        public TimeSpan Position
        {
            get { return (TimeSpan) GetValue(PositionProperty); }
            set { SetValue(PositionProperty, value); }
        }

        public static readonly DependencyProperty PositionProperty = DependencyProperty.Register(nameof(Position), typeof(TimeSpan), typeof(AddBookMark), new PropertyMetadata(null));

        public AudioBookSource Source
        {
            get { return (AudioBookSource) GetValue(SourceProperty); }
            set { SetValue(SourceProperty, value); }
        }

        public static readonly DependencyProperty SourceProperty = DependencyProperty.Register(nameof(Source),
            typeof(AudioBookSource), typeof(AddBookMark), new PropertyMetadata(null, SourceCallBack));

        private static void SourceCallBack(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs dependencyPropertyChangedEventArgs)
        {
            if (dependencyPropertyChangedEventArgs.NewValue == null &&
                dependencyPropertyChangedEventArgs.OldValue != null)
                dependencyObject.SetValue(dependencyPropertyChangedEventArgs.Property,
                    dependencyPropertyChangedEventArgs.OldValue);
        }

        private async void SaveClicked(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(bookmark.Title))
            {
                await new MessageDialog("Title shouldn't be empty!").ShowAsync();
                return;
            }
            var newBookMark = new BookMark()
            {
                Description = bookmark.Description,
                EndPosition = bookmark.EndPosition,
                FileName = bookmark.FileName,
                IsRange = bookmark.IsRange,
                Position = bookmark.Position,
                Title = bookmark.Title
            };
            SaveCommand.Execute(newBookMark);
            Frame.GoBack();
        }

        private SpeechRecognizer spearchRecognizer = null;
        private async void RecognizeText(object sender, RoutedEventArgs e)
        {
            TextBox.Text = await GetRecognizedText();  



        }

        private void palyPlayer(object sender, RoutedEventArgs e)
        {
            player.MediaPlayer.Play();
        }

        private void pausePlayer(object sender, RoutedEventArgs e)
        {
            player.MediaPlayer.Pause();
        }

        private async Task<string> GetRecognizedText()
        {
            using (spearchRecognizer = new Windows.Media.SpeechRecognition.SpeechRecognizer())
            {
                try
                {
                    await spearchRecognizer.CompileConstraintsAsync();

                    SpeechRecognitionResult result = await spearchRecognizer.RecognizeWithUIAsync();

                    return result.Text;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Exception: {ex.Message}\n{ex.StackTrace}");
                    await
                        new MessageDialog("Somethins wrong with your microphone or settings, please check them")
                            .ShowAsync();
                    return "";
                }
            }
        }

        private async void RecognizeDescriptionText(object sender, RoutedEventArgs e)
        {
            DescriptionTextBox.Text = await GetRecognizedText();
        }

        public event PropertyChangedEventHandler PropertyChanged;

        [NotifyPropertyChangedInvocator]
        private void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
