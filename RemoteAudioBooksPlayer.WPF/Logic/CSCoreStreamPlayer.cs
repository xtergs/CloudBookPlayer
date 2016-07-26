using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using CSCore;
using CSCore.Codecs;
using CSCore.Codecs.MP3;
using CSCore.SoundOut;

namespace RemoteAudioBooksPlayer.WPF.Logic
{
    class CSCoreStreamPlayer
    {
        public void PlayASound(Stream stream)
        {
            //Contains the sound to play
            //using ()
            {
                //SoundOut implementation which plays the sound
                //using ()
                //{
                //Tell the SoundOut which sound it has to play
                IWaveSource soundSource = GetSoundSource(stream);
                ISoundOut soundOut = GetSoundOut();
                    soundOut.Initialize(soundSource);
                    //Play the sound
                    soundOut.Play();

                    //Thread.Sleep(2000);

                    ////Stop the playback
                    //soundOut.Stop();
                //}
            }
        }

        private ISoundOut GetSoundOut()
        {
            if (WasapiOut.IsSupportedOnCurrentPlatform)
                return new WasapiOut();
            else
                return new DirectSoundOut();
        }

        private IWaveSource GetSoundSource(Stream stream)
        {
            // Instead of using the CodecFactory as helper, you specify the decoder directly:
            return new DmoMp3Decoder(stream);

        }
    }
}
