using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using AudioBooksPlayer.WPF.Streaming;
using Ninject;
using Ninject.Parameters;
using RemoteAudioBooksPlayer.WPF.Logic;
using RemoteAudioBooksPlayer.WPF.ViewModel;

namespace RemoteAudioBooksPlayer.WPF
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private MainViewModel model;
        public MainWindow()
        {
            InitializeComponent();
            KernelBase kernel = new StandardKernel();
            kernel.Bind<MainViewModel>().ToSelf().WithParameter(new Parameter("startupDiscoveryListener", true, true));
            kernel.Bind<StreamPlayer>().ToSelf();

            var mainViewModel = kernel.Get<MainViewModel>();
            model = mainViewModel;
            DataContext = mainViewModel;
        }

        private void TreeView_OnSelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            if (e.NewValue is AudioBookInfoRemote)
                model.SelectedBroadcastAudioBook = (AudioBookInfoRemote) e.NewValue;
            e.Handled = true;
        }
    }
}
