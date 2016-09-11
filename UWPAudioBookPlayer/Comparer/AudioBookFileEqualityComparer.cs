using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UWPAudioBookPlayer.Model;

namespace UWPAudioBookPlayer.Comparer
{
    class AudioBookFileEqualityComparer : IEqualityComparer<AudiBookFile>
    {
        public bool Equals(AudiBookFile x, AudiBookFile y)
        {
            return x.Name == y.Name;
        }

        public int GetHashCode(AudiBookFile obj)
        {
            return 0;
        }
    }
}
