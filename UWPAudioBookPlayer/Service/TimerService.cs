using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using Windows.Devices.Sensors;
using Windows.UI.Core;
using PropertyChanged;
using UWPAudioBookPlayer.Annotations;

namespace UWPAudioBookPlayer.Service
{
	public interface ITimerSettings
	{
		int TimerMinutes { get; set; }
		int DefaultTimerMinutes { get; }
		bool IsActive { get; set; }
	}
	[ImplementPropertyChanged]
	public class TimerService : INotifyPropertyChanged
	{
		public ITimerSettings TimerSettings { get; }

		public TimerService(ITimerSettings timerSettings)
		{
			TimerSettings = timerSettings;
			DelaySeconds = 1;
			if (timerSettings.TimerMinutes <= 0)
				TimerMinutes = timerSettings.DefaultTimerMinutes;
			else
				TimerMinutes = timerSettings.TimerMinutes;
		}

		public event EventHandler TimerStoped;
		private int _delaySeconds;

		private CancellationTokenSource source;
		private int _timerMinutes;
		public TimeSpan Timer { get; set; }
		public TimeSpan Left { get; private set; }

		public int TimerMinutes
		{
			get { return _timerMinutes; }
			set
			{
				_timerMinutes = value;
				TimerSettings.TimerMinutes = value;
				Timer = TimeSpan.FromMinutes(value);
				Left = Timer;
			}
		}

		public int DelaySeconds
		{
			get { return _delaySeconds; }
			set
			{
				_delaySeconds = value;
				DelayTimeSpan = TimeSpan.FromSeconds(value);
			}
		}

		public TimeSpan DelayTimeSpan { get; private set; }

		public bool IsRunnign { get; private set; }

		private Task RunningTimerTask { get; set; }

		public event PropertyChangedEventHandler PropertyChanged;

		public Task StartTimer()
		{
			if (IsRunnign)
				return RunningTimerTask;
			return Task.Run(async () =>
			{
				try
				{
					if (IsRunnign)
						return;
					using (source = new CancellationTokenSource())
					{
						var token = source.Token;
						RunningTimerTask = StartTime(token);
						IsRunnign = true;
						await RunningTimerTask;
					}
				}
				finally
				{
					source = null;
					IsRunnign = false;
					OnTimerStoped();
				}
			});
		}

		private async Task StartTime(CancellationToken token)
		{
			while (true)
			{
				await Task.Delay(DelaySeconds * 1000);
				if (token.IsCancellationRequested)
					return;
				Left = Left.Subtract(DelayTimeSpan);
				if (Left <= TimeSpan.Zero)
				{
					Left = TimeSpan.Zero;
					return;
				}
			}
		}

		public void PauseTimer()
		{
			if (source == null)
				return;
			source.Cancel(false);
		}

		public void StopTimer()
		{
			PauseTimer();
			Left = Timer;
		}

		public void Shake()
		{
			Left = Timer;
		}

		[NotifyPropertyChangedInvocator]
		protected virtual async void OnPropertyChanged([CallerMemberName] string propertyName = null)
		{
			await Windows.ApplicationModel.Core.CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(
				CoreDispatcherPriority.Normal,
				() =>
				{
					PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
				});
		}

		protected virtual void OnTimerStoped()
		{
			TimerStoped?.Invoke(this, EventArgs.Empty);
		}
	}

	[ImplementPropertyChanged]
	internal class TimerWithSensorService : TimerService
	{
		private readonly Accelerometer accelerometer;
		public TimerWithSensorService(ITimerSettings settings) : base(settings)
		{
			accelerometer = Accelerometer.GetDefault(AccelerometerReadingType.Standard);
			accelerometer.Shaken += AccelerometerOnShaken;
		}

		private void AccelerometerOnShaken(Accelerometer sender, AccelerometerShakenEventArgs args)
		{
			Shake();
		}
	}

	[ImplementPropertyChanged]
	public class TimerViewModel : INotifyPropertyChanged
	{
		public event EventHandler TimerStoped;
		private bool _isActive;
		private bool _isCanStart;
		public TimerService TimerService { get; }

		public TimerViewModel(TimerService service)
		{
			TimerService = service;
			IsActive = service.TimerSettings.IsActive;
			service.TimerStoped += async (sender, args) =>
			{
				await Windows.ApplicationModel.Core.CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(
				CoreDispatcherPriority.Normal,
				() =>
				{
					OnTimerStoped();
					var command = TimerStopedCommand;
					if (command != null && command.CanExecute(TimerStopedCommandParam))
						command.Execute(TimerStopedCommandParam);
				});
			};
		}

		public ICommand TimerStopedCommand { get; set; }
		public object TimerStopedCommandParam { get; set; }

		public bool IsCanStart
		{
			get { return _isCanStart; }
			set
			{
				_isCanStart = value;
				if (IsActive && value)
					TimerService.StartTimer();
			}
		}

		public bool IsActive
		{
			get { return _isActive; }
			set
			{
				if (_isActive != value)
					TimerService.TimerSettings.IsActive = value;
				_isActive = value;
				if (value)
					if (IsCanStart)
						TimerService.StartTimer();
					else
						TimerService.PauseTimer();
			}
		}

		public void StartTimer()
		{
			TimerService.StartTimer();
		}

		public void StopTimer()
		{ TimerService.StopTimer(); }

		public void PauseTimer()
		{
			TimerService.PauseTimer();
		}

		public int[] DeleyMinutes { get; } = new[] { 1, 5, 10, 15, 20, 30, 45, 60, 75, 90 };

		protected virtual void OnTimerStoped()
		{
			TimerStoped?.Invoke(this, EventArgs.Empty);
		}

		public event PropertyChangedEventHandler PropertyChanged;

		[NotifyPropertyChangedInvocator]
		protected virtual async void OnPropertyChanged([CallerMemberName] string propertyName = null)
		{
			await Windows.ApplicationModel.Core.CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(
				CoreDispatcherPriority.Normal,
				() =>
				{
					PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
				});
		}
	}
}