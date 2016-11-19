using System;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Media.Imaging;
using UWPAudioBookPlayer.Model;

namespace UWPAudioBookPlayer.Converter
{
    class AudioBookSourceToCoverConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            var val = value as AudioBookSourceWithClouds;
            if (val == null)
                return value;
            return value;
            BitmapImage image = new BitmapImage();
            //image.set
            //(await val.GetFileStream(val.Images?.FirstOrDefault() ?? val.Cover)).Item2;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new System.NotImplementedException();
        }
    }
}
