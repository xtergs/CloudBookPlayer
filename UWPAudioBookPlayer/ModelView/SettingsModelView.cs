using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using GalaSoft.MvvmLight.Command;
using PropertyChanged;
using UWPAudioBookPlayer.Annotations;
using UWPAudioBookPlayer.DAL.Model;
using UWPAudioBookPlayer.Service;

namespace UWPAudioBookPlayer.ModelView
{
    [ImplementPropertyChanged]
    public class SettingsModelView : ISettingsService, INotifyPropertyChanged
    {
        private IApplicationSettingsHelper helper;
        private MainControlViewModel mainViewModel;
        public SettingsModelView(IApplicationSettingsHelper helper, MainControlViewModel mainViewModel)
        {
            if (helper == null)
                throw new ArgumentNullException(nameof(helper));
            if (mainViewModel == null)
                throw new ArgumentNullException(nameof(mainViewModel));
            this.helper = helper;
            this.mainViewModel = mainViewModel;

            RemoveCloudController = new RelayCommand<ICloudController>((controller) =>
            {
                if (controller == null)
                    return;
                this.mainViewModel.CloudControllers.Remove(controller);
                OnPropertyChanged(nameof(Controllers));
            });
        }

        public List<ICloudController> Controllers
        {
            get { return mainViewModel.OnlyCloudControolers; }
        }

        public bool AutomaticaliDeleteFilesFromDrBox {
            get { return helper.SimpleGet(false); }
            set { helper.SimpleSet(value); }
          }

        public bool AskBeforeDeletionBook {
            get { return helper.SimpleGet(true); }
            set { helper.SimpleSet(value); }
        }

        public RelayCommand<ICloudController> RemoveCloudController { get; }

        public MainControlViewModel MainViewModel
        {
            get { return mainViewModel; }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        [NotifyPropertyChangedInvocator]
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
