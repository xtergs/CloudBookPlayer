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
using UWPAudioBookPlayer.ModelView;
using Autofac;
using Microsoft.Graphics.Canvas.Effects;
using UWPAudioBookPlayer.Service;

// The Blank Page item template is documented at http://go.microsoft.com/fwlink/?LinkId=234238

namespace UWPAudioBookPlayer.View
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class LibrivoxBooksOnlineView : Page
    {
        private LibrivoxOnlineBooksViewModel viewModel;
        private ISettingsService settings;
        private SpriteVisual visual;
        //private SpriteVisual BottomVisual;
        private GaussianBlurEffect blurEffect;

        public LibrivoxBooksOnlineView()
        {
            this.InitializeComponent();
            viewModel = Global.container.Resolve<LibrivoxOnlineBooksViewModel>();
            settings = Global.container.Resolve<ISettingsService>();
            var mainModel = Global.MainModelView;
            viewModel.AddSourceToLibrary = mainModel.AddSourceToLibraryCommand;
            viewModel.AddAndPlayBook = mainModel.StartPlaySourceCommand;
            this.Loading += OnLoading;
            DataContext = viewModel;

            InitComposition();
        }

        private async void OnLoading(FrameworkElement sender, object args)
        {
            VisualStateManager.GoToState(this, ShowBookInfoStateGroup.Name, false);
            await viewModel.LoadData();
        }


        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            SystemNavigationManager.GetForCurrentView().AppViewBackButtonVisibility = AppViewBackButtonVisibility.Visible;
            SystemNavigationManager.GetForCurrentView().BackRequested += OnBackRequested;
            base.OnNavigatedTo(e);
        }

        private void OnBackRequested(object sender, BackRequestedEventArgs backRequestedEventArgs)
        {
            if (viewModel.BackIfCan())
            {
                backRequestedEventArgs.Handled = true;
                return;
            }
            Windows.UI.Core.SystemNavigationManager.GetForCurrentView().BackRequested -= OnBackRequested;
            if (Frame.CanGoBack)
            {
                Frame.GoBack();
                backRequestedEventArgs.Handled = true;
            }
        }

        private void InitComposition()
        {
            var compositor = ElementCompositionPreview.GetElementVisual(backFrame).Compositor;

            visual = compositor.CreateSpriteVisual();
            //BottomVisual = compositor.CreateSpriteVisual();



            blurEffect = new GaussianBlurEffect()
            {
                Name = "Blur",
                BlurAmount = settings.BlurControlPanel, // You can place your blur amount here.
                BorderMode = EffectBorderMode.Hard,
                Optimization = EffectOptimization.Balanced,
                Source = new CompositionEffectSourceParameter("source"),
            };

            BlendEffect blendEffect = new BlendEffect
            {
                Background = blurEffect,
                Foreground = new ColorSourceEffect { Name = "Color", Color = Color.FromArgb((byte)settings.OpacityUserBlur, 255, 255, 255) },
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

        private void BackFrame_OnSizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (visual == null)
                return;
            visual.Size = new Vector2((float)backFrame.ActualWidth, (float)backFrame.ActualHeight);
        }

        private void Pivot_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var pivot = sender as Pivot;
            var item = pivot.SelectedItem as PivotItem;
            if (item == TitleItem)
            {
                viewModel.ShowBooksByTitleCommand.Execute(null);
            }else if (item == AuthorItem)
                viewModel.ShowAuthorsCommand.Execute(null);

        }

        private void bottomGrid_Loading(FrameworkElement sender, object args)
        {
            
        }

        private void UserControl_Loading(FrameworkElement sender, object args)
        {
            
        }

        private void FrameworkElement_OnLoaded(object sender, RoutedEventArgs e)
        {
            var control = sender as Control;
            bool res;
            if (control.ActualWidth >= 720)
            {
                res = VisualStateManager.GoToState(control, "FullState", false);
            }
            else
            {
                res = VisualStateManager.GoToState(control, "CompactState", false);
            }
        }

        private void FrameworkElement_OnDataContextChanged(FrameworkElement sender, DataContextChangedEventArgs args)
        {
            
        }
    }
}
