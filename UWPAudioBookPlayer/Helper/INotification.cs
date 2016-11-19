using System;
using System.Threading.Tasks;

namespace UWPAudioBookPlayer.Helper
{
    [Flags]
    public enum ActionButtons
    {
        Ok, Cancel,
        Retry, None, Continue, Yes, No
    }

    public interface INotification
    {
        Task ShowMessage(string title, string message);
        Task<ActionButtons> ShowMessage(string title, string message, params ActionButtons[] buttons);
        Task<ActionButtons> ShowMessageWithTimer(string title, string message, ActionButtons buttons, int duration);
        Task ShowMessageAsync(string changelog);
    }
}
