using System;
using Windows.Devices.Bluetooth.Advertisement;
using UniversalBeacon.Library.Core.Interfaces;
using UniversalBeacon.Library.Core.Interop;

namespace UniversalBeacon.Library.UWP
{
    public class WindowsBeaconProvider : IBeaconProvider
    {
        public event EventHandler<BeaconError> WatcherStopped;

        public event EventHandler<BeaconPacketArgs> BeaconRegionEntered;
        public event EventHandler<BeaconPacketArgs> BeaconRegionExited;
        public event EventHandler<BeaconPacketArgs> BeaconReceived;

        private readonly BluetoothLEAdvertisementWatcher _watcher;
        private bool _running;

        public WindowsBeaconProvider()
        {
            _watcher = new BluetoothLEAdvertisementWatcher
            {
                ScanningMode = BluetoothLEScanningMode.Active
            };
        }

        /// <summary>
        /// Gets the BluetoothLEAdvertisementWatcher used by the provider instance
        /// </summary>
        public BluetoothLEAdvertisementWatcher AdvertisementWatcher
        {
            get => _watcher;
        }

        public BeaconWatcherStatusCodes WatcherStatus
        {
            get
            {
                if (_watcher == null)
                {
                    return BeaconWatcherStatusCodes.Stopped;
                }

                return (BeaconWatcherStatusCodes)_watcher.Status;
            }
        }

        private void WatcherOnReceived(BluetoothLEAdvertisementWatcher sender, BluetoothLEAdvertisementReceivedEventArgs eventArgs)
        {
            BeaconReceived?.Invoke(this, new BeaconPacketArgs(eventArgs.ToUniversalBLEPacket()));
        }

        public void Start()
        {
            if (_running) return;

            lock (_watcher)
            {
                _watcher.Received += WatcherOnReceived;
                _watcher.Stopped += WatcherOnStopped;
                _watcher.Start();

                _running = true;
            }
        }

        private void WatcherOnStopped(BluetoothLEAdvertisementWatcher sender, BluetoothLEAdvertisementWatcherStoppedEventArgs args)
        {
            WatcherStopped?.Invoke(this, new BeaconError((BeaconError.BeaconErrorType) args.Error));
        }

        public void Stop()
        {
            if (!_running) return;

            lock (_watcher)
            {
                _watcher.Received -= WatcherOnReceived;
                _watcher.Stop();

                _running = false;
            }
        }
    }
}
