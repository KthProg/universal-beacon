using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using UniversalBeacon.Library.Core.Entities;
using UniversalBeacon.Library.Core.Interop;

namespace UniversalBeacon.Library.Core.Parsing
{
    public static class BeaconRawDataParser
    {
        public static Beacon ParseRawData(byte[] rawData, bool includesHeaderBytes = true)
        {

            // https://github.com/inthepocket/ibeacon-scanner-android/blob/1.2.1/ibeaconscanner/src/main/java/mobi/inthepocket/android/beacons/ibeaconscanner/ScannerScanCallback.java#L73
            int startByte = includesHeaderBytes ? 2 : 0;
            bool wasIBeaconPatternFound = false;
            while (startByte <= 5)
            {
                if ((rawData[startByte + 2] & 0xff) == 0x02 && // identifies an iBeacon
                        (rawData[startByte + 3] & 0xff) == 0x15)
                {
                    // identifies correct data length
                    wasIBeaconPatternFound = true;
                    break;
                }
                startByte++;
            }

            if (!wasIBeaconPatternFound)
            {
                return null;
            }

            // get the UUID from the hex result
            byte[] uuidBytes = new byte[16];
            Array.Copy(rawData, startByte + 4, uuidBytes, 0, 16);

            // get the major from hex result
            byte[] majorBytes = new byte[2];
            Array.Copy(rawData, startByte + 20, majorBytes, 0, 2);

            // get the minor from hex result
            byte[] minorBytes = new byte[2];
            Array.Copy(rawData, startByte + 22, minorBytes, 0, 2);

            return new Beacon(Beacon.BeaconTypeEnum.iBeacon)
            {
                Region = new BeaconRegion
                {
                    Uuid = BitConverter.ToString(uuidBytes),
                    MajorVersion = BitConverter.ToUInt16(majorBytes, 0),
                    MinorVersion = BitConverter.ToUInt16(minorBytes, 0)
                }
            };
        }
    }
}
