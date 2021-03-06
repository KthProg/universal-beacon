﻿using System;
using UniversalBeacon.Library.Core.Interop;

namespace UniversalBeacon.Library.Core.Interfaces
{    
    public interface IBeaconProvider
    {
        /// <summary>
        /// Event is invoked whenever a new Bluetooth Beacon region has been entered.
        /// Usually handled directly by the library. If required, your app implementation can
        /// also subscribe to get notified about events.
        /// </summary>
        event EventHandler<BeaconPacketArgs> BeaconRegionEntered;
        /// <summary>
        /// Event is invoked whenever a new Bluetooth Beacon region has been exited.
        /// Usually handled directly by the library. If required, your app implementation can
        /// also subscribe to get notified about events.
        /// </summary>
        event EventHandler<BeaconPacketArgs> BeaconRegionExited;
        /// <summary>
        /// Event is invoked whenever a Bluetooth Beacon has been received.
        /// Usually handled directly by the library. If required, your app implementation can
        /// also subscribe to get notified about events.
        /// </summary>
        event EventHandler<BeaconPacketArgs> BeaconReceived;
        /// <summary>
        /// Wrapper for the Bluetooth LE Watcher stopped event of the underlying OS.
        /// Currently used by UWP platform and invoked whenever there has been an issue
        /// starting the Bluetooth LE watcher or if an issue occured while watching for
        /// beacons (e.g., if the user turned off Bluetooth while the app is running).
        /// </summary>
        event EventHandler<BeaconError> WatcherStopped;
        /// <summary>
        /// Start watching for Bluetooth beacons.
        /// </summary>
        void Start();
        /// <summary>
        /// Stop watching for Bluetooth beacons.
        /// </summary>
        void Stop();
    }
}
