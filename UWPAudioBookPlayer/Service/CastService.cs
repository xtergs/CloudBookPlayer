using System;
using Windows.Foundation;
using Windows.Media.Casting;
using Windows.Media.Playback;
using Windows.UI.Xaml;

namespace UWPAudioBookPlayer.Service
{
    public class CastService
    {
        private CastingConnection connection;
        private CastingDevicePicker picker = null;
        private MediaPlayer player;

        public CastService(MediaPlayer player)
        {
            this.player = player;
            picker = new CastingDevicePicker();
            picker.Filter.SupportsAudio = true;
            picker.Filter.SupportsPictures = false;
            picker.Filter.SupportsVideo = false;

            picker.CastingDeviceSelected += PickerOnCastingDeviceSelected;
            picker.CastingDevicePickerDismissed += PickerOnCastingDevicePickerDismissed;
        }

        private void PickerOnCastingDevicePickerDismissed(CastingDevicePicker sender, object args)
        {
            
        }

        private async void PickerOnCastingDeviceSelected(CastingDevicePicker sender, CastingDeviceSelectedEventArgs args)
        {
            await
                Windows.ApplicationModel.Core.CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, async () =>
            {
                try
                {
                    //DateTime t1 = DateTime.Now;
                    //DeviceInformation mydevice = await DeviceInformation.CreateFromIdAsync(args.SelectedCastingDevice.Id);
                    //DateTime t2 = DateTime.Now;

                    //TimeSpan ts = new TimeSpan(t2.Ticks - t1.Ticks);

                    //System.Diagnostics.Debug.WriteLine(string.Format("DeviceInformation.CreateFromIdAsync took '{0} seconds'", ts.TotalSeconds));

                    if (connection != null)
                    {
                        connection.ErrorOccurred -= Connection_ErrorOccurred;
                        connection.StateChanged -= Connection_StateChanged;
                    }

                    //Create a casting conneciton from our selected casting device
                    connection = args.SelectedCastingDevice.CreateCastingConnection();

                    connection.ErrorOccurred += Connection_ErrorOccurred;
                    connection.StateChanged += Connection_StateChanged;
                    //Hook up the casting events

                    // Get the casting source from the MediaElement
                    CastingSource source = null;

                    try
                    {
                        // Get the casting source from the Media Element
                        source = player.GetAsCastingSource();

                        // Start Casting
                        CastingConnectionErrorStatus status = await connection.RequestStartCastingAsync(source);

                        if (status == CastingConnectionErrorStatus.Succeeded)
                        {
                            player.Play();
                        }

                    }
                    catch
                    {
                        throw;
                    }
                }
                catch (Exception ex)
                {
                    throw;
                }
            });
        }

        private void Connection_StateChanged(CastingConnection sender, object args)
        {
            
        }

        private void Connection_ErrorOccurred(CastingConnection sender, CastingConnectionErrorOccurredEventArgs args)
        {
            
        }


        public void ShowPicker()
        {
            picker.Show(new Rect(0, 0, 300, 300));
        }

    }
}
