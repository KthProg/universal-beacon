using System;
using System.Collections.Generic;
using System.Text;
using UniversalBeacon.Library.Core.Entities;
using UniversalBeacon.Library.Core.Interop;

namespace UniversalBeacon.Library.Core.Parsing
{
    public static class BeaconRawDataParser
    {
        public static Beacon ParseRawData(byte[] rawData)
        {
            // TODO: parse beacon, get type etc.
            return new Beacon(Beacon.BeaconTypeEnum.iBeacon)
            {
                // properties
            };
        }

        public static void AddBeaconPacketData(Beacon beacon, BeaconPacket beaconPacket)
        {
            beacon.BluetoothAddress = beaconPacket.BluetoothAddress;
            beacon.Rssi = beaconPacket.RawSignalStrengthInDBm;
        }
    }
}
