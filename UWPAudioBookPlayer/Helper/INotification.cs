using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UWPAudioBookPlayer.Helper
{
    [Flags]
    enum ActionButtons
    {
        Ok, Cancel,
        Retry
    }
    interface INotification
    {
        Task ShowMessage(string title, string message);
        Task<ActionButtons> ShowMessage(string title, string message, ActionButtons buttons);
    }
}
