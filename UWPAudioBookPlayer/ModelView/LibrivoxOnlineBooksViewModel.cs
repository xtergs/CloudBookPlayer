using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Data;
using Windows.Web.Http;
using GalaSoft.MvvmLight.Command;
using PropertyChanged;
using UWPAudioBookPlayer.Model;
using UWPAudioBookPlayer.Scrapers;

namespace UWPAudioBookPlayer.ModelView
{
    static class MapperHelper
    {
        public static AudiBookFile ToAudioBookFile(this OnlineBookFile file)
        {
            var audioFile = new AudiBookFile()
            {
                Duration = file.Duration,
                IsAvalible = true,
                Name = Path.GetFileName(file.link),
                Path = file.link,
                Order = (uint) file.Order,
                Chapter = file.Title
            };
            return audioFile;
        }
    }
    [ImplementPropertyChanged]
    public class IncrementalLoadingCollection<T> : ObservableCollection<T>, ISupportIncrementalLoading
    {
        private LoadMoreData loadData;
        private int lang = 0;

        public IncrementalLoadingCollection(LoadMoreData loadataMethod)
        {
            this.loadData = loadataMethod;
        }

        public int CurrentPage = 0;
        public int MaxPageCount = 1;

        public delegate Task<KeyValuePair< List<T>, int>> LoadMoreData(int page);

        private Task<LoadMoreItemsResult> LoadDataAsync()
        {
            var page = Interlocked.Increment(ref  CurrentPage);
            var dispatcher = Window.Current.Dispatcher;
            return Task.Run<LoadMoreItemsResult>(async () =>
            {
                var books = await loadData(page);
                await dispatcher.RunAsync(
                    CoreDispatcherPriority.Normal,
                    () =>
                    {
                        foreach (var item in books.Key)
                            this.Add(item);
                        MaxPageCount = books.Value;
                    });
                return new LoadMoreItemsResult() {Count = (uint) books.Value};
            });
        }

        public IAsyncOperation<LoadMoreItemsResult> LoadMoreItemsAsync(uint count = 0)
        {
            return LoadDataAsync().AsAsyncOperation<LoadMoreItemsResult>();
        }

        public bool HasMoreItems => CurrentPage < MaxPageCount;
    }


    public enum CurrentState
    {
        Authors, AuthorBooks, Titles, Genries, Book
    }

    [ImplementPropertyChanged]
    class LibrivoxOnlineBooksViewModel : INotifyPropertyChanged
    {
        private LIbriVoxScraper scraper;
        private Stack<CurrentState> state = new Stack<CurrentState>(3);
        private OnlineBook _selectedBook;
        private OnlineAuthor _selectedAuthor;

        private Windows.UI.Core.CoreDispatcher dispatcher;
        

        private Dictionary<string, OnlineBook> chachedFullBooks = new Dictionary<string, OnlineBook>(10);

        public LibrivoxOnlineBooksViewModel(LIbriVoxScraper scraper)
        {
            if (scraper == null)
                throw new ArgumentNullException(nameof(scraper));
            this.scraper = scraper;
            dispatcher = Window.Current.Dispatcher;
            AddBookToLibraryCommand = new RelayCommand<OnlineBook>(AddBookToLibrary);


            ShowBooksByTitleCommand = new RelayCommand(ShowBooksByTitle);
            ShowAuthorsCommand = new RelayCommand(ShowAuthors);
            ShowBookCommand = new RelayCommand<OnlineBook>(ShowBook);
            ShowAuthorsBooksCommand = new RelayCommand<OnlineAuthor>(ShowAuthorBooks);

        }

        public bool FetchingBook { get; set; } = false;
        public bool FetchingData { get; set; } = false;

        public bool BackIfCan()
        {
            if (state.Count <= 1)
                return false;
            state.Pop();
            ChangeVisibility();
            return true;
        }

        private void ShowAuthorBooks(OnlineAuthor obj)
        {
            state.Push(CurrentState.AuthorBooks);
            SelectedAuthor = obj;
            BookList = new IncrementalLoadingCollection<OnlineBook>(LoadMoreBooksByAuthor);
                IsShowBookList = true;
            IsShowAuthorList = false;
            IsShowBookInfo = false;
        }

        private async Task<OnlineBook> GetBook(OnlineBook obj)
        {
            if (chachedFullBooks.ContainsKey(obj.link))
            {
                return chachedFullBooks[obj.link];
            }
            Stream stream = null;
            using (var client = new HttpClient())
            {
                client.DefaultRequestHeaders.Add("X-Requested-With", "XMLHttpRequest");
                stream = (await client.GetInputStreamAsync(new Uri(obj.link))).AsStreamForRead();
            }
            if (stream == null)
                return null;
            var fullBook = scraper.ParseBookPage(stream);
            fullBook.link = obj.link;
            chachedFullBooks.Add(obj.link, fullBook);
            return fullBook;
        }

        private async void ShowBook(OnlineBook obj)
        {
            if (FetchingBook)
                return;
            try
            {
                FetchingBook = true;
                IsShowBookList = false;
                IsShowAuthorList = false;
                IsShowBookInfo = true;
                state.Push(CurrentState.Book);
                var fullBook = GetBook(obj);
                SelectedBook = await fullBook;
            }
            finally
            {
                FetchingBook = false;
            }
        }

        public async Task LoadData()
        {
            Stream stream = null;
            using (var client = new HttpClient())
            {
                client.DefaultRequestHeaders.Add("X-Requested-With", "XMLHttpRequest");
                stream  = (await client.GetInputStreamAsync(new Uri(scraper.GetLinkToLanguages()))).AsStreamForRead();
            }
            if (stream == null)
                return;
            Languges = scraper.GetLanguages(stream);
            SelectedLanguage = Languges.First();
        }

        private async void ShowBooksByTitle()
        {
            state.Clear();
            state.Push(CurrentState.Titles);
            if (BookList == null)
            {
                BookList = new IncrementalLoadingCollection<OnlineBook>(LoadMoreByTitle);
            }
            IsShowAuthorList = false;
            IsShowBookList = true;
            IsShowBookInfo = false;
        }

        private async void ShowAuthors()
        {
            state.Clear();
            state.Push(CurrentState.Authors);
            if (AuthorList == null)
            {
                AuthorList = new IncrementalLoadingCollection<OnlineAuthor>(LoadMoreByAuthor);
            }
            IsShowAuthorList = true;
            IsShowBookList = false;
            IsShowBookInfo = false;
        }

        private async Task<KeyValuePair<List<OnlineBook>, int>> LoadMoreByTitle(int page)
        {
            FetchingData = true;
            try
            {
                var link = scraper.GetLinkToTitles(page, SelectedLanguage.primaryKey, SelectedProjectTyp,
                    SelectedSortOrder);
                using (var client = new HttpClient())
                {
                    client.DefaultRequestHeaders.Add("X-Requested-With", "XMLHttpRequest");
                    var stream = (await client.GetInputStreamAsync(new Uri(link))).AsStreamForRead();
                    var data = scraper.ParseListBookByTitle(stream);
                    return new KeyValuePair<List<OnlineBook>, int>(data.Books, data.MaxPageCount);
                }
            }
            finally
            {
                FetchingData = false;
            }

        }


        private async Task<KeyValuePair<List<OnlineAuthor>, int>> LoadMoreByAuthor(int page)
        {
            FetchingData = true;
            try
            {
                var link = scraper.GetLinkToAuthors(page, SelectedProjectTyp, SelectedSortOrder);
                using (var client = new HttpClient())
                {
                    client.DefaultRequestHeaders.Add("X-Requested-With", "XMLHttpRequest");
                    var stream = (await client.GetInputStreamAsync(new Uri(link))).AsStreamForRead();
                    var data = scraper.ParseListAuthor(stream);
                    return new KeyValuePair<List<OnlineAuthor>, int>(data, data.MaxPageCount);
                }
            }
            finally
            {
                FetchingData = false;
            }
        }

        private async Task<KeyValuePair<List<OnlineBook>, int>> LoadMoreBooksByAuthor(int page)
        {
            FetchingData = true;
            try
            {
                var link = scraper.GetLinkAuthorBooks(SelectedAuthor.AuthorId, page, SelectedSortOrder,
                    SelectedProjectTyp);
                using (var client = new HttpClient())
                {
                    client.DefaultRequestHeaders.Add("X-Requested-With", "XMLHttpRequest");
                    var stream = (await client.GetInputStreamAsync(new Uri(link))).AsStreamForRead();
                    var data = scraper.ParseListBookByTitle(stream);
                    return new KeyValuePair<List<OnlineBook>, int>(data.Books, data.MaxPageCount);
                }
            }
            finally
            {
                FetchingData = false;
            }

        }

        public int CurrentPage { get; set; } = 1;
        public int MaxPage { get; set; } = 1;



        public IncrementalLoadingCollection<OnlineBook> BookList { get; private set; }
        public IncrementalLoadingCollection<OnlineAuthor> AuthorList { get; private set; }

        public OnlineBook SelectedBook
        {
            get { return _selectedBook; }
            set
            {
                try
                {
                    if (_selectedBook?.link == value?.link)
                    {
                        _selectedBook = value;
                        return;
                    }
                    _selectedBook = value;
                    if (value == null)
                    {
                        if (!state.Any())
                            return;
                        var st = state.Pop();
                    }
                    else
                        ShowBook(value);
                }
                finally
                {
                    OnPropertyChanged();
                }
            }
        }

        public OnlineAuthor SelectedAuthor
        {
            get { return _selectedAuthor; }
            set
            {
                if (_selectedAuthor == value)
                    return;
                _selectedAuthor = value;
                if (value == null)
                {
                    state.Pop();
                    BookList = null;
                }
                else
                    ShowAuthorBooks(value);
            }
        }

        public void ChangeVisibility()
        {
            switch (state.Peek())
            {
                    case CurrentState.Titles:
                    IsShowBookList = true;
                    IsShowAuthorList = false;
                    IsShowBookInfo = false;
                    break;
                    case CurrentState.Authors:
                    IsShowBookList = false;
                    IsShowAuthorList = true;
                    IsShowBookInfo = false;
                    break;
                    case CurrentState.AuthorBooks:
                    IsShowBookList = true;
                    IsShowAuthorList = false;
                    IsShowBookInfo = false;
                    break;
                    case CurrentState.Book:
                    IsShowBookList = false;
                    IsShowAuthorList = false;
                    IsShowBookInfo = true;
                    break;
            }
        }

        public List<Language> Languges { get; set; }
        public Language SelectedLanguage { get; set; }
        public ProjectTyp SelectedProjectTyp { get; set; } = ProjectTyp.All;
        public SortOrder SelectedSortOrder { get; set; } = SortOrder.alpha;


        public bool IsShowBookList { get; set; }
        public bool IsShowAuthorList { get; set; }
        public bool IsShowBookInfo { get; set; } = false;


        public RelayCommand ShowBooksByTitleCommand { get; private set; }
        public RelayCommand ShowAuthorsCommand { get; private set; }
        public RelayCommand<OnlineAuthor> ShowAuthorsBooksCommand { get; private set; }
        public RelayCommand<OnlineBook> ShowBookCommand { get; private set; }


        public RelayCommand<OnlineBook> AddBookToLibraryCommand { get; }

        private async void AddBookToLibrary(OnlineBook book)
        {
            book = await GetBook(book);
            OnlineAudioBookSource source = new OnlineAudioBookSource("LibviVox", CloudType.Online)
            {
                Name = book.BookName,
                Cover = book.CoverLink,
                IsLocked = true,
                TotalDuration = book.Duration,
                Link = book.link,
                Path = "LibriVox\\" + book.BookName,
                HostLink = "https://librivox.org",
                Files = book.Files.Select(x => x.ToAudioBookFile()).ToList(),
            };
            if (AddSourceToLibrary == null)
                throw new ArgumentNullException(nameof(AddSourceToLibrary));
            AddSourceToLibrary.Execute(source);
        }

        public RelayCommand<AudioBookSourceWithClouds> AddSourceToLibrary { get; set; }


        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual async void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            await dispatcher.RunAsync(CoreDispatcherPriority.Normal, ()=>
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName)));
        }
    }
}
