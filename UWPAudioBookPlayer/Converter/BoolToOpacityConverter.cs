using System;
using Windows.UI.Xaml.Data;

namespace UWPAudioBookPlayer.Converter
{
    public class BoolToOpacityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            bool val = (bool) value;
            if (val)
                return 1;
            return 0.5;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
}
