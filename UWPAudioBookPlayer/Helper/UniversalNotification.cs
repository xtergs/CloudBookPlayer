using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Windows.UI.Popups;

namespace UWPAudioBookPlayer.Helper
{
    class UniversalNotification : INotification
    {
        private static List<UICommand> GetCommand(ActionButtons buttons)
        {
            var res = new List<UICommand>(2);
            if (buttons.HasFlag(ActionButtons.Ok))
                res.Add(new UICommand(ActionButtons.Ok.ToString()));
            if (buttons.HasFlag(ActionButtons.Cancel))
                res.Add(new UICommand(ActionButtons.Cancel.ToString()));
            return res;
        }

        private static List<UICommand> GetCommand(ActionButtons[] buttons)
        {
            var res = buttons.Select(c => new UICommand(c.ToString())).ToList();
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

        public async Task<ActionButtons> ShowMessage(string title, string message, params ActionButtons[] buttons)
        {
            var messageDialog = new MessageDialog(message, title);
            var commands = GetCommand(buttons);
            foreach (var command in commands)
                messageDialog.Commands.Add(command);
            var res = await messageDialog.ShowAsync();
            return (ActionButtons)Enum.Parse(typeof(ActionButtons), res.Label);
        }

        public async Task<ActionButtons> ShowMessageWithTimer(string title, string message, ActionButtons buttons, int duration)
        {
            var messageDialog = new MessageDialog(message, title);
            var commands = GetCommand(buttons);
            foreach (var command in commands)
                messageDialog.Commands.Add(command);
            List<Task< ActionButtons >> tasks = new List<Task<ActionButtons>>();
                var res = messageDialog.ShowAsync();

            tasks.Add(Task.Run(async () =>
            {
                var resu = await res;
                var xx = (ActionButtons)Enum.Parse(typeof(ActionButtons), resu.Label);
                return (xx);
            }));
            tasks.Add( Task.Run(async () =>
            {
                await Task.Delay(duration);
                return ActionButtons.None;
            }));
            var result =  (await Task.WhenAny(tasks)).Result;
            res.Close();
            return result;


        }

        public async Task ShowMessageAsync(string changelog)
        {
            var messageDialog = new MessageDialog(changelog);
            await messageDialog.ShowAsync();
        }
    }
}
