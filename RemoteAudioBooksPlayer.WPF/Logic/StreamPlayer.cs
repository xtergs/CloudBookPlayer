using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NAudio.FileFormats.Mp3;
using NAudio.Wave;
using Timer = System.Timers.Timer;

namespace RemoteAudioBooksPlayer.WPF.Logic
{
    public class StreamPlayer
    {
        public StreamPlayer()
        {
            timer1 = new Timer();
            timer1.Interval = 250;
            timer1.Elapsed += timer1_Tick;
        }
        enum StreamingPlaybackState
        {
            Stopped,
            Playing,
            Buffering,
            Paused
        }

        private BufferedWaveProvider bufferedWaveProvider;
        private IWavePlayer waveOut;
        private volatile StreamingPlaybackState playbackState;
        private volatile bool fullyDownloaded;
        private HttpWebRequest webRequest;
        private VolumeWaveProvider16 volumeProvider;
        private Timer timer1;

        private WaveStream streamm;

        public void PlayStream(string url)
        {
            if (playbackState == StreamingPlaybackState.Stopped)
            {
                playbackState = StreamingPlaybackState.Buffering;
                bufferedWaveProvider = null;
                ThreadPool.QueueUserWorkItem(StreamMp3, url);
                timer1.Enabled = true;
            }
            else if (playbackState == StreamingPlaybackState.Paused)
            {
                playbackState = StreamingPlaybackState.Buffering;
            }
        }

        public void PlayStream(Stream stream)
        {
            if (playbackState == StreamingPlaybackState.Stopped)
            {
                playbackState = StreamingPlaybackState.Buffering;
                bufferedWaveProvider = null;
                ThreadPool.QueueUserWorkItem(StreamMp3, stream);
                timer1.Enabled = true;
            }
            else if (playbackState == StreamingPlaybackState.Paused)
            {
                playbackState = StreamingPlaybackState.Buffering;
            }
        }

        private void StreamMp3(Stream stream)
        {
            fullyDownloaded = false;
            var buffer = new byte[16384 * 4]; // needs to be big enough to hold a decompressed frame

            IMp3FrameDecompressor decompressor = null;
            try
            {
                var readFullyStream = new ReadFullyStream(stream);
                do
                {
                    if (IsBufferNearlyFull)
                    {
                        Debug.WriteLine("Buffer getting full, taking a break");
                        Thread.Sleep(500);
                        continue;
                    }
                    else
                    {
                        Mp3Frame frame;
                        try
                        {
                            readFullyStream.Read(new byte[this.moving], 0, this.moving);
                            this.moving = 0;
                            frame = Mp3Frame.LoadFromStream(readFullyStream);
                        }
                        catch (EndOfStreamException e)
                        {
                            //fullyDownloaded = true;
                            // reached the end of the MP3 file / stream
                            //break;
                            frame = null;
                        }
                        catch (WebException)
                        {
                            // probably we have aborted download from the GUI thread
                            break;
                        }
                        if (frame == null)
                            continue;
                        if (decompressor == null)
                        {
                            // don't think these details matter too much - just help ACM select the right codec
                            // however, the buffered provider doesn't know what sample rate it is working at
                            // until we have a frame
                            decompressor = CreateFrameDecompressor(frame);
                            bufferedWaveProvider = new BufferedWaveProvider(decompressor.OutputFormat);
                            //waveOut.Init(new RawSourceWaveStream(new MemoryStream(), new Mp3WaveFormat(1, 2, 2, 3)));
                            bufferedWaveProvider.BufferDuration = TimeSpan.FromSeconds(20);
                            // allow us to get well ahead of ourselves
                            //this.bufferedWaveProvider.BufferedDuration = 250;
                        }
                        int decompressed = decompressor.DecompressFrame(frame, buffer, 0);
                        //Debug.WriteLine(String.Format("Decompressed a frame {0}", decompressed));
                        bufferedWaveProvider.AddSamples(buffer, 0, decompressed);
                    }

                } while (playbackState != StreamingPlaybackState.Stopped);
                Debug.WriteLine("Exiting");
                // was doing this in a finally block, but for some reason
                // we are hanging on response stream .Dispose so never get there
                decompressor.Dispose();
            }
            finally
            {
                if (decompressor != null)
                {
                    decompressor.Dispose();
                }
            }
        }

        private void StreamMp3Stream(object state)
        {
            if (state is Stream)
            {
                var stream = (Stream) state;
                StreamMp3(stream);
            }
        }
        private void StreamMp3(object state)
        {
            if (state is Stream)
            {
                StreamMp3((Stream)state);
                return;
            }
            var url = (string)state;
            webRequest = (HttpWebRequest)WebRequest.Create(url);
            HttpWebResponse resp;
            try
            {
                resp = (HttpWebResponse)webRequest.GetResponse();
            }
            catch (WebException e)
            {
                if (e.Status != WebExceptionStatus.RequestCanceled)
                {
                    //ShowError(e.Message);
                }
                return;
            }
           
            try
            {
                using (var responseStream = resp.GetResponseStream())
                {
                    StreamMp3(responseStream);
                }
            }
            finally
            {
               
            }
        }

        private static IMp3FrameDecompressor CreateFrameDecompressor(Mp3Frame frame)
        {
            WaveFormat waveFormat = new Mp3WaveFormat(frame.SampleRate, frame.ChannelMode == ChannelMode.Mono ? 1 : 2,
                frame.FrameLength, frame.BitRate);
            var xx =  new DmoMp3FrameDecompressor(waveFormat);
            var x2 =  new AcmMp3FrameDecompressor(waveFormat);
            return x2;
        }

        private bool IsBufferNearlyFull
        {
            get
            {
                return bufferedWaveProvider != null &&
                       bufferedWaveProvider.BufferLength - bufferedWaveProvider.BufferedBytes
                       < bufferedWaveProvider.WaveFormat.AverageBytesPerSecond / 4;
            }
        }

        private void timer1_Tick(object sender, EventArgs e)
        {
            if (playbackState != StreamingPlaybackState.Stopped)
            {
                if (waveOut == null && bufferedWaveProvider != null)
                {
                    Debug.WriteLine("Creating WaveOut Device");
                    waveOut = CreateWaveOut();
                    //waveOut.PlaybackStopped += OnPlaybackStopped;
                    volumeProvider = new VolumeWaveProvider16(bufferedWaveProvider);
                    //volumeProvider.Volume = volumeSlider1.Volume;
                    waveOut.Init(volumeProvider);
                    //progressBarBuffer.Maximum = (int)bufferedWaveProvider.BufferDuration.TotalMilliseconds;
                }
                else if (bufferedWaveProvider != null)
                {
                    var bufferedSeconds = bufferedWaveProvider.BufferedDuration.TotalSeconds;
                    //ShowBufferState(bufferedSeconds);
                    // make it stutter less if we buffer up a decent amount before playing
                    if (bufferedSeconds < 0.5 && playbackState == StreamingPlaybackState.Playing && !fullyDownloaded)
                    {
                        Pause();
                    }
                    else if (bufferedSeconds > 4 && playbackState == StreamingPlaybackState.Buffering)
                    {
                        Play();
                    }
                    else if (fullyDownloaded && bufferedSeconds == 0)
                    {
                        Debug.WriteLine("Reached end of stream");
                        StopPlayback();
                    }
                }

            }
        }

        private void StopPlayback()
        {
            if (playbackState != StreamingPlaybackState.Stopped)
            {
                if (!fullyDownloaded)
                {
                    webRequest.Abort();
                }

                playbackState = StreamingPlaybackState.Stopped;
                if (waveOut != null)
                {
                    waveOut.Stop();
                    waveOut.Dispose();
                    waveOut = null;
                }
                timer1.Enabled = false;
                // n.b. streaming thread may not yet have exited
                Thread.Sleep(500);
                //ShowBufferState(0);
            }
        }

        static object o = new object();
        private void Play()
        {
            lock (o)
            {
                if (waveOut.PlaybackState == PlaybackState.Playing)
                    return;
                waveOut.Play();
                Debug.WriteLine(String.Format("Started playing, waveOut.PlaybackState={0}", waveOut.PlaybackState));
                playbackState = StreamingPlaybackState.Playing;
            }
        }

        private void Pause()
        {
            lock (o)
            {
                if (waveOut.PlaybackState != PlaybackState.Playing)
                    return;
                playbackState = StreamingPlaybackState.Buffering;
                waveOut.Pause();
                Debug.WriteLine(String.Format("Paused to buffer, waveOut.PlaybackState={0}", waveOut.PlaybackState));
            }
        }

        private IWavePlayer CreateWaveOut()
        {
            var xx =  new WaveOut();
            //var yy = );
            //xx.Init(yy);
            return xx;
        }

        private int moving = 0;
        public void Forward(int ms)
        {
            this.moving = bufferedWaveProvider.WaveFormat.AverageBytesPerSecond*ms;
            bufferedWaveProvider.ClearBuffer();
        }
    }
}
