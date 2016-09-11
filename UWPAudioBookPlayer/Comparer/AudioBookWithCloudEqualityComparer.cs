using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UWPAudioBookPlayer.Model;

namespace UWPAudioBookPlayer.Comparer
{
    class AudioBookWithCloudEqualityComparer : IEqualityComparer<AudioBookSourceWithClouds>
    {
        public bool Equals(AudioBookSourceWithClouds x, AudioBookSourceWithClouds y)
        {
            return x.Name == y.Name;
        }

        public int GetHashCode(AudioBookSourceWithClouds obj)
        {
            return 0;
        }
    }
}
