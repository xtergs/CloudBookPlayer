using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI;
using Windows.UI.Composition;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Hosting;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;
using Microsoft.Graphics.Canvas.Effects;
using UWPAudioBookPlayer.Model;
using UWPAudioBookPlayer.ModelView;

// The Blank Page item template is documented at http://go.microsoft.com/fwlink/?LinkId=234238

namespace UWPAudioBookPlayer.View
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class BookDetailInfo : Page
    {
        private AudioBookSourceDetailViewModel viewModel;
        public BookDetailInfo()
        {
            this.InitializeComponent();
            Windows.UI.Core.SystemNavigationManager.GetForCurrentView().AppViewBackButtonVisibility = AppViewBackButtonVisibility.Visible;

            Windows.UI.Core.SystemNavigationManager.GetForCurrentView().BackRequested += (s, a) =>
            {

                if (Frame.CanGoBack)
                {
                    Frame.GoBack();
                    a.Handled = true;
                }
            };

            this.Loaded += OnLoaded;
			this.Loading += OnLoading;

        }

	    private async void OnLoading(FrameworkElement sender, object args)
	    {
		    Loading -= OnLoading;
		    InitComposition();
		    await Task.WhenAll(
			    viewModel.LoadCloudData(),
			    ViewHelper.LoadImage(backgroudn, viewModel.Book as AudioBookSourceWithClouds, viewModel.Controllers1));
			    await ViewHelper.LoadImage(smallCover, viewModel.Book as AudioBookSourceWithClouds, viewModel.Controllers1);

	    }

		private void BackFrame_OnSizeChanged(object sender, SizeChangedEventArgs e)
		{
			if (visual == null)
				return;
			visual.Size = new Vector2((float)backFrame.ActualWidth, (float)backFrame.ActualHeight);
		}

		private SpriteVisual visual;
		private void InitComposition()
		{
			var compositor = ElementCompositionPreview.GetElementVisual(backFrame).Compositor;

			visual = compositor.CreateSpriteVisual();
			//BottomVisual = compositor.CreateSpriteVisual();



			var blurEffect = new GaussianBlurEffect()
			{
				Name = "Blur",
				BlurAmount = viewModel.Settings.BlurControlPanel, // You can place your blur amount here.
				BorderMode = EffectBorderMode.Hard,
				Optimization = EffectOptimization.Balanced,
				Source = new CompositionEffectSourceParameter("source"),
			};

			BlendEffect blendEffect = new BlendEffect
			{
				Background = blurEffect,
				Foreground = new ColorSourceEffect { Name = "Color", Color = Color.FromArgb((byte)viewModel.Settings.OpacityUserBlur, 255, 255, 255) },
				Mode = BlendEffectMode.SoftLight
			};

			var effectFactory = compositor.CreateEffectFactory(blendEffect, new[] { "Blur.BlurAmount" });

			var effectBrush = effectFactory.CreateBrush();
			effectBrush.SetSourceParameter("source", compositor.CreateBackdropBrush());
			visual.Brush = effectBrush;

			//BottomVisual.Brush = effectBrush;

			ElementCompositionPreview.SetElementChildVisual(backFrame, visual);
			//ElementCompositionPreview.SetElementChildVisual(BottomRectagle, BottomVisual);
		}

		private async void OnLoaded(object sender, RoutedEventArgs routedEventArgs)
        {
            this.Loaded -= OnLoaded;
            await viewModel.GetCover().ConfigureAwait(false);
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            if (e.Parameter is AudioBookSourceDetailViewModel)
            {
                var param = e.Parameter as AudioBookSourceDetailViewModel;
                this.viewModel = param;
                this.DataContext = param;
            }
            base.OnNavigatedTo(e);
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            base.OnNavigatedFrom(e);
        }
    }
}
