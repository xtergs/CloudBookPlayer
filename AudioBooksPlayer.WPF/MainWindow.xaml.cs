using System;
using System.Windows;
using AudioBooksPlayer.WPF.ExternalLogic;
using AudioBooksPlayer.WPF.Properties;
using Microsoft.Practices.Unity;

namespace AudioBooksPlayer.WPF
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private UnityContainer container;
        private MainViewModel main;
        public MainWindow()
        {
            InitializeComponent();
            container = new UnityContainer();
            container.RegisterType<MainViewModel>();
            container.RegisterType<IFileSelectHelper, WPFFileSelectHelper>();
            main = container.Resolve<MainViewModel>(new ParameterOverride("startupDiscovery", Settings.Default.SturtupDiscovery));

            DataContext = main;
        }

        private void MainWindow_OnLoaded(object sender, RoutedEventArgs e)
        {
            main.LoadData.Execute(null);
        }

        private void MainWindow_OnClosed(object sender, EventArgs e)
        {
            main.StopPlayingAudioBook.Execute(null);
            main.SaveDataCommand.Execute(null);
        }
    }
}
