using System;
using Windows.UI.Xaml.Data;
using UWPAudioBookPlayer.DAL.Model;

namespace UWPAudioBookPlayer.Converter
{
    public class AudioBookSourceToImageConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value is AudioBookSourceCloud)
                return "..\\Image\\DropBoxLogo.png";
            return "..\\Image\\HDD.png";
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
}
