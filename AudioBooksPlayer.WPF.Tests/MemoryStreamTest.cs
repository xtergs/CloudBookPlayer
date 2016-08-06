using System;
using System.Runtime.InteropServices;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using RemoteAudioBooksPlayer.WPF.ViewModel;

namespace AudioBooksPlayer.WPF.Tests
{
    [TestClass]
    public class MemoryStreamTest
    {
        [TestMethod]
        public void BasicUsageMemoryStream  ()
        {
            //A
            byte[] buffer = new byte[10];
            byte[] testBuf = new byte[1];
            MemorySecReadStream stream = new MemorySecReadStream(buffer);

            //A
            for (int i = 0; i < 10; i++)
            {
                testBuf[0] = (byte)i;
                stream.Write(testBuf, 0, testBuf.Length);
            }
            for (int i = 0; i < 5; i++)
            {
                Assert.AreEqual(stream.Read(testBuf, 0, testBuf.Length), 1);
                Assert.AreEqual(testBuf[0], i);
            }
            for (int i = 0; i < 5; i++)
            {
                testBuf[0] = (byte)(i + 15);
                stream.Write(testBuf, 0, testBuf.Length);
                Assert.AreEqual(stream.Read(testBuf, 0, testBuf.Length), testBuf.Length);
                Assert.AreEqual(testBuf[0], i + 5);
            }

            //A
        }

		[ExpectedException(typeof(ArgumentOutOfRangeException))]
		[TestMethod]
		public void OverflowExceptionMemoryStream()
		{
			//A
			byte[] buffer = new byte[10];
			byte[] testBuf = new byte[1];
			MemorySecReadStream stream = new MemorySecReadStream(buffer);

			//A
			for (int i = 0; i < 7; i++)
			{
				testBuf[0] = (byte)i;
				stream.Write(testBuf, 0, testBuf.Length);
			}

			stream.Write(new byte[] {7, 8}, 0, 2);
			stream.Write(new byte[] { 9,108 }, 0, 2);

			//A
			Assert.AreEqual(108, buffer[0]);
		}

		[TestMethod]
		public void OverflowMemoryStream()
		{
			//A
			byte[] buffer = new byte[10];
			byte[] testBuf = new byte[1];
			MemorySecReadStream stream = new MemorySecReadStream(buffer);

			//A
			for (int i = 0; i < 7; i++)
			{
				testBuf[0] = (byte)i;
				stream.Write(testBuf, 0, testBuf.Length);
			}

			stream.Write(new byte[] { 7, 8 }, 0, 2);
			stream.Read(new byte[10], 0, 3);
			stream.Write(new byte[] { 9, 108 }, 0, 2);

			//A
			Assert.AreEqual(108, buffer[0]);
		}
	}
}
