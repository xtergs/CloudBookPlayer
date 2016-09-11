using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace UWPAudioBookPlayer.Service
{
    public interface IApplicationSettingsHelper
    {
        // DateTime DateTimeSettings { get; set; }
        void SimpleSet<T>(T value, [CallerMemberName] string key = null);
        T SimpleGet<T>(T defValue = default(T), [CallerMemberName] string key = null);
       DateTime SimbleGet(DateTime value = default(DateTime), [CallerMemberName] string key = null);
        void SimpleSet(DateTime value, [CallerMemberName] string key = null);
    }

    public interface ISettingsService
    {
        bool AutomaticaliDeleteFilesFromDrBox { get; set; }
        bool AskBeforeDeletionBook { get; set; }
    }
}
