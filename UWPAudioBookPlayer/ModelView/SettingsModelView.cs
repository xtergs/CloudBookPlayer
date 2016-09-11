using System;
using PropertyChanged;
using UWPAudioBookPlayer.Service;

namespace UWPAudioBookPlayer.ModelView
{
    [ImplementPropertyChanged]
    public class SettingsModelView : ISettingsService
    {
        private IApplicationSettingsHelper helper;
        public SettingsModelView(IApplicationSettingsHelper helper)
        {
            if (helper == null)
                throw new ArgumentNullException(nameof(helper));
            this.helper = helper;
        }

        public bool AutomaticaliDeleteFilesFromDrBox {
            get { return helper.SimpleGet(false); }
            set { helper.SimpleSet(value); }
          }

        public bool AskBeforeDeletionBook {
            get { return helper.SimpleGet(true); }
            set { helper.SimpleSet(value); }
        }
    }
}
