using System;
using System.IO;
using System.Threading.Tasks;
using Windows.Web.Http;
using Microsoft.VisualStudio.TestPlatform.UnitTestFramework;
using UWPAudioBookPlayer.Scrapers;

namespace UwpUnitTests
{
    [TestClass]
    public class LibriVoxScraperUnitTest
    {
        [TestMethod]
        public async Task ParseBookPageTest()
        {
            //A
            LIbriVoxScraper scraper = new LIbriVoxScraper();
            var asyncOperationWithProgress = await new HttpClient().GetInputStreamAsync(new Uri(@"https://librivox.org/30000-bequest-and-other-stories-by-mark-twain/"));

            //A
            var book = scraper.ParseBookPage(asyncOperationWithProgress.AsStreamForRead());

            //A
            Assert.IsFalse(string.IsNullOrWhiteSpace(book.Author));
            Assert.IsFalse(string.IsNullOrWhiteSpace(book.BookName));
            Assert.IsFalse(string.IsNullOrWhiteSpace(book.CoverLink ));
            Assert.IsFalse(string.IsNullOrWhiteSpace(book.Description ));
            Assert.IsFalse(string.IsNullOrWhiteSpace(book.Description ));
            Assert.IsFalse(string.IsNullOrWhiteSpace(book.Genries ));
            Assert.IsFalse(string.IsNullOrWhiteSpace(book.Language));
            Assert.IsFalse(string.IsNullOrWhiteSpace(book.ReadBy ));
            Assert.IsTrue(book.Duration > TimeSpan.Zero);
            Assert.IsTrue(book.Files.Count > 0);
        }
    }
}
