﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace AudioBooksPlayer.WPF.ExternalLogic
{
    public interface IFileSelectHelper
    {
        string SelectFolder();
    }

    public class WPFFileSelectHelper : IFileSelectHelper
    {
        public string SelectFolder()
        {
            var folderDialog = new FolderBrowserDialog();
#if DEBUG
            folderDialog.SelectedPath = Environment.CurrentDirectory;
#endif
            if (folderDialog.ShowDialog() != DialogResult.OK)
                return null;
            return folderDialog.SelectedPath;
        }
    }
}
