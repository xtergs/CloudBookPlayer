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
using Windows.ApplicationModel.Contacts;
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

        public void ResetPages()
        {
            CurrentPage = 0;
            MaxPageCount = 1;
        }

        public delegate Task<KeyValuePair< List<T>, int>> LoadMoreData(int page);

        private Task<LoadMoreItemsResult> LoadDataAsync()
        {
            var page = Interlocked.Increment(ref  CurrentPage);
            var dispatcher = Window.Current.Dispatcher;
            return Task.Run<LoadMoreItemsResult>(async () =>
            {
                var books = await loadData(page);
                if (books.Key == null)
                {
                    Interlocked.Decrement(ref CurrentPage);
                    return new LoadMoreItemsResult();
                }
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
        private OnlineBookViewModel _selectedBook;
        private OnlineAuthor _selectedAuthor;

        private Windows.UI.Core.CoreDispatcher dispatcher;
        

        private Dictionary<string, OnlineBook> chachedFullBooks = new Dictionary<string, OnlineBook>(10);
        private OnlineBookViewModel _authorSelectedBook;

        public LibrivoxOnlineBooksViewModel(LIbriVoxScraper scraper)
        {
            if (scraper == null)
                throw new ArgumentNullException(nameof(scraper));
            this.scraper = scraper;
            ShowBookForcedCommand = new RelayCommand<OnlineBookViewModel>(ShowBookForced);
            dispatcher = Window.Current.Dispatcher;
            AddBookToLibraryCommand = new RelayCommand<OnlineBook>(AddBookToLibrary);
            PlayBookCommand = new RelayCommand<OnlineBook>(PlayBook);

            RerfreshDataCommand = new RelayCommand(RerfreshData);

            ShowBooksByTitleCommand = new RelayCommand(ShowBooksByTitle);
            ShowAuthorsCommand = new RelayCommand(ShowAuthors);
            ShowBookCommand = new RelayCommand<OnlineBookViewModel>(ShowBook);
            ShowAuthorsBooksCommand = new RelayCommand<OnlineAuthor>(ShowAuthorBooks);

        }

        private async void ShowBookForced(OnlineBookViewModel obj)
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
                var fullBook = await GetBook(obj.Book);
                _selectedBook = GetOnlineBookViewModel(fullBook);
                ShowBook(_selectedBook);
            }
            finally
            {
                FetchingBook = false;
            }
        }

        private void RerfreshData()
        {
            BookList?.Clear();
            BookList?.ResetPages();
            AuthorList?.Clear();
            AuthorList?.ResetPages();
        }


        public bool FetchingBook { get; set; } = false;
        public bool FetchingData { get; set; } = false;

        public bool BackIfCan()
        {
            if (state.Count <= 0)
                return false;
            if (state.Peek() == CurrentState.Book)
            {
                state.Pop();
                IsShowBookInfo = false;
                return true;
            }
            if (state.Peek() == CurrentState.AuthorBooks)
            {
                state.Pop();
                IsShowBookInfo = false;
                IsShowAuthorList = true;
                IsShowBookList = false;
                SelectedAuthor = null;
                return true;
            }
            if (state.Count <= 1)
                return false;
            if (SelectedAuthor != null)
            {
                SelectedAuthor = null;
                return true;
            }
            state.Pop();
            ChangeVisibility();
            return true;
        }

        private void ShowAuthorBooks(OnlineAuthor obj)
        {
            state.Push(CurrentState.AuthorBooks);
            SelectedAuthor = obj;
            AuthorBookList = new IncrementalLoadingCollection<OnlineBookViewModel>(LoadMoreBooksByAuthor);
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
            chachedFullBooks[obj.link] =  fullBook;
            return fullBook;
        }

        private async void ShowBook(OnlineBookViewModel obj)
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
                var fullBook = await GetBook(obj.Book);
                SelectedBook = GetOnlineBookViewModel(fullBook);
            }
            finally
            {
                FetchingBook = false;
            }
        }

        private Task LoadingDataTask { get; set; }
        public bool LoadingData { get; set; }

        public Task LoadData()
        {
            if (LoadingData)
                return LoadingDataTask;
            LoadingDataTask =  LoadDataAsync();
            LoadingData = true;
            try
            {
                return LoadingDataTask;
            }
            finally
            {
                LoadingData = false;
            }

        }

        public bool InicialErrorToDownload { get; set; }
        public bool ConnectionError { get; set; }

        public async Task LoadDataAsync()
        {
            Stream stream = null;
            try
            {
                using (var client = new HttpClient())
                {
                    client.DefaultRequestHeaders.Add("X-Requested-With", "XMLHttpRequest");
                    stream = (await client.GetInputStreamAsync(new Uri(scraper.GetLinkToLanguages()))).AsStreamForRead();
                }
            }
            catch (Exception ex)
            {
                InicialErrorToDownload = true;
                return;
            }
            if (stream == null)
            {
                InicialErrorToDownload = true;
                return;
            }
            Languges = scraper.GetLanguages(stream);
            SelectedLanguage = Languges.First();
            InicialErrorToDownload = false;
        }

        private async void ShowBooksByTitle()
        {
            await LoadData();
            if (BookList == null)
            {
                BookList = new IncrementalLoadingCollection<OnlineBookViewModel>(LoadMoreByTitle);
            }
            IsShowBookInfo = false;
        }

        private async void ShowAuthors()
        {
            if (AuthorList == null)
            {
                AuthorList = new IncrementalLoadingCollection<OnlineAuthor>(LoadMoreByAuthor);
            }
            IsShowAuthorList = true;
            IsShowBookList = false;
            IsShowBookInfo = false;
        }

        private async Task<KeyValuePair<List<OnlineBookViewModel>, int>> LoadMoreByTitle(int page)
        {
            if (SelectedLanguage == null)
                return new KeyValuePair<List<OnlineBookViewModel>, int>(new List<OnlineBookViewModel>(),0 );
            FetchingData = true;
            try
            {
                var link = scraper.GetLinkToTitles(page, SelectedLanguage.primaryKey, SelectedProjectType.Type,
                    SelectedSortOrder.Order);
                using (var client = new HttpClient())
                {
                    client.DefaultRequestHeaders.Add("X-Requested-With", "XMLHttpRequest");
                    var stream = (await client.GetInputStreamAsync(new Uri(link))).AsStreamForRead();
                    var data = scraper.ParseListBookByTitle(stream);
                    var tempViewMOdesl = GetOnlineBooksViewModel(data.Books);
                    return new KeyValuePair<List<OnlineBookViewModel>, int>(tempViewMOdesl, data.MaxPageCount);
                }
            }
            catch (Exception ex)
            {
                return new KeyValuePair<List<OnlineBookViewModel>, int>(null, 0);
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
                try
                {
                    var link = scraper.GetLinkToAuthors(page, SelectedProjectType.Type, SelectedSortOrder.Order);
                    using (var client = new HttpClient())
                    {
                        client.DefaultRequestHeaders.Add("X-Requested-With", "XMLHttpRequest");
                        var stream = (await client.GetInputStreamAsync(new Uri(link))).AsStreamForRead();
                        var data = scraper.ParseListAuthor(stream);
                        return new KeyValuePair<List<OnlineAuthor>, int>(data, data.MaxPageCount);
                    }
                }
                catch (Exception ex)
                {
                    return new KeyValuePair<List<OnlineAuthor>, int>(null, 0);
                }
            }
            finally
            {
                FetchingData = false;
            }
        }

        private OnlineBookViewModel GetOnlineBookViewModel(OnlineBook book)
        {
                    return new OnlineBookViewModel(book)
                    {
                        AddToLiraryExternalCommand = AddBookToLibraryCommand,
                        PlayExternalCommand = PlayBookCommand,
                        ShowBookInfoExternalCommand = ShowBookForcedCommand
                    };
    }

        private List<OnlineBookViewModel> GetOnlineBooksViewModel(IList<OnlineBook> books)
        {
            if (books == null) return null;
            return books.Select(x => GetOnlineBookViewModel(x)).ToList();
        } 

        private async Task<KeyValuePair<List<OnlineBookViewModel>, int>> LoadMoreBooksByAuthor(int page)
        {
            FetchingData = true;
            try
            {
                var link = scraper.GetLinkAuthorBooks(SelectedAuthor.AuthorId, page, SelectedSortOrder.Order,
                    SelectedProjectType.Type);
                using (var client = new HttpClient())
                {
                    client.DefaultRequestHeaders.Add("X-Requested-With", "XMLHttpRequest");
                    var stream = (await client.GetInputStreamAsync(new Uri(link))).AsStreamForRead();
                    var data = scraper.ParseListBookByTitle(stream);
                    var tempViewModesl = GetOnlineBooksViewModel(data.Books);
                    return new KeyValuePair<List<OnlineBookViewModel>, int>(tempViewModesl, data.MaxPageCount);
                }
            }
            finally
            {
                FetchingData = false;
            }

        }

        public int CurrentPage { get; set; } = 1;
        public int MaxPage { get; set; } = 1; 


        public IncrementalLoadingCollection<OnlineBookViewModel> BookList { get; private set; }

        public IncrementalLoadingCollection<OnlineBookViewModel> AuthorBookList { get; private set; }
        public IncrementalLoadingCollection<OnlineAuthor> AuthorList { get; private set; }


        public OnlineBookViewModel AuthorSelectedBook
        {
            get { return _authorSelectedBook; }
            set
            {
                try
                {
                    if (_authorSelectedBook != null)
                        _authorSelectedBook.IsExpanded = false;
                    if (value != null && value.CanBeExpanded)
                    {
                        _authorSelectedBook = value;
                        value.IsExpanded = !value.IsExpanded;
                        return;
                    }
                    if (_authorSelectedBook?.Book?.link == value?.Book?.link)
                    {
                        _authorSelectedBook = value;
                        return;
                    }
                    _authorSelectedBook = value;
                    if (value?.Book == null)
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

        public OnlineBookViewModel SelectedBook
        {
            get { return _selectedBook; }
            set
            {
                try
                {
                    if (_selectedBook != null)
                        _selectedBook.IsExpanded = false;
                    if (value != null && value.CanBeExpanded )
                    {
                        _selectedBook = value;
                        value.IsExpanded = !value.IsExpanded;
                        return;
                    }
                    if (_selectedBook?.Book?.link == value?.Book?.link)
                    {
                        _selectedBook = value;
                        return;
                    }
                    _selectedBook = value;
                    if (value.Book == null)
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
        public ProjectTypeNamed SelectedProjectType { get; set; } = ProjectTypes[0];
        public static ProjectTypeNamed[] ProjectTypes { get; } = new ProjectTypeNamed[] {new ProjectTypeNamed(ProjectType.All, "All"), new ProjectTypeNamed(ProjectType.Group, "Group"), new ProjectTypeNamed(ProjectType.Solo, "Solo"),   };
        public SortOrderNamed SelectedSortOrder { get; set; } = SortOrders[0];

        public static SortOrderNamed[] SortOrders { get; } = new[]
        {new SortOrderNamed(SortOrder.alpha, "Alpha"), new SortOrderNamed(SortOrder.catalog_date, "CatalogDate"),};

        public bool IsShowBookList { get; set; }
        public bool IsShowAuthorList { get; set; }
        public bool IsShowBookInfo { get; set; } = false;


        public RelayCommand RerfreshDataCommand { get; private set; }
        public RelayCommand ShowBooksByTitleCommand { get; private set; }
        public RelayCommand ShowAuthorsCommand { get; private set; }
        public RelayCommand<OnlineAuthor> ShowAuthorsBooksCommand { get; private set; }
        public RelayCommand<OnlineBookViewModel> ShowBookCommand { get; }
        public RelayCommand<OnlineBookViewModel> ShowBookForcedCommand { get; }


        public RelayCommand<OnlineBook> AddBookToLibraryCommand { get; }
        public RelayCommand<OnlineBook> PlayBookCommand { get; }


        private async Task<OnlineAudioBookSource> GetOnlineBookSource(OnlineBook book)
        {
            book = await GetBook(book);
            OnlineAudioBookSource source = new OnlineAudioBookSource("LibviVox", CloudType.Online)
            {
                Name = book.BookName,
                //Cover = book.CoverLink,
                Images = new ImageStruct[] {new ImageStruct( book.CoverLink, book.CoverLink)},
                IsLocked = true,
                TotalDuration = book.Duration,
                Link = book.link,
                Path = "LibriVox\\" + book.BookName,
                HostLink = "https://librivox.org",
                Files = book.Files.Select(x => x.ToAudioBookFile()).ToList(),
            };
            return source;
        }

        private async void AddBookToLibrary(OnlineBook book)
        {
            var source = await GetOnlineBookSource(book);
            if (AddSourceToLibrary == null)
                throw new ArgumentNullException(nameof(AddSourceToLibrary));
            AddSourceToLibrary.Execute(source);
        }

        private async void PlayBook(OnlineBook book)
        {
            var source = await GetOnlineBookSource(book);
            if (AddAndPlayBook == null)
                throw new ArgumentNullException(nameof(AddAndPlayBook));
            AddAndPlayBook.Execute(source);
        }

        public RelayCommand<AudioBookSourceWithClouds> AddSourceToLibrary { get; set; }
        public RelayCommand<AudioBookSourceWithClouds> AddAndPlayBook { get; set; }


        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual async void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            await dispatcher.RunAsync(CoreDispatcherPriority.Normal, ()=>
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName)));
        }
    }
}
