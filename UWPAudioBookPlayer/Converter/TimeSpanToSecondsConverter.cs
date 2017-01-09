using System;
using System.Globalization;
using Windows.UI.Xaml.Data;

namespace UWPAudioBookPlayer.Converter
{
	public class TimeSpanToShortenedMinutesConverter: IValueConverter {
		public object Convert(object value, Type targetType, object parameter, string language)
		{
			TimeSpan val = (TimeSpan) value;
			var xxx = CultureInfo.CurrentUICulture.DateTimeFormat;
			string hours = val.Hours > 0 ? val.Hours.ToString("D2") + ":" : "";
			return hours + $"{val.Minutes.ToString("D2")}:{val.Seconds.ToString("D2")}";
		}

		public object ConvertBack(object value, Type targetType, object parameter, string language)
		{
			throw new NotImplementedException();
		}
	}
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
