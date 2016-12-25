using System;
using Windows.UI.Xaml.Controls;

// The Blank Page item template is documented at http://go.microsoft.com/fwlink/?LinkId=234238

namespace UWPAudioBookPlayer.Controllers
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class DropBoxAuthView : Page
    {
        public DropBoxAuthView()
        {
            this.InitializeComponent();
        }

        public void NavigateTo(string url)
        {
            webView.Source = new Uri(url);
        }
    }
}
