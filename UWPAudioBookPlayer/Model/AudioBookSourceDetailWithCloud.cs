using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UWPAudioBookPlayer.Model
{
    public class AudioBookFileDetailWithClouds
    {
        public AudiBookFile File { get; set; }

        public bool IsLocalAvalible { get; set; }
        public bool IsDropBoxAvalible { get; set; }
    }
    public class AudioBookSourceDetailWithCloud
    {
       
    }
}
