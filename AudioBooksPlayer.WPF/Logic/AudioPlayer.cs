using System;
using System.Collections.Generic;
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
        private AudioBooksInfo plyyingBook;

        private ISoundOut soundOut;

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
                return;

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
            if (soundOut.WaveSource.Position == soundOut.WaveSource.Length)
            {
                plyyingBook.CurrentFile++;
                plyyingBook.PositionInFile = 0;
                PlayASound();
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
    }
}
