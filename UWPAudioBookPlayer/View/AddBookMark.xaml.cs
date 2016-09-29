using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using System.Windows.Input;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Globalization;
using Windows.Media.Core;
using Windows.Media.SpeechRecognition;
using Windows.UI.Core;
using Windows.UI.Popups;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;
using Autofac;
using GalaSoft.MvvmLight.Command;
using UWPAudioBookPlayer.Model;
using UWPAudioBookPlayer.ModelView;

// The User Control item template is documented at http://go.microsoft.com/fwlink/?LinkId=234236

namespace UWPAudioBookPlayer.View
{
    public sealed partial class AddBookMark : Page
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
            player.MediaOpened += PlayerOnMediaOpened;
            player.MediaFailed += PlayerOnMediaFailed;
            SaveCommand = viewModel.AddBookMarkCommand;
        }

        private async void PlayerOnMediaFailed(object sender, ExceptionRoutedEventArgs exceptionRoutedEventArgs)
        {
            player.MediaFailed -= PlayerOnMediaFailed;
            player.MediaOpened -= PlayerOnMediaOpened;

            await new MessageDialog("Occured error opening media!").ShowAsync();

            Frame.GoBack();
        }

        private void PlayerOnMediaOpened(object sender, RoutedEventArgs routedEventArgs)
        {
            player.MediaFailed -= PlayerOnMediaFailed;
            player.MediaOpened -= PlayerOnMediaOpened;
            bookmark.EndPosition = player.NaturalDuration.TimeSpan;
            player.Position = bookmark.Position;
            OpeningMedia = false;
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
            var stream = await book.GetFileStream(file.Name);
            OpeningMedia = true;
            player.SetSource(stream.Item2, stream.Item1);

            Windows.UI.Core.SystemNavigationManager.GetForCurrentView().AppViewBackButtonVisibility =
                AppViewBackButtonVisibility.Visible;
            SystemNavigationManager.GetForCurrentView().BackRequested += OnBackRequested;
        }

        private void OnBackRequested(object sender, BackRequestedEventArgs backRequestedEventArgs)
        {
            SystemNavigationManager.GetForCurrentView().BackRequested -= OnBackRequested;
            Frame.GoBack();
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
            player.Play();
        }

        private void pausePlayer(object sender, RoutedEventArgs e)
        {
            player.Pause();
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
    }
}
