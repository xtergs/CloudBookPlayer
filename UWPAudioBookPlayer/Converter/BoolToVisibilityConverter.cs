using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Media;

namespace UWPAudioBookPlayer.Converter
{
    public class BoolToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            var val = (bool) value;
            return val ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }

    public class BoolToNotVisiblityConverter : IValueConverter
    {
        public bool[] AdditionParameter { get; set; } = new bool[0];
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            bool par = true;
            if (parameter != null)
                par =  (bool) parameter;
            var val = (bool) value && par;
            return val ? Visibility.Collapsed : Visibility.Visible;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }

    public class BoolToUniformOrFillConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            var val = (bool)value;
            
            return val ? Stretch.Uniform : Stretch.Fill;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
}
