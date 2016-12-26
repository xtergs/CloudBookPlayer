using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using HtmlAgilityPack;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PropertyChanged;

namespace UWPAudioBookPlayer.Scrapers
{
    public class OnlineBookFile
    {
        public string link { get; set; }
        public string Title { get; set; }
        public string Reader { get; set; }
        public TimeSpan Duration { get; set; }
        public int Order { get; set; }
    }

    [ImplementPropertyChanged]
    public class OnlineBook
    {
        public string link { get; set; }
        public string CoverLink { get; set; }
        public string BookName { get; set; }
        public string Author { get; set; }
        public string AuthorLink { get; set; }
        public string Description { get; set; }
        public string Language { get; set; }
        public string Genries { get; set; }
        public TimeSpan Duration { get; set; }
        public DateTime CatalogDate { get; set; }
        public string ReadBy { get; set; }

        public List<OnlineBookFile> Files { get; set; }
    }

    public class BooksList
    {
        public  List<OnlineBook> Books { get; set; }
        public int MaxPageCount { get; set; }
        public int CurrentPage { get; set; }
    }

    public class OnlineAuthor
    {
        public string FullName { get; set; }
        public int AuthorId { get; set; }
        public string Link { get; set; }
        public int ComplitedBooks { get; set; }
        public int InProgressBooks { get; set; }

    }

    public class OnlineAuthorList : List<OnlineAuthor>
    {
        public int MaxPageCount { get; set; }
        public int CurrentPage { get; set; }
    }

    public enum ProjectType
    {
        Solo, Group,
        All
    }

    public struct ProjectTypeNamed
    {
        public ProjectTypeNamed(ProjectType type, string name)
        {
            Type = type;
            Name = name;
        }
        public ProjectType Type { get; set; }
        public string Name { get; set; }

        public override string ToString()
        {
            return Name;
        }
    }

    public enum SortOrder
    {
        catalog_date,
        alpha
    }

    public struct SortOrderNamed
    {
        public SortOrderNamed(SortOrder order, string name)
        {
            Order = order;
            Name = name;
        }
        public SortOrder Order { get; set; }
        public string Name { get; set; }
        public override string ToString()
        {
            return Name;
        }
    }

    public class Language
    {
        public int primaryKey { get; set; }
        public string LanguageLocal { get; set; }
        public string LanguageEnglish { get; set; }
        public int BookCount { get; set; }
    }
    public class LIbriVoxScraper
    {
        public string GetLinkToTitles(int page, int language = 0, ProjectType type = ProjectType.All, SortOrder order = SortOrder.alpha)
        {
            if (page <= 0)
                page = 1;
            string category = "title";
            if (language != 0)
                category = "language";
            string projectType = "either";
            if (type == ProjectType.Solo)
                projectType = "solo";
            else if (type == ProjectType.Group)
                projectType = "group";
            return
                $@"https://librivox.org/search/get_results?primary_key={language}&search_category={category}&sub_category=&search_page={page}&search_order={order.ToString()}&project_type={projectType}";
        }

        public string GetLinkToAuthors(int page, ProjectType type = ProjectType.All, SortOrder order = SortOrder.alpha)
        {
            if (page <= 0)
                page = 1;
            string projectType = "either";
            if (type == ProjectType.Solo)
                projectType = "solo";
            else if (type == ProjectType.Group)
                projectType = "group";
            return
                $@"https://librivox.org/search/get_results?primary_key=0&search_category=author&sub_category=&search_page={page}&search_order={order.ToString()}&project_type={projectType}";
        }

        public string GetLinkAuthorBooks(int authorId, int page, SortOrder order = SortOrder.alpha, ProjectType type = ProjectType.All)
        {
            if (page <= 0)
                page = 1;
            string projectType = "either";
            if (type == ProjectType.Solo)
                projectType = "solo";
            else if (type == ProjectType.Group)
                projectType = "group";
            return
                $@"https://librivox.org/author/get_results?primary_key={authorId}&search_category=author&sub_category=&search_page={page}&search_order={order}&project_type={projectType}";
        }

        public string GetLinkToLanguages()
        {
            return
                @"https://librivox.org/search/get_results?primary_key=0&search_category=language&sub_category=&search_page=1&search_order=alpha&project_type=either";
        }

        public List<Language> GetLanguages(Stream page)
        {
            byte[] buffer = new byte[1024 * 32];
            int readed = 0;
            StringBuilder builder = new StringBuilder(32 * 1024);
            while ((readed = page.Read(buffer, 0, buffer.Length)) > 0)
            {
                builder.Append(UTF8Encoding.UTF8.GetString(buffer, 0, readed));
            }
            HtmlAgilityPack.HtmlDocument document = new HtmlDocument();

            var jsonResult = ((JObject)JsonConvert.DeserializeObject(builder.ToString()));
            foreach (var x in jsonResult)
            {

                if (x.Key == "results")
                {
                    document.LoadHtml(x.Value.ToString());
                    List<Language> languages = new List<Language>(25);
                    foreach (var node in document.DocumentNode.ChildNodes.Where(xx => xx.Name == "li"))
                    {
                        var lang = ParseLanguageByTag(node);
                        if (lang.BookCount <= 0)
                            continue;
                        languages.Add(lang);
                    }
                    return languages;
                }
            }

            return new List<Language>(0);

        }

        private Language ParseLanguageByTag(HtmlNode node)
        {
            try
            {
                var lang = new Language()
                {
                    LanguageEnglish = node.SelectSingleNode(@"div/h3/a").InnerText,
                    primaryKey = int.Parse(node.SelectSingleNode(@"div/h3/a").Attributes["data-primary_key"].Value),
                    LanguageLocal = node.SelectSingleNode(@"div/p[@class='native-lang']")?.InnerText,
                    BookCount =
                        int.Parse(node.SelectSingleNode(@"div/p/span[1]").InnerText.Replace("books", "").Trim())
                };
                lang.LanguageLocal = lang.LanguageLocal ?? lang.LanguageEnglish;
                return lang;
            }
            catch (NullReferenceException e)
            {
                throw;
            }
        }

        public OnlineBook ParseBookPage(Stream data)
        {
            HtmlAgilityPack.HtmlDocument document = new HtmlDocument();
            document.Load(data);

            var book = new OnlineBook()
            {
                BookName = document.DocumentNode.SelectSingleNode(@"/html/body/div/div[2]/div/h1").InnerText,
                Language = document.DocumentNode.SelectSingleNode(@"/html/body/div/div[2]/div/p[3]").InnerText,
                Genries = document.DocumentNode.SelectSingleNode(@"/html/body/div/div[2]/div/p[2]").InnerText,
                CoverLink =
                    document.DocumentNode.SelectSingleNode(@"/html/body/div/div[2]/div/div[1]/img").Attributes["src"]
                        .Value,
                Author = document.DocumentNode.SelectSingleNode(@"/html/body/div/div[2]/div/p[1]/a").InnerText,
                AuthorLink = document.DocumentNode.SelectSingleNode(@"/html/body/div/div[2]/div/p[1]/a").Attributes["href"].Value,
                Description = document.DocumentNode.SelectSingleNode(@"/html/body/div/div[2]/div/div[2]").InnerText,
                Duration = TimeSpan.Parse(document.DocumentNode.SelectSingleNode(@"/html/body/div/div[1]/div[3]/dl/dd[1]").InnerText),
                ReadBy = document.DocumentNode.SelectSingleNode(@"/html/body/div/div[1]/div[3]/dl/dd[4]").InnerText,
            };

            var files = document.DocumentNode.SelectNodes(@"/html/body/div/div[2]/table/tbody/tr");

            book.Files = new List<OnlineBookFile>(files.Count);

            var headers =
                document.DocumentNode.SelectNodes(@"/html/body/div/div[2]/table/thead/tr/th")
                    .Select(x => x.InnerText.ToLower())
                    .ToArray();

            int orderCounter = 1;
            foreach (var file in files)
            {
                var onlineFile = ParseBookFileFromTag(file, headers);
                onlineFile.Order = orderCounter++;
                book.Files.Add(onlineFile);
            }


            return book;
        }

        

        private OnlineBookFile ParseBookFileFromTag(HtmlNode node, string[] mapper)
        {
            try
            {
                var file = new OnlineBookFile()
                {
                    link = node.SelectSingleNode(@"td[2]/a").Attributes["href"].Value,
                    Title = node.SelectSingleNode(@"td[2]/a").InnerText,
                };
                string param = "reader";
                if (mapper.Contains(param))
                    file.Reader = node.SelectSingleNode($@"td[{Array.IndexOf(mapper, param)+1}]/a")?.InnerText;
                param = "time";
                if (mapper.Contains(param))
                    file.Duration = TimeSpan.Parse(node.SelectSingleNode($@"td[{Array.IndexOf(mapper, param)+1}]").InnerText);
                return file;
            }
            catch (Exception e)
            {
                throw;
            }
        }

        public BooksList ParseListBookByTitle(Stream data)
        {
            byte[] buffer = new byte[1024 * 32];
            int readed = 0;
            StringBuilder builder = new StringBuilder(32 * 1024);
            while ((readed = data.Read(buffer, 0, buffer.Length)) > 0)
            {
                builder.Append(UTF8Encoding.UTF8.GetString(buffer, 0, readed));
            }
            HtmlAgilityPack.HtmlDocument document = new HtmlDocument();

            var jsonResult = ((JObject)JsonConvert.DeserializeObject(builder.ToString()));
            var booksList = new BooksList();
            foreach (var x in jsonResult)
            {

                if (x.Key == "results")
                {
                    document.LoadHtml(x.Value.ToString());
                    List<OnlineBook> books = new List<OnlineBook>(25);
                    foreach (var node in document.DocumentNode.ChildNodes.Where(xx => xx.Name == "li"))
                    {
                        books.Add(ParseShortBookDescriptionFromTag(node));
                    }
                    booksList.Books = books;
                }
                if (x.Key == "pagination")
                {
                    document.LoadHtml(x.Value.ToString());
                    booksList.MaxPageCount =
                        int.Parse(
                            document.DocumentNode.SelectSingleNode(@"a[last()]")?.Attributes
                                ["data-page_number"].Value ?? "0");
                }
            }

            return booksList;
        }

        private OnlineBook ParseShortBookDescriptionFromTag(HtmlNode node)
        {
            try
            {
                var book = new OnlineBook()
                {
                    BookName = node.SelectSingleNode(@"div[1]/h3/a").InnerText,
                    link = node.SelectSingleNode(@"div[1]/h3/a").Attributes["href"].Value,
                    Author = node.SelectSingleNode(@"div[1]/p[1]/a")?.InnerText ?? node.SelectSingleNode(@"div[1]/p")?.InnerText,
                    AuthorLink = node.SelectSingleNode(@"div[1]/p[1]/a")?.Attributes["href"]?.Value,
                    CoverLink = node.SelectSingleNode(@"a/img")?.Attributes["src"]?.Value,
                    Genries = node.SelectSingleNode(@"div[1]/p[2]").InnerText
                };

                return book;
            }
            catch (NullReferenceException e)
            {
                throw;
            }
        }

        public OnlineAuthorList ParseListAuthor(Stream data)
        {
            byte[] buffer = new byte[1024 * 32];
            int readed = 0;
            StringBuilder builder = new StringBuilder(32 * 1024);
            while ((readed = data.Read(buffer, 0, buffer.Length)) > 0)
            {
                builder.Append(UTF8Encoding.UTF8.GetString(buffer, 0, readed));
            }
            HtmlAgilityPack.HtmlDocument document = new HtmlDocument();

            var jsonResult = ((JObject)JsonConvert.DeserializeObject(builder.ToString()));
            OnlineAuthorList authors = new OnlineAuthorList();
            foreach (var x in jsonResult)
            {

                if (x.Key == "results")
                {
                    document.LoadHtml(x.Value.ToString());
                    foreach (var node in document.DocumentNode.ChildNodes.Where(xx => xx.Name == "li"))
                    {
                       var author = ParseShorAuthorDescriptionFromTag(node);
                        if (author.ComplitedBooks <= 0)
                            continue;
                        authors.Add(author);
                    }
                }
                if (x.Key == "pagination")
                {
                    document.LoadHtml(x.Value.ToString());
                    authors.MaxPageCount =
                        int.Parse(
                            document.DocumentNode.SelectSingleNode(@"a[last()]").Attributes
                                ["data-page_number"].Value);
                }
            }

            return authors;
        }

        private OnlineAuthor ParseShorAuthorDescriptionFromTag(HtmlNode node)
        {
            var link = node.SelectSingleNode(@"div/h3/a").Attributes["href"].Value;
            var id = link.Substring(link.LastIndexOf('/') + 1);
            var author = new OnlineAuthor()
            {
                FullName = node.SelectSingleNode(@"div/h3/a").InnerText,
                ComplitedBooks = int.Parse(node.SelectSingleNode(@"div/p/span[1]").InnerText.Replace("books", "").Trim()),
                InProgressBooks = int.Parse(node.SelectSingleNode(@"div/p/span[2]").InnerText.Replace("books", "").Trim()),
                AuthorId = int.Parse(id),
                Link = link
            };
            return author;
        }
    }
}
