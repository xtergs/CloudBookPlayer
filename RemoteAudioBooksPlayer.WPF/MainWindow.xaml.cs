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
        public MainWindow()
        {
            InitializeComponent();
            KernelBase kernel = new StandardKernel();
            kernel.Bind<MainViewModel>().ToSelf().WithParameter(new Parameter("startupDiscoveryListener", true, true));
            kernel.Bind<StreamPlayer>().ToSelf();

            var mainViewModel = kernel.Get<MainViewModel>();

            DataContext = mainViewModel;
        }
    }
}
