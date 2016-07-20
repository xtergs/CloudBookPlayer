using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using AudioBooksPlayer.WPF.Annotations;
using AudioBooksPlayer.WPF.DAL;
using AudioBooksPlayer.WPF.ExternalLogic;
using AudioBooksPlayer.WPF.Logic;
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
        private AudioBooksProcessor audioProcessor;
        private AudioPlayer audioPlayer;
        private Context context;


        public MainViewModel(IFileSelectHelper fileSelectHelper, AudioPlayer audioPlayer, Context context)
        {
            if (fileSelectHelper == null)
                throw new ArgumentNullException(nameof(fileSelectHelper));
            if (audioPlayer == null)
                throw new ArgumentNullException(nameof(audioPlayer));
            if (this.context == context)
                throw new ArgumentNullException(nameof(context));

            this.fileSelectHelper = fileSelectHelper;
            this.audioPlayer = audioPlayer;
            this.context = context;

            audioProcessor = new AudioBooksProcessor();
        }

        public bool IsPlaying
        {
            get { return _isPlaying; }
            set
            {
                if (value == _isPlaying)
                    return;
                _isPlaying = value;
                OnPropertyChanged();
            }
        }
        

        public bool IsBussy
        {
            get { return _isBussy; }
            set
            {
                if (value == _isBussy)
                    return;
                _isBussy = value;
                OnPropertyChanged();
            }
        }

        public string BussyStatus
        {
            get { return _bussyStatus; }
            set
            {
                if (value == _bussyStatus)
                    return;
                _bussyStatus = value;
                OnPropertyChanged();
            }
        }

        public AudioBooksInfo[] AudioBooks => context.AudioBooks;

        public AudioFileInfo[] SelectedBookFiles => SelectedAudioBook.Files;

        public AudioBooksInfo SelectedAudioBook
        {
            get { return _selectedAudioBook; }
            set
            {
                if (Equals(value, _selectedAudioBook)) return;
                _selectedAudioBook = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(SelectedBookFiles));
                OnPropertyChanged(nameof(PlaySelectedAudioBook));
            }
        }


        private ICommand addAudioBook;
        private AudioBooksInfo _selectedAudioBook;
        private bool _isPlaying;
        private volatile bool _isBussy;
        private string _bussyStatus;

        public ICommand AddAudioBookCommand
        {
            get
            {
                return new RelayCommand(() =>
                {
                    var folder = fileSelectHelper.SelectFolder();
                    if (folder == null)
                        return;
                    context.AddAudioBook(audioProcessor.ProcessAudoiBookFolder(folder));
                    OnPropertyChanged(nameof(AudioBooks));
                });
            }
        }

        public ICommand PlaySelectedAudioBook
        {
            get
            {
                return new RelayCommand(() =>
                {

                    audioPlayer.PlayAudioBook(SelectedAudioBook);
                    IsPlaying = true;
                }, () => SelectedAudioBook != null);
            }
        }

        public ICommand StopPlayingAudioBook
        {
            get
            {
                return new RelayCommand(() =>
                {
                    audioPlayer.StopPlay();
                    IsPlaying = false;
                }, ()=> IsPlaying);
            }
        }

        public ICommand LoadData
        {
            get
            {
                return new RelayCommand(() =>
                {
                    if (IsBussy)
                        return;
                    IsBussy = true;
                    BussyStatus = "Loading data...";
                    context.LoadData();
                    OnPropertyChanged(nameof(AudioBooks));
                    IsBussy = false;
                }, () => !IsBussy);
            }
        }

        public ICommand SaveDataCommand
        {
            get
            {
                return new RelayCommand(() =>
                {
                    if (IsBussy)
                        return;
                    IsBussy = true;
                    BussyStatus = "Saving data...";
                    context.SaveData();
                    IsBussy = false;
                }, () => !IsBussy);
            }
        }


    }
}
