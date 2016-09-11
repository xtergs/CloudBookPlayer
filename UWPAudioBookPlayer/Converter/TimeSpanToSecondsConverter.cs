using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.UI.Xaml.Data;

namespace UWPAudioBookPlayer.Converter
{
    public class TimeSpanToSecondsConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            TimeSpan timeSpan =(TimeSpan) value;
            return timeSpan.TotalSeconds;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            double secs = Math.Floor((double) value);
            return TimeSpan.FromSeconds(secs);
        }
    }

    public class SecondsToTimeSpanConverter : IValueConverter
    {
        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            TimeSpan timeSpan = (TimeSpan)value;
            return timeSpan.TotalSeconds;
        }

        public object Convert(object value, Type targetType, object parameter, string language)
        {
            double secs = (double)value;
            return TimeSpan.FromSeconds(Math.Floor(secs));
        }
    }
}
