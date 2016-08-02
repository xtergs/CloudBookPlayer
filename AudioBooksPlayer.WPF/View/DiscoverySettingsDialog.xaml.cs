using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Windows;
using System.Windows.Input;
using AudioBooksPlayer.WPF.Annotations;
using AudioBooksPlayer.WPF.Streaming;
using GalaSoft.MvvmLight.CommandWpf;

namespace AudioBooksPlayer.WPF.View
{
    /// <summary>
    /// Interaction logic for DiscoverySettingsDialog.xaml
    /// </summary>
    public partial class DiscoverySettingsDialog : Window, INotifyPropertyChanged
    {
        private Timer updateTimer;
        private DiscoverModule discoverModule;
        private int _port;

        public int Port
        {
            get { return _port; }
            set
            {
                if (value == _port) return;
                _port = value;
                OnPropertyChanged();
            }
        }

        public int CurrentPort => discoverModule.Port;

        public string Status { get {
            if (discoverModule.IsDiscovered)
            {
                return "Broadcasting...";
            }
            if (!discoverModule.IsDiscovered)
                return "Broadcast stopped";
            return "None";

        } }

        public DiscoverySettingsDialog(DiscoverModule discovery)
        {
            if (discovery == null)
                throw new ArgumentNullException(nameof(discovery));
            this.discoverModule = discovery;
            InitializeComponent();
            Port = discovery.Port;
            DataContext = this;
            SetupCommands();
            updateTimer = new Timer(updateStatus, null, 0, 1000);
        }

        private void updateStatus(object state)
        {
            OnPropertyChanged(nameof(Status));
            OnPropertyChanged(nameof(CurrentPort));
        }

        private void SetupCommands()
        {
            StartDiscoveryCommand = new RelayCommand(StartDiscoveryExecute, StartDiscoveryCanExecute);
            StopDiscoveryCommand = new RelayCommand(StopDiscoveryExecute, StopDiscoveryCanExecute);
            ApplyChangesCommand = new RelayCommand(ApplyChangesExecute, ApplyChangesCanExecute);
            StartListeningCommand = new RelayCommand(StartListenExecute, StartListenCanExecute);
            StopListenCommand = new RelayCommand(StopListenExecute, StopListenCanExecute);
        }

        private bool StopListenCanExecute()
        {
            return discoverModule.IsListening;
        }

        private void StopListenExecute()
        {
            discoverModule.StopListen();
        }

        private bool StartListenCanExecute()
        {
            return !discoverModule.IsListening;
        }

        private void StartListenExecute()
        {
            discoverModule.StartListen();
        }

        private bool ApplyChangesCanExecute()
        {
            return discoverModule.Port != Port;
        }

        private void ApplyChangesExecute()
        {
            try
            {
                discoverModule.Port = Port;
            }
            catch (CantSetPortWhileListeningException e)
            {
                MessageBox.Show("Error while trying to set discovery port\nTry to stop discovery and reasign port");
            }
        }

        private bool StopDiscoveryCanExecute()
        {
            return discoverModule.IsDiscovered;
        }

        private void StopDiscoveryExecute()
        {
            discoverModule.StopDiscovery();
        }

        private bool StartDiscoveryCanExecute()
        {
            return !discoverModule.IsDiscovered;
        }

        private void StartDiscoveryExecute()
        {
            discoverModule.StartDiscoverty(null);
        }

        public ICommand StartDiscoveryCommand { get; private set; }
        public ICommand StopDiscoveryCommand { get; private set; }
        public ICommand StartListeningCommand { get; set; }
        public ICommand StopListenCommand { get; set; }
        public ICommand ApplyChangesCommand { get; private set; }

        public event PropertyChangedEventHandler PropertyChanged;

        [NotifyPropertyChangedInvocator]
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private void ButtonBase_OnClick(object sender, RoutedEventArgs e)
        {
            updateTimer.Dispose();
            this.Close();
        }
    }
}
