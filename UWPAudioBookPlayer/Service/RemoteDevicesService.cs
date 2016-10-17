using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading.Tasks;
using Windows.Foundation.Collections;
using Windows.System;
using Windows.System.RemoteSystems;
using UWPAudioBookPlayer.ModelView;

namespace UWPAudioBookPlayer.Service
{
    public class RemoteDevicesService
    {
        public event EventHandler SystemsUpdated;

        private readonly List<RemoteSystem> _remoteSystems = new List<RemoteSystem>(1);
        private readonly Dictionary<string, RemoteSystem> _mapRemoteSystems = new Dictionary<string, RemoteSystem>(1);


        private void RemoteSystemWatcher_RemoteSystemAdded(RemoteSystemWatcher sender, RemoteSystemAddedEventArgs args)
        {
            try
            {
                _mapRemoteSystems.Add(args.RemoteSystem.Id, args.RemoteSystem);
                _remoteSystems.Add(args.RemoteSystem);
                OnSystemsUpdated();
            }
            catch (Exception e)
            {
                
            }
        }

        private RemoteSystemWatcher m_remoteSystemWatcher { get; set; }



        public async Task<RemoteSystemAccessStatus> RequestAccess()
        {
            RemoteSystemAccessStatus accessStatus = await RemoteSystem.RequestAccessAsync();
            return accessStatus;
        }

        public void StartWatch()
        {

            m_remoteSystemWatcher = RemoteSystem.CreateWatcher();

            // Subscribing to the event raised when a new remote system is found by the watcher.
            m_remoteSystemWatcher.RemoteSystemAdded += RemoteSystemWatcher_RemoteSystemAdded;

            // Subscribing to the event raised when a previously found remote system is no longer available.
            m_remoteSystemWatcher.RemoteSystemRemoved += RemoteSystemWatcher_RemoteSystemRemoved;

            m_remoteSystemWatcher.RemoteSystemUpdated += MRemoteSystemWatcherOnRemoteSystemUpdated;

            m_remoteSystemWatcher.Start();

        }

        private void MRemoteSystemWatcherOnRemoteSystemUpdated(RemoteSystemWatcher sender, RemoteSystemUpdatedEventArgs args)
        {
            
        }

        public ImmutableList<RemoteSystem> RemoteSystems => _remoteSystems.ToImmutableList();

        private void RemoteSystemWatcher_RemoteSystemRemoved(RemoteSystemWatcher sender, RemoteSystemRemovedEventArgs args)
        {
            _remoteSystems.Add(_mapRemoteSystems[args.RemoteSystemId]);
            _mapRemoteSystems.Remove(args.RemoteSystemId);
            OnSystemsUpdated();
        }

        public void StopWatch()
        {
            m_remoteSystemWatcher.Stop();
            m_remoteSystemWatcher.RemoteSystemAdded -= RemoteSystemWatcher_RemoteSystemAdded;

            // Subscribing to the event raised when a previously found remote system is no longer available.
            m_remoteSystemWatcher.RemoteSystemRemoved -= RemoteSystemWatcher_RemoteSystemRemoved;

            _remoteSystems.Clear();
            _mapRemoteSystems.Clear();
            OnSystemsUpdated();
        }

        protected virtual void OnSystemsUpdated()
        {
            SystemsUpdated?.Invoke(this, EventArgs.Empty);
        }

        public async Task<RemoteLaunchUriStatus> LaunchOnRemote(RemoteSystem remoteSystem, string launchParameters, string version)
        {
            var result = await RemoteLauncher.LaunchUriAsync(new RemoteSystemConnectionRequest(remoteSystem),
                uri: new Uri($"cloudbookplayer:?{version};{launchParameters}"));
            return result;
        }
    }
}
