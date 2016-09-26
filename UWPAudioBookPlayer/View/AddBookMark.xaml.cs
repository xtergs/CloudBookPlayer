using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Windows.Input;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;
using GalaSoft.MvvmLight.Command;
using UWPAudioBookPlayer.Model;

// The User Control item template is documented at http://go.microsoft.com/fwlink/?LinkId=234236

namespace UWPAudioBookPlayer.View
{
    public sealed partial class AddBookMark : UserControl
    {
        private BookMark bookmark;
        public AddBookMark()
        {
            this.InitializeComponent();
            this.Loading += OnLoading;
            this.Loaded += OnLoaded;
        }

        private void OnLoaded(object sender, RoutedEventArgs routedEventArgs)
        {
            bookmark = new BookMark()
            {
                FileName = Source.GetCurrentFile.Name,
                Position = Position,
                Title = "",
                Description = ""
            };
            DataContext = bookmark;
            TextBox.Focus(FocusState.Keyboard);
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

        private void SaveClicked(object sender, RoutedEventArgs e)
        {
            SaveCommand.Execute(bookmark);
        }
    }
}
