using System;
using Windows.UI.Xaml.Data;
using UWPAudioBookPlayer.DAL.Model;
using UWPAudioBookPlayer.ModelView;

namespace UWPAudioBookPlayer.Converter
{
    public class AudioBookSourceToImageConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            var cloud = value as AudioBookSourceCloud;
            if (cloud?.Type == CloudType.DropBox)
                return "..\\Image\\DropBoxLogo.png";
            if (cloud?.Type == CloudType.OneDrive)
                return "..\\Image\\OneDriveLogo.png";
            if (cloud?.Type == CloudType.Online)
                return "..\\Image\\online.png";
            return "..\\Image\\HDD.png";
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
}
