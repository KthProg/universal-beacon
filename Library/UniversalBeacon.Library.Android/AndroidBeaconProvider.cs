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

#if DEBUG
        private readonly object _bluetoothDevicesListLock = new object();
        private readonly List<ulong> _bluetoothDevicesWeHaveReceivedBeaconsFrom = new List<ulong>();
#endif

        public AndroidBeaconProvider(Context context, BeaconRegion beaconRegion)
        {
            SystemDebug.WriteLine(LogTag, LogTag);

            var manager = (BluetoothManager)context.GetSystemService("bluetooth");
            _beaconRegion = beaconRegion;
            _adapter = manager.Adapter;
        }

        private void ScanCallback_OnAdvertisementPacketReceived(object sender, BeaconPacketArgs e)
        {
#if DEBUG
            lock (_bluetoothDevicesListLock)
            {
                if (!_bluetoothDevicesWeHaveReceivedBeaconsFrom.Contains(e.Data.BluetoothAddress))
                {
                    SystemDebug.WriteLine($"Beacon received: {e.Data.BluetoothAddress:X} {e.Data.Region.Uuid}", LogTag);
                    _bluetoothDevicesWeHaveReceivedBeaconsFrom.Add(e.Data.BluetoothAddress);
                }
            }
#endif

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
                try
                {
                    // if > 30 seconds since last beacon, send beacon exited
                    await Task.Delay(BeaconExitedTimeoutMs, _cancellationToken);
                }
                catch (TaskCanceledException)
                {
                    SystemDebug.WriteLine("Region exit delay task cancelled", LogTag);
                    return;
                }
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

            lock (_lock)
            {
                _cancellationTokenSource = new CancellationTokenSource();
                _cancellationToken = _cancellationTokenSource.Token;

                // if Start/Stop were called on different threads and start ran first the token
                // will be canceled and this will cancel immediately anyways

                _scanTask = Task.Run(async () =>
                {
                    while (!_cancellationToken.IsCancellationRequested)
                    {
                        //SystemDebug.WriteLine("scanning for beacons...", LogTag);
                        var scanCallback = new BLEScanCallback();
                        scanCallback.OnAdvertisementPacketReceived += ScanCallback_OnAdvertisementPacketReceived;
                        _adapter.BluetoothLeScanner.StartScan(null, scanSettings, scanCallback);
                        try
                        {
                            await Task.Delay(ScanDurationMs, _cancellationToken);
                        }
                        catch (TaskCanceledException)
                        {
                            SystemDebug.WriteLine("Scan duration delay cancelled", LogTag);
                        }
                        //SystemDebug.WriteLine("scanning for beacons completed...", LogTag);
                        _adapter.BluetoothLeScanner.StopScan(scanCallback);
                        scanCallback.OnAdvertisementPacketReceived -= ScanCallback_OnAdvertisementPacketReceived;
                        //SystemDebug.WriteLine("scanning for beacons paused...", LogTag);
                        try
                        {
                            await Task.Delay(ScanDelayMs, _cancellationToken);
                        }
                        catch (TaskCanceledException)
                        {
                            SystemDebug.WriteLine("Scan delay cancelled", LogTag);
                        }
                    }
                }, _cancellationToken);
            }
        }

        public void Stop()
        {
            lock (_lock)
            {
                SystemDebug.WriteLine("cancelling adapter discovery", LogTag);

                _adapter?.CancelDiscovery();

                try
                {
                    SystemDebug.WriteLine("cancelling beaconing operations", LogTag);

                    _cancellationTokenSource?.Cancel();
                    _cancellationTokenSource?.Dispose();
                }
                catch (Exception)
                {
                    SystemDebug.WriteLine("failed to cancel beacon scanning", LogTag);
                }

                try
                {
                    SystemDebug.WriteLine("cancelling beacon exit task", LogTag);

                    _regionExitedCancellationTokenSource?.Cancel();
                    _regionExitedCancellationTokenSource?.Dispose();
                }
                catch (Exception)
                {
                    SystemDebug.WriteLine("failed to cancel beacon exit task", LogTag);
                }

                WatcherStopped?.Invoke(sender: this, e: new BeaconError(BeaconError.BeaconErrorType.Success));

#if DEBUG
                lock (_bluetoothDevicesListLock)
                {
                    _bluetoothDevicesWeHaveReceivedBeaconsFrom?.Clear();
                }
#endif
            }
        }
    }
}
