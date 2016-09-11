using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using UWPAudioBookPlayer.DAL.Model;

namespace UWPAudioBookPlayer.TemplateSelector
{
    public class AudioBookSourceTemplateSelector : DataTemplateSelector
    {
        public DataTemplate AduioBookWithCloud { get; set; }
        public DataTemplate AudioBookCloud { get; set; }
        protected override DataTemplate SelectTemplateCore(object item)
        {

            if (item is AudioBookSourceCloud)
                return AudioBookCloud;
            return AduioBookWithCloud;
        }
    }
}
