using System;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Dropbox.Api;


// The Content Dialog item template is documented at http://go.microsoft.com/fwlink/?LinkId=234238

namespace UWPAudioBookPlayer.Controllers
{
    public enum ResultEnum
    {
        Authenticated,
        Cancel,
        Error
    }
    public sealed partial class DropBoxAuthDialog : ContentDialog
    {
        private string AppResponseUrl;
        private readonly string _appCode;
        private readonly string _appSecret;

        public ResultEnum Result { get; private set; } = ResultEnum.Cancel;

        public DropBoxAuthDialog(Uri url, string appResponseUrl, string appCode, string appSecret)
        {
            this.AppResponseUrl = appResponseUrl;
            _appCode = appCode;
            _appSecret = appSecret;
            this.InitializeComponent();
            webView.Navigate(url);
            this.IsPrimaryButtonEnabled = false;
            this.MinHeight *= 4;
            this.MinWidth *= 1.5;
        }

        private void OnSizeChanged(object sender, SizeChangedEventArgs sizeChangedEventArgs)
        {
            
        }


        private void ContentDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
        }

        private void ContentDialog_SecondaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            this.Hide();
        }

        public string Token { get; private set; }

        private async void WebView_OnNavigationCompleted(WebView sender, WebViewNavigationCompletedEventArgs args)
        {
            progress.IsActive = false;
            if (args.Uri.OriginalString.Contains(@"error=access_denied"))
            {
                Result = ResultEnum.Error;
                this.Hide();
            }
            if (args.Uri.OriginalString.Contains(AppResponseUrl))
            {
                var resp = await DropboxOAuth2Helper.ProcessCodeFlowAsync(args.Uri, _appCode, _appSecret, AppResponseUrl);
                Token = resp.AccessToken;
                Result = ResultEnum.Authenticated;
                this.Hide();
            }
        }

        private void WebView_OnNavigationStarting(WebView sender, WebViewNavigationStartingEventArgs args)
        {
            progress.IsActive = true;
        }

        private void WebView_OnSizeChanged(object sender, SizeChangedEventArgs e)
        {
            //this.Width = webView.DesiredSize.Width;
        }
    }
}
