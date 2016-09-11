using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using AudioBooksPlayer.WPF.Streaming;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AudioBooksPlayer.WPF.Tests
{
    [TestClass]
    public class StreamingTests
    {
        private double _packageLoss;

        [TestMethod]
        public async Task BaseUdpStreaming()
        {
            StreamingUDP udp = new StreamingUDP();
            IPEndPoint endpoint = new IPEndPoint(IPAddress.Parse("192.168.0.100"), 23230);
            List<Task> tasks = new List<Task>(2);


            var stream = File.Create(
                @"TestData\The War of the Worlds\01-01.mp3");
            tasks.Add(udp.StartListeneningSteam(stream, endpoint, new Progress<ReceivmentProgress>()).ContinueWith((state) =>
            {
                stream.Flush();
                stream.Close();
            }));
            Thread.Sleep(1000);
            var rstream = File.Open(
                @"TestData\The War of the Worlds\H.G. Wells - The War Of The Worlds - 01-01.mp3", FileMode.Open);
                tasks.Add(udp.StartSendStream( Guid.Empty,rstream, "", new IPEndPoint(IPAddress.Parse("255.255.255.255"), 23230), new Progress<StreamProgress>(Handler)).ContinueWith((state)=>rstream.Close()));

            try
            {
                Task.WaitAll(tasks.ToArray());
            }
            catch
            {
                throw;
            }
            Assert.IsTrue(true);
        }

        private void Handler(StreamProgress streamProgress)
        {
            //throw new NotImplementedException();
        }

        
    }
}
