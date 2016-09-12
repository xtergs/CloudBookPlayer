using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.UI.Xaml.Data;
using UWPAudioBookPlayer.ModelView;

namespace UWPAudioBookPlayer.Converter
{
    class CloudTypeToImageConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (!(value is CloudType))
                return value;
            var val = (CloudType)value;
            if (val == CloudType.DropBox)
                return "../Image/DropBoxLogo.png";
            if (val == CloudType.OneDrive)
                return "../Image/OneDriveLogo.png";
            if (val == CloudType.Local)
                return "../Image/HDD.png";
            return value;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
}
