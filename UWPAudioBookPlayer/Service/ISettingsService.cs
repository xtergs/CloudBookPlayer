using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using Windows.UI;

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

    public struct ListDataTemplateStruct
    {
        public string Value { get; set; }
        public string HumanValue { get; set; }
        public bool IsWrapItems { get; set; }
    }

    public interface ISettingsService : INotifyPropertyChanged
    {
        bool AutomaticaliDeleteFilesFromDrBox { get; set; }
        bool AskBeforeDeletionBook { get; set; }
        int MaxLengthHistory { get; set; }
        string Changelog { get;  }
        string ChanglogShowedForVersion { get; set; }
        bool StartInCompactMode { get; set; }
        string ChangeLogOnce { get;}
        string SavedVersion { get; set; }
        bool ShowBooksList { get; set; }
        string ListDataTemplate { get; set; }
        bool IsWrapListItems { get; }
        ListDataTemplateStruct[] AvaliableListDataTemplages { get; }
        string StandartCover { get; set; }
        bool UseStandartCover { get; set; }

        #region Develop
        bool IsDevelopMode { get; set; }
        bool IsShowBackgroundImage { get; set; }
        bool IsShowPlayingBookImage { get; set; }
        bool IsBlurBackgroundImage { get; set; }
        int ValueToBlurBackgroundImage { get; set; }
        float BlurControlPanel { get; set; }
        bool BlurOnlyOverImage { get; set; }
        bool FillBackgroundEntireWindow { get; set; }
        Color ColorOfUserControlBlur { get; set; }
        double OpacityUserBlur { get; set; }

#endregion
    }
}
