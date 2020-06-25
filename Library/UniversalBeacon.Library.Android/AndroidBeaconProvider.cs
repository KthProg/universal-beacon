using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Android.Bluetooth;
using Android.Bluetooth.LE;
using Android.Content;
using Android.OS;
using Java.Sql;
using UniversalBeacon.Library.Core.Interfaces;
using UniversalBeacon.Library.Core.Interop;
using SystemDebug = System.Diagnostics.Debug;

namespace UniversalBeacon.Library
{
    public class AndroidBeaconProvider : Java.Lang.Object, IBeaconProvider
    {
        private readonly string LogTag = nameof(AndroidBeaconProvider);

        public event EventHandler<BeaconError> WatcherStopped;
        public event EventHandler<BeaconPacketArgs> BeaconRegionEntered;
        public event EventHandler<BeaconPacketArgs> BeaconRegionExited;
        public event EventHandler<BeaconPacketArgs> BeaconReceived;

        private readonly BluetoothAdapter _adapter;
        private readonly BeaconRegion _beaconRegion;

        private CancellationTokenSource _cancellationTokenSource;
        private CancellationToken _cancellationToken;

        private CancellationTokenSource _regionExitedCancellationTokenSource;
        private CancellationToken _regionExitedCancellationToken;

        private object _lock = new object();
        private const int BeaconExitedTimeoutMs = 30000;
        private DateTime _lastBeaconReceivedDateTime;
        private Task _regionExitedWatchdogTask;
        private Task _scanTask;
        private bool _wasRegionExitTriggered = false;
        private bool _wasFirstRegionEnterTriggered = false;

        private const int ScanDelayMs = 5000;
        private const int ScanDurationMs = 5000;

        public AndroidBeaconProvider(Context context, BeaconRegion beaconRegion)
        {
            SystemDebug.WriteLine(LogTag, LogTag);

            var manager = (BluetoothManager)context.GetSystemService("bluetooth");
            _beaconRegion = beaconRegion;
            _adapter = manager.Adapter;
        }

        private void ScanCallback_OnAdvertisementPacketReceived(object sender, BeaconPacketArgs e)
        {
            SystemDebug.WriteLine($"Beacon received: {e.Data.BluetoothAddress:X} {e.Data.Region.Uuid}", LogTag);

            if (e.Data.Region.Uuid.Replace("-", String.Empty).ToLower() != _beaconRegion.Uuid.Replace("-", String.Empty).ToLower())
            {
                return;
            }

            // matched UUID, this is the region we are looking for, set name
            e.Data.Region.RegionName = _beaconRegion.RegionName;

            lock (_lock)
            {
                try
                {
                    _regionExitedCancellationTokenSource.Cancel();
                    _regionExitedCancellationTokenSource.Dispose();
                }
                catch (Exception) { }

                _regionExitedCancellationTokenSource = new CancellationTokenSource();
                _regionExitedCancellationToken = _regionExitedCancellationTokenSource.Token;

                // start task to kick off region exit if no beacon is received for a period of time
                _regionExitedWatchdogTask = Task.Run(WaitDelayAndCheckForRegionExited(_regionExitedCancellationToken), _cancellationToken);

                // send region entered if region was exited and new beacon received,
                // or if this is the first beacon we received
                if (_wasRegionExitTriggered || !_wasFirstRegionEnterTriggered)
                {
                    _wasFirstRegionEnterTriggered = true;
                    BeaconRegionEntered?.Invoke(this, new BeaconPacketArgs(e.Data));
                }

                BeaconReceived?.Invoke(this, new BeaconPacketArgs(e.Data));

                _lastBeaconReceivedDateTime = DateTime.Now;

                _wasRegionExitTriggered = false;
            }
        }

        public Func<Task> WaitDelayAndCheckForRegionExited(CancellationToken regionExitedCancellationToken)
        {
            return async () =>
            {
                // if > 30 seconds since last beacon, send beacon exited
                await Task.Delay(BeaconExitedTimeoutMs, _cancellationToken);
                lock (_lock)
                {
                    if (!regionExitedCancellationToken.IsCancellationRequested)
                    {
                        _wasRegionExitTriggered = true;
                        SystemDebug.WriteLine("Region exited", LogTag);
                        BeaconRegionExited?.Invoke(this, new BeaconPacketArgs(new BeaconPacket(_beaconRegion)));
                    }
                }
            };
        }

        public void Start()
        {
            SystemDebug.WriteLine($"{nameof(Start)}()", LogTag);

            if (_adapter is null || _adapter.BluetoothLeScanner is null)
            {
                SystemDebug.WriteLine("adapter is null, please turn bluetooth on", LogTag);
                return;
            }

            var scanSettings = new ScanSettings.Builder().SetScanMode(Android.Bluetooth.LE.ScanMode.LowPower).Build();

            SystemDebug.WriteLine("starting scan", LogTag);

            _cancellationTokenSource = new CancellationTokenSource();
            _cancellationToken = _cancellationTokenSource.Token;

            _scanTask = Task.Run(async () =>
            {
                while (!_cancellationToken.IsCancellationRequested)
                {
                    var scanCallback = new BLEScanCallback();
                    scanCallback.OnAdvertisementPacketReceived += ScanCallback_OnAdvertisementPacketReceived;
                    _adapter.BluetoothLeScanner.StartScan(null, scanSettings, scanCallback);
                    await Task.Delay(ScanDurationMs);
                    _adapter.BluetoothLeScanner.StopScan(scanCallback);
                    await Task.Delay(ScanDelayMs);
                }
            }, _cancellationToken);
        }

        public void Stop()
        {
            _adapter.CancelDiscovery();

            try
            {
                _cancellationTokenSource.Cancel();
                _cancellationTokenSource.Dispose();
            }
            catch (Exception)
            {
                SystemDebug.WriteLine("failed to cancel beacon scanning", LogTag);
            }

            try
            {
                _regionExitedCancellationTokenSource.Cancel();
                _regionExitedCancellationTokenSource.Dispose();
            }
            catch (Exception) {
                SystemDebug.WriteLine("failed to cancel beacon exit task", LogTag);
            }

            WatcherStopped?.Invoke(sender: this, e: new BeaconError(BeaconError.BeaconErrorType.Success));
        }
    }
}
