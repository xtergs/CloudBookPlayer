using System;
using AudioBooksPlayer.WPF.Logic;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AudioBooksPlayer.WPF.Tests
{
    [TestClass]
    public class AudioBooksProcessorUnitTest
    {
        [TestMethod]
        public void ProcessAudoiBookFolderCorrect()
        {
            //A
            var processor = new AudioBooksProcessor();

            //A
            var bookInfo = processor.ProcessAudoiBookFolder(@"TestData\The War of the Worlds");

            //A
            Assert.IsTrue(true);
        }
    }
}
