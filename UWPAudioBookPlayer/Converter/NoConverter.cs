using System;
using Windows.UI.Xaml.Data;

namespace UWPAudioBookPlayer.Converter
{
    public class NoConverter : IValueConverter 
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            var val = (bool) value;
            return !val;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
}
