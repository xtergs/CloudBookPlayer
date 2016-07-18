using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using AudioBooksPlayer.WPF.Annotations;
using AudioBooksPlayer.WPF.ExternalLogic;
using AudioBooksPlayer.WPF.Model;
using GalaSoft.MvvmLight.CommandWpf;

namespace AudioBooksPlayer.WPF
{
    public class BaseViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        [NotifyPropertyChangedInvocator]
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public class MainViewModel : BaseViewModel
    {
        private IFileSelectHelper fileSelectHelper;

        private List<AudioBooksInfo> _audioBooks;

        public AudioBooksInfo[] AudioBooks
        {
            get { return _audioBooks.ToArray(); }
            protected set { _audioBooks = new List<AudioBooksInfo>(value); }
        }


        private ICommand addAudioBook;

        public ICommand AddAudioBookCommand
        {
            get
            {
                return new RelayCommand(() =>
                {
                    var folder = fileSelectHelper.SelectFolder();
                    if (folder == null)
                        return;
                    
                });
            }
        }
    }
}
