using System;
using System.Diagnostics;
using Foundation;
using CoreLocation;
using UniversalBeacon.Library.Core.Interfaces;
using UniversalBeacon.Library.Core.Interop;
using CoreImage;
using System.Linq;

namespace UniversalBeacon.Library
{
    public class CocoaBeaconProvider : NSObject, IBeaconProvider
    {
        private readonly string LogTag = nameof(CocoaBeaconProvider);
        public event EventHandler<BeaconError> WatcherStopped;
        public event EventHandler<BeaconPacketArgs> BeaconRegionEntered;
        public event EventHandler<BeaconPacketArgs> BeaconRegionExited;
        public event EventHandler<BeaconPacketArgs> BeaconReceived;

        private readonly CLLocationManagerDelegate _locationManagerDelegate;
        private readonly CLLocationManager _locationManager;
        private readonly CLBeaconRegion _clBeaconRegion;

        private class UniversalBeaconLocationManagerDelegate : CLLocationManagerDelegate
        {
            private string LogTag => _bluetoothPacketProvider.LogTag;
            private readonly CocoaBeaconProvider _bluetoothPacketProvider;
            public UniversalBeaconLocationManagerDelegate(CocoaBeaconProvider bluetoothPacketProvider) : base()
            {
                if(bluetoothPacketProvider is null) { throw new ArgumentNullException(nameof(bluetoothPacketProvider)); }
                _bluetoothPacketProvider = bluetoothPacketProvider;
            }

            public override void DidVisit(CLLocationManager manager, CLVisit visit)
            {
                Debug.WriteLine($"visited {visit.Coordinate.ToString()}", LogTag);
            }

            public override void DeferredUpdatesFinished(CLLocationManager manager, NSError error)
            {
                Debug.WriteLine($"deferred updates {error.DebugDescription}", LogTag);
            }

            public override void DidRangeBeaconsSatisfyingConstraint(CLLocationManager manager, CLBeacon[] beacons, CLBeaconIdentityConstraint beaconConstraint)
            {
                Debug.WriteLine("ranged beacons by constraint", LogTag);
            }

            public override void Failed(CLLocationManager manager, NSError error)
            {
                Debug.WriteLine($"something went wrong {error.DebugDescription}", LogTag);
            }

            public override void LocationsUpdated(CLLocationManager manager, CLLocation[] locations)
            {
                Debug.WriteLine("locations updated", LogTag);
            }

            public override void UpdatedLocation(CLLocationManager manager, CLLocation newLocation, CLLocation oldLocation)
            {
                Debug.WriteLine("location updated", LogTag);
            }

            public override void DidStartMonitoringForRegion(CLLocationManager manager, CLRegion region)
            {
                Debug.WriteLine($"monitoring region {region.Identifier}", LogTag);
            }

            public override void MonitoringFailed(CLLocationManager manager, CLRegion region, NSError error)
            {
                Debug.WriteLine($"failed monitoring region {region.Identifier}", LogTag);
            }

            public override void DidFailRangingBeacons(CLLocationManager manager, CLBeaconIdentityConstraint beaconConstraint, NSError error)
            {
                Debug.WriteLine("failed ranging beacons", LogTag);
            }

            public override void RangingBeaconsDidFailForRegion(CLLocationManager manager, CLBeaconRegion region, NSError error)
            {
                Debug.WriteLine($"failed ranging beacons for region {region.Identifier}", LogTag);
            }

            public override void DidDetermineState(CLLocationManager manager, CLRegionState state, CLRegion region)
            {
                Debug.WriteLine($"region state determined for {region.Identifier} {state}", LogTag);

                if (state != CLRegionState.Inside || region.Identifier != _bluetoothPacketProvider._clBeaconRegion.Identifier) { return; }

                if (CLLocationManager.IsRangingAvailable)
                {
                    _bluetoothPacketProvider._locationManager.StartRangingBeacons(_bluetoothPacketProvider._clBeaconRegion);
                }
                else
                {
                    Debug.WriteLine("Ranging not available", LogTag);
                }
            }

            public override void RegionEntered(CLLocationManager manager, CLRegion region)
            {
                Debug.WriteLine($"Region entered {region.Identifier}", LogTag);

                _bluetoothPacketProvider.BeaconRegionEntered?.Invoke(_bluetoothPacketProvider, new BeaconPacketArgs(new BeaconPacket(new BeaconRegion(region.Identifier))));

                if (CLLocationManager.IsRangingAvailable)
                {
                    _bluetoothPacketProvider._locationManager.StartRangingBeacons(_bluetoothPacketProvider._clBeaconRegion);
                }
                else
                {
                    Debug.WriteLine("Ranging not available", LogTag);
                }
            }

            public override void RegionLeft(CLLocationManager manager, CLRegion region)
            {
                Debug.WriteLine($"Region left {region.Identifier}", LogTag);

                _bluetoothPacketProvider.BeaconRegionExited?.Invoke(_bluetoothPacketProvider, new BeaconPacketArgs(new BeaconPacket(new BeaconRegion(region.Identifier))));

                if (CLLocationManager.IsRangingAvailable)
                {
                    _bluetoothPacketProvider._locationManager.StopRangingBeacons(_bluetoothPacketProvider._clBeaconRegion);
                }
                else
                {
                    Debug.WriteLine("Ranging not available", LogTag);
                }
            }

            public override void DidRangeBeacons(CLLocationManager manager, CLBeacon[] beacons, CLBeaconRegion region)
            {
                Debug.WriteLine("Beacons ranged", LogTag);

                _bluetoothPacketProvider.BeaconReceived?.Invoke(_bluetoothPacketProvider, new BeaconPacketArgs(new BeaconPacket(new BeaconRegion(region.Identifier))));
            }
        }

        public CocoaBeaconProvider(CLBeaconRegion beaconRegion)
        {
            Debug.WriteLine("constructor()", LogTag);

            if (!CLLocationManager.LocationServicesEnabled)
            {
                Debug.WriteLine("Location services disabled, my bad.", LogTag);
                return;
            }

            _locationManagerDelegate = new UniversalBeaconLocationManagerDelegate(this);

            _locationManager = new CLLocationManager();
            _locationManager.Delegate = _locationManagerDelegate;

            _clBeaconRegion = beaconRegion;
        }

        public void Start()
        {
            Debug.WriteLine($"{nameof(Start)}()", LogTag);

            if (!CLLocationManager.IsMonitoringAvailable(typeof(CLBeaconRegion))){
                Debug.WriteLine("Cannot monitor beacon regions, my bad.", LogTag);
                return;
            }

            _locationManager.StartMonitoring(_clBeaconRegion);
        }

        public void Stop()
        {
            Debug.WriteLine($"{nameof(Stop)}()", LogTag);


            if (CLLocationManager.IsMonitoringAvailable(typeof(CLBeaconRegion)))
            {
                _locationManager.StopMonitoring(_clBeaconRegion);
            }
            else
            {
                Debug.WriteLine("Beacon monitor stop failed, monitoring not available.", LogTag);
            }


            if (CLLocationManager.IsRangingAvailable)
            {
                try
                {
                    _locationManager.StopRangingBeacons(_clBeaconRegion);
                }
                catch (Exception)
                {
                    Debug.WriteLine("Beacon ranging stop failed. Probably already stopped ranging", LogTag);
                }
            }

            WatcherStopped?.Invoke(sender: this, e: new BeaconError(BeaconError.BeaconErrorType.Success));
        }
    }
}