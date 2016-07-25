using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AudioBooksPlayer.WPF.Model;
using CSCore;
using CSCore.Codecs;
using CSCore.SoundOut;

namespace AudioBooksPlayer.WPF.Logic
{
    public class AudioPlayer
    {
        public event EventHandler PlayingNextFile;
        public event EventHandler PlaybackStoped; 
        private AudioBooksInfo plyyingBook;

        private ISoundOut soundOut;

        public TimeSpan TotalTime
        {
            get
            {
                if (soundOut == null)
                    return TimeSpan.Zero;
                return soundOut.WaveSource.GetLength();
            }
        }

        public TimeSpan CurrentTime
        {
            get
            {
                if (soundOut == null)
                    return TimeSpan.Zero;
                return soundOut.WaveSource.GetPosition();
            }
        }

        public void PlayAudioBook(AudioBooksInfo book)
        {
            StopPlay();
            plyyingBook = book;
            PlayASound();
        }

        private void PlayASound()
        {
            //Contains the sound to play
            IWaveSource soundSource = GetSoundSource();
            if (soundSource == null)
            {
                OnPlaybackStoped();
                StopPlay();
                return;
            }

            //SoundOut implementation which plays the sound
            if (soundOut != null && soundOut.PlaybackState == PlaybackState.Playing)
            {
                soundOut.Stop();
            }
            soundOut?.Dispose();
            soundOut = GetSoundOut();
            soundOut.Stopped += SoundOutOnStopped;
            //Tell the SoundOut which sound it has to play
            soundOut.Initialize(soundSource);
            soundOut.WaveSource.Position = plyyingBook.PositionInFile;
            //Play the sound
            soundOut.Play();

        }

        public void StopPlay()
        {
            if (soundOut != null)
            {
                soundOut.Stop();
                plyyingBook.PositionInFile = soundOut.WaveSource.Position;
                soundOut.Dispose();
                soundOut = null;
            }
        }

        private void SoundOutOnStopped(object sender, PlaybackStoppedEventArgs playbackStoppedEventArgs)
        {
            if (soundOut == null)
                return;
            if (soundOut.WaveSource.Position / (double)soundOut.WaveSource.Length > 0.9)
            {
                PlayNext();
                return;
            }
            plyyingBook.PositionInFile = soundOut.WaveSource.Position;
        }

        private ISoundOut GetSoundOut()
        {
            if (WasapiOut.IsSupportedOnCurrentPlatform)
                return new WasapiOut();
            else
                return new DirectSoundOut();
        }

        private IWaveSource GetSoundSource()
        {
            if (plyyingBook.CurrentFile < plyyingBook.Files.Length)
                return CodecFactory.Instance.GetCodec(plyyingBook.Files[plyyingBook.CurrentFile].FilePath);
            return null;
        }

        public void SetTimePosition(TimeSpan time)
        {
            soundOut.WaveSource.SetPosition(time);
            plyyingBook.PositionInFile = soundOut.WaveSource.Position;
        }

        public void PlayNext()
        {
            plyyingBook.CurrentFile++;
            plyyingBook.PositionInFile = 0;
            PlayASound();
            OnPlayingNextFile();
        }

        public void PlayPrev()
        {
            plyyingBook.CurrentFile--;
            plyyingBook.PositionInFile = 0;
            PlayASound();
            OnPlayingNextFile();
        }

        protected virtual void OnPlayingNextFile()
        {
            PlayingNextFile?.Invoke(this, EventArgs.Empty);
        }

        protected virtual void OnPlaybackStoped()
        {
            PlaybackStoped?.Invoke(this, EventArgs.Empty);
        }
    }
}
