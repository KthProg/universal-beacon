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


            public override void Failed(CLLocationManager manager, NSError error)
            {
                Debug.WriteLine($"something went wrong {error.DebugDescription}", LogTag);
            }

            public override void DidStartMonitoringForRegion(CLLocationManager manager, CLRegion region)
            {
                Debug.WriteLine($"monitoring region {region.Identifier}", LogTag);

                _bluetoothPacketProvider._locationManager.RequestState(region);
            }

            public override void MonitoringFailed(CLLocationManager manager, CLRegion region, NSError error)
            {
                Debug.WriteLine($"failed monitoring region {region.Identifier}", LogTag);
            }

            public override void DidDetermineState(CLLocationManager manager, CLRegionState state, CLRegion region)
            {
                Debug.WriteLine($"region state determined for {region.Identifier} {state}", LogTag);
            }

            public override void RegionEntered(CLLocationManager manager, CLRegion region)
            {
                Debug.WriteLine($"Region entered {region.Identifier}", LogTag);

                _bluetoothPacketProvider.BeaconRegionEntered?.Invoke(_bluetoothPacketProvider, new BeaconPacketArgs(new BeaconPacket(new BeaconRegion(region.Identifier))));
            }

            public override void RegionLeft(CLLocationManager manager, CLRegion region)
            {
                Debug.WriteLine($"Region left {region.Identifier}", LogTag);

                _bluetoothPacketProvider.BeaconRegionExited?.Invoke(_bluetoothPacketProvider, new BeaconPacketArgs(new BeaconPacket(new BeaconRegion(region.Identifier))));
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

            try
            {
                CLBeaconRegion oldRegion = new CLBeaconRegion(beaconRegion.Uuid, beaconRegion.Major.UInt16Value, beaconRegion.Minor.UInt16Value, "TPMS");

                _locationManager.StopMonitoring(oldRegion);
            }
            catch (Exception)
            {

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

            WatcherStopped?.Invoke(sender: this, e: new BeaconError(BeaconError.BeaconErrorType.Success));
        }
    }
}