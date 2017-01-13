using Microsoft.HockeyApp;
using System;
using Windows.ApplicationModel;
using Windows.ApplicationModel.Activation;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;
using Autofac;
using UWPAudioBookPlayer.DAL;
using UWPAudioBookPlayer.Helper;
using UWPAudioBookPlayer.Model;
using UWPAudioBookPlayer.ModelView;
using UWPAudioBookPlayer.Scrapers;
using UWPAudioBookPlayer.Service;

namespace UWPAudioBookPlayer
{
	public static class Global
	{
		public static IContainer container;

		public static MainControlViewModel MainModelView;
		public static BookMarksViewModel BookMarksViewModel;
	}
	
	/// <summary>
	/// Provides application-specific behavior to supplement the default Application class.
	/// </summary>
	sealed partial class App : Application
	{
		public static string GetAppVersion()
		{

			Package package = Package.Current;
			PackageId packageId = package.Id;
			PackageVersion version = packageId.Version;

			return string.Format("{0}.{1}.{2}.{3}", version.Major, version.Minor, version.Build, version.Revision);

		}
		/// <summary>
		/// Initializes the singleton application object.  This is the first line of authored code
		/// executed, and as such is the logical equivalent of main() or WinMain().
		/// </summary>
		public App()
		{
			HockeyClient.Current.Configure("b54af8e72dc84a2090803f7f5433ad24");
			var config = new GoogleAnalytics.EasyTrackerConfig();
			config.AppVersion = GetAppVersion();
			config.TrackingId = "UA-85431708-2";
			config.AppName = Package.Current.PublisherDisplayName;
			config.UseSecure = true;
			GoogleAnalytics.EasyTracker.Current.Config = config;
			this.InitializeComponent();
			this.Suspending += OnSuspending;
			this.EnteredBackground += OnEnteredBackground;
			this.LeavingBackground += OnLeavingBackground;
			Resuming += OnResuming;
		}

		private void OnLeavingBackground(object sender, LeavingBackgroundEventArgs leavingBackgroundEventArgs)
		{
			
		}

		protected override async void OnActivated(IActivatedEventArgs args)
		{

			base.OnActivated(args);
			if (args.Kind == ActivationKind.Protocol)
			{
				switch (args.PreviousExecutionState)
				{
					default:
						var protocolArgs = args as ProtocolActivatedEventArgs;
						if (protocolArgs.Uri.Scheme == "cloudbookplayer")
						await Global.container.Resolve<MainControlViewModel>().StartPlaySource(protocolArgs.Uri.OriginalString.Remove(0, "cloudbookplayer:?".Length));
						break;
				}
				
			}
		}

		private void OnResuming(object sender, object o)
		{
			Global.container.Resolve<MainControlViewModel>().LoadData();
		}

		private async void OnEnteredBackground(object sender, EnteredBackgroundEventArgs enteredBackgroundEventArgs)
		{
			var deferal = enteredBackgroundEventArgs.GetDeferral();
			await Global.container.Resolve<MainControlViewModel>().SaveData();
			deferal.Complete();
		}

		/// <summary>
		/// Invoked when the application is launched normally by the end user.  Other entry points
		/// will be used such as when the application is launched to open a specific file.
		/// </summary>
		/// <param name = "e">Details about the launch request and process.</param>
		protected override async void OnLaunched(LaunchActivatedEventArgs e)
		{
//            LIbriVoxScraper scraper = new LIbriVoxScraper();
//            //var asyncOperationWithProgress = await new HttpClient().GetInputStreamAsync(new Uri(@"https://librivox.org/30000-bequest-and-other-stories-by-mark-twain/"));

//            Uri uri = new Uri(@"https://librivox.org/search/get_results?primary_key=0&search_category=title&sub_category=&search_page=1&search_order=alpha&project_type=either");
//            var filter = new HttpBaseProtocolFilter();
//#if DEBUG
//            filter.IgnorableServerCertificateErrors.Add(ChainValidationResult.Expired);
//            filter.IgnorableServerCertificateErrors.Add(ChainValidationResult.Untrusted);
//            filter.IgnorableServerCertificateErrors.Add(ChainValidationResult.InvalidName);
//#endif
			   

//            var st = await client.GetInputStreamAsync(uri);
//            scraper.ParseListBookByTitle(st.AsStreamForRead());
//            //WebRequest webRequest = WebRequest.Create(uri);
//            //WebResponse webResponse = await webRequest.GetResponseAsync();
//            //var len = webResponse.ContentLength;

//            //scraper.ParseListBookByTitle(webResponse.GetResponseStream());
			
#if DEBUG
			if (System.Diagnostics.Debugger.IsAttached)
			{
				this.DebugSettings.EnableFrameRateCounter = true;
			}

#endif
			Frame rootFrame = Window.Current.Content as Frame;
			// Do not repeat app initialization when the Window already has content,
			// just ensure that the window is active
			if (rootFrame == null)
			{
				RegisterComponents();
				// Create a Frame to act as the navigation context and navigate to the first page
				rootFrame = new Frame();
				rootFrame.NavigationFailed += OnNavigationFailed;
				if (e.PreviousExecutionState == ApplicationExecutionState.Terminated)
				{
				//TODO: Load state from previously suspended application
				}

				// Place the frame in the current Window
				Window.Current.Content = rootFrame;
			}

			Global.container.Resolve<MainControlViewModel>().LoadData();

			if (e.PrelaunchActivated == false)
			{
				if (rootFrame.Content == null)
				{
					// When the navigation stack isn't restored navigate to the first page,
					// configuring the new page by passing required information as a navigation
					// parameter
					rootFrame.Navigate(typeof(MainPage), e.Arguments);
				}

				// Ensure the current window is active
				Window.Current.Activate();
			}
		}

		/// <summary>
		/// Invoked when Navigation to a certain page fails
		/// </summary>
		/// <param name = "sender">The Frame which failed navigation</param>
		/// <param name = "e">Details about the navigation failure</param>
		void OnNavigationFailed(object sender, NavigationFailedEventArgs e)
		{
			throw new Exception("Failed to load Page " + e.SourcePageType.FullName);
		}

		/// <summary>
		/// Invoked when application execution is being suspended.  Application state is saved
		/// without knowing whether the application will be terminated or resumed with the contents
		/// of memory still intact.
		/// </summary>
		/// <param name = "sender">The source of the suspend request.</param>
		/// <param name = "e">Details about the suspend request.</param>
		private async void OnSuspending(object sender, SuspendingEventArgs e)
		{
			//var deferral = e.SuspendingOperation.GetDeferral();
			//await Global.container.Resolve<MainControlViewModel>().SaveData();
			////TODO: Save application state and stop any background activity
			//deferral.Complete();
		}

		private void RegisterComponents()
		{
			Autofac.ContainerBuilder builder = new ContainerBuilder();
			builder.RegisterType<MainControlViewModel>().SingleInstance();
			builder.RegisterType<SettingsModelView>().As<ISettingsService>()
													.As<ITimerSettings>().SingleInstance();
			builder.RegisterType<UniversalApplicationSettingsHelper>().As<IApplicationSettingsHelper>();
			builder.RegisterType<LibrivoxOnlineBooksViewModel>();
			builder.RegisterType<LIbriVoxScraper>();
			builder.RegisterType<RemoteDevicesService>().SingleInstance();
			builder.RegisterType<BookMarksViewModel>();
			builder.RegisterType<UniversalNotification>().As<INotification>();
			builder.RegisterType<JSonRepository>().As<IDataRepository>();
			builder.RegisterType<AudioBookSourceFactory>();
			builder.RegisterType<ManageSources>().SingleInstance();
			builder.RegisterType<OperationsService>();
			builder.Register(c=> new ControllersService(c.ResolveNamed<IControllersRepository>("sqliteControllers"))).SingleInstance();
			builder.RegisterType<SqliteRepository>().Named<IControllersRepository>("sqliteControllers");
			builder.RegisterType<TimerService>();
			builder.RegisterType<TimerViewModel>();
			builder.RegisterType<AudioBookSourceDetailViewModel>();

			var container = builder.Build();
			Global.container = container;
		}

	}
}