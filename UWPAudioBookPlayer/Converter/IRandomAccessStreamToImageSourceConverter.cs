using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Storage.Streams;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;

namespace UWPAudioBookPlayer.Converter
{
    class IRandomAccessStreamToImageSourceConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            var stream = value as IRandomAccessStream;
            if (stream == null)
                return value;
            var bitMap = new BitmapImage();
            try
            {
                bitMap.SetSourceAsync(stream);
            }
            catch
            {
                
            }
            return bitMap;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
}
