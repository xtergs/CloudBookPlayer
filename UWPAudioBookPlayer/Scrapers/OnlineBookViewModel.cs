using System.ComponentModel;
using System.Runtime.CompilerServices;
using GalaSoft.MvvmLight.Command;
using UWPAudioBookPlayer.Annotations;

namespace UWPAudioBookPlayer.Scrapers
{
    public class OnlineBookViewModel : INotifyPropertyChanged
    {
        private bool _isExpanded;

        public OnlineBookViewModel(OnlineBook book)
        {
            this.Book = book;
        }

        public OnlineBook Book { get; }

        public RelayCommand<OnlineBook> AddToLiraryExternalCommand { get; set; }
        public RelayCommand DownlaodExternalCommand { get; set; }
        public RelayCommand<OnlineBook> PlayExternalCommand { get; set; }
        public RelayCommand ShowDetailInfoExternalCommand { get; set; }
        public RelayCommand<OnlineBookViewModel> ShowBookInfoExternalCommand { get; set; }

        public bool IsExpanded
        {
            get { return _isExpanded; }
            set
            {
                _isExpanded = value;
                OnPropertyChanged();
            }
        }

        public bool CanBeExpanded { get; set; } = false;
        public event PropertyChangedEventHandler PropertyChanged;

        [NotifyPropertyChangedInvocator]
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}