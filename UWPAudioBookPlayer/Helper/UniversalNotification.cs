using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Windows.UI.Popups;

namespace UWPAudioBookPlayer.Helper
{
    class UniversalNotification : INotification
    {
        private List<UICommand> GetCommand(ActionButtons buttons)
        {
            var res = new List<UICommand>(2);
            if (buttons.HasFlag(ActionButtons.Ok))
                res.Add(new UICommand(ActionButtons.Ok.ToString()));
            if (buttons.HasFlag(ActionButtons.Cancel))
                res.Add(new UICommand(ActionButtons.Cancel.ToString()));
            return res;
        }
        public async Task ShowMessage(string title, string message)
        {
            var messageDialog = new MessageDialog(message, title);
            var res = await messageDialog.ShowAsync();
        }

        public async Task<ActionButtons> ShowMessage(string title, string message, ActionButtons buttons)
        {
            var messageDialog = new MessageDialog(message, title);
            var commands = GetCommand(buttons);
            foreach (var command in commands)
                messageDialog.Commands.Add(command);
            var res = await messageDialog.ShowAsync();
            return (ActionButtons)Enum.Parse(typeof(ActionButtons), res.Label);
        }
    }
}
