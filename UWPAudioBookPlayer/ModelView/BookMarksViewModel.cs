using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Windows.Media.Core;
using Windows.Media.Playback;
using GalaSoft.MvvmLight.Command;
using PropertyChanged;
using UWPAudioBookPlayer.DAL;
using UWPAudioBookPlayer.Helper;
using UWPAudioBookPlayer.Model;

namespace UWPAudioBookPlayer.ModelView
{
    [ImplementPropertyChanged]
    public class BookMarkWrapper
    {
        public BookMarkWrapper(RelayCommand<BookMark> play, RelayCommand<BookMark> delete)
        {
            Play = play;
            DeleteBookMarks = delete;
        }
        private bool _haveRealFile;
        public BookMark BookMark { get; set; }

        public bool HaveRealFile
        {
            get { return _haveRealFile; }
            set
            {
                _haveRealFile = value;
                Play.RaiseCanExecuteChanged();
            }
        }

        public bool HaveError { get; set; }

        public RelayCommand<BookMark> Play { get; set; }
        public RelayCommand<BookMark> PlayInMainPlayer { get; set; }
        public RelayCommand<BookMark> DeleteBookMarks { get; set; }
    }



    [ImplementPropertyChanged]
    public class BookMarksViewModel
    {
        private AudioBookSourceFactory factory;
        private List<BookMark> _bookMarks;
        private INotification notificator;
        private IDataRepository repository;

        public BookMarksViewModel(INotification notificator, IDataRepository repository)
        {
            this.notificator = notificator;
            this.repository = repository;
            factory = new AudioBookSourceFactory();
            PlayBookMarkCommand = new RelayCommand<BookMark>(PlayBookMark, CanPlayBookMark);
            PlayBookMarkInMainPlayerCommand = new RelayCommand<BookMark>(PlayBookMarkInMainPlayer);
            ClearAllCommand = new RelayCommand(ClearAll);
            RefreshAllCommand = new RelayCommand(RefreshAllBookMarksC);
            DeleteBookMarkCommand = new RelayCommand<BookMark>(DeleteBookMark);
        }

        private async void ClearAll()
        {
            if (!Wrappers.Any())
                return;
            if (Wrappers.Any(x => x.HaveRealFile))
            {
                var res =
                    await
                        notificator.ShowMessage("", "Are you really want delete all bookmarks, also from disk?",
                            ActionButtons.No, ActionButtons.Yes);
                if (res == ActionButtons.No)
                    return;
            }
            IsBusy = true;
            try
            {
                var result = await factory.ClearAllBookMarks(true, AudioBook);
                if (!result)
                {
                    notificator.ShowMessage("Error", "Occurend an error during deleting bookmarks");
                }
                await RefreshAllBookMarks();
            }
            finally
            {
                IsBusy = false;
            }
        }

        private bool CanPlayBookMark(BookMark bookMark)
        {
            return MediaPlayer != null && PlayingBookMark != bookMark;
        }

        public async Task CheckRealBookMarks()
        {
            var tasks = await Task.WhenAll(factory.GetBookMarks(AudioBook),
            factory.GetBookFiles(AudioBook));
            var marks = tasks[0];
            var tempDict =
                Wrappers.Select(x => new {BookMark = x, FileName = x.BookMark.FileName}).ToList();
            List<string> foundFiles = new List<string>();
            foreach (var mark in marks)
            {
                var m = tempDict.SingleOrDefault(x => x.FileName == mark);
                if (m == null)
                    continue;
                m.BookMark.HaveRealFile = true;
                foundFiles.Add(mark);
            }
            var files = tasks[1];
            foreach (var file in files)
            {
                var m = tempDict.FirstOrDefault(x => x.BookMark.BookMark.FileName == file);
                if (m == null)
                    continue;
                m.BookMark.HaveRealFile = true;
            }
            foreach (var mark in marks.Except(foundFiles).ToArray())
            {
                Wrappers.Add(new BookMarkWrapper(PlayBookMarkCommand, DeleteBookMarkCommand) {BookMark = factory.GetBookMarkFromFileName(mark), HaveRealFile = true});
            }
        }

        public MediaPlayer MediaPlayer { get; set; }

        public AudioBookSourceWithClouds AudioBook { get; set; }
        public bool IsBusy { get; set; }

        public List<BookMark> BookMarks
        {
            get { return _bookMarks; }
            set
            {
                _bookMarks = value;

                Wrappers = new ObservableCollection<BookMarkWrapper>(_bookMarks.Select(x=> new BookMarkWrapper(PlayBookMarkCommand, DeleteBookMarkCommand)
                {
                    BookMark = x,
                    PlayInMainPlayer = PlayBookMarkInMainPlayerCommand,
                }));
                
            }
        }

        public BookMark PlayingBookMark { get; set; }
        
        private RelayCommand<BookMark> PlayBookMarkCommand { get; set; }
        private RelayCommand<BookMark> PlayBookMarkInMainPlayerCommand { get; set; }
        public RelayCommand<AudioBookSourceWithClouds> ExternalPlayCommand { get; set; }
        public RelayCommand ClearAllCommand { get; private set; }
        public RelayCommand RefreshAllCommand { get; private set; }
        public RelayCommand<BookMark> DeleteBookMarkCommand { get; private set; }

        public async void PlayBookMark(BookMark mark)
        {
            PlayingBookMark = mark;
            var open = await factory.GetBookMark(AudioBook, mark);
            MediaPlayer.Source = MediaSource.CreateFromStream(open.Value.AsRandomAccessStream(), open.Key);
            MediaPlayer.Play();
        }

        private void PlayBookMarkInMainPlayer(BookMark obj)
        {
            AudioBook.CurrentFile = (int)AudioBook.Files.FirstOrDefault(x => x.Name == obj.FileName).Order;
            AudioBook.Position = obj.Position;
            ExternalPlayCommand.Execute(AudioBook);
        }

        public async void RefreshAllBookMarksC()
        {
            IsBusy = true;
            try
            {
                await RefreshAllBookMarks();
            }
            finally
            {
                IsBusy = false;
            }
        }

        public Task RefreshAllBookMarks()
        {
            var bookMarks = repository.BookMarks(AudioBook);
            BookMarks = bookMarks.ToList();
            return CheckRealBookMarks();
        }

        public async void DeleteBookMark(BookMark bookMark)
        {
            var wrapper = Wrappers.FirstOrDefault(w => w.BookMark == bookMark);
            if (bookMark.IsRange && wrapper.HaveRealFile)
            {
                var answer = await notificator.ShowMessage("Aware", "BookMark also will be deleted from storage. Continue?",
                    ActionButtons.Cancel, ActionButtons.Continue);
                if (answer == ActionButtons.Cancel)
                    return;

                await factory.DeleteBookMarks(bookMark, AudioBook);
            }

            if (repository.RemoveBookMark(bookMark, AudioBook))
            {
                Wrappers.Remove(wrapper);
                BookMarks.Remove(bookMark);
            }
        }

        public ObservableCollection<BookMarkWrapper> Wrappers { get; set; }

        public bool IsNothingFound => !IsBusy && Wrappers?.Count <= 0;
    }
}
