using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Xml.XPath;
using Android.Bluetooth;
using Android.Bluetooth.LE;
using Android.Nfc.Tech;
using Android.Runtime;
using UniversalBeacon.Library.Core.Interop;
using UniversalBeacon.Library.Core.Parsing;

namespace UniversalBeacon.Library
{
    internal class BLEScanCallback : ScanCallback
    {
        private readonly string LogTag = nameof(BLEScanCallback);

        public event EventHandler<BeaconPacketArgs> OnAdvertisementPacketReceived;

        public override void OnScanFailed([GeneratedEnum] ScanFailure errorCode)
        {
            Debug.WriteLine($"scan failed, error: {errorCode}", LogTag);

            base.OnScanFailed(errorCode);
        }

        public override void OnScanResult([GeneratedEnum] ScanCallbackType callbackType, ScanResult result)
        {
            base.OnScanResult(callbackType, result);

            switch (result.Device.Type)
            {
                case BluetoothDeviceType.Le:
                case BluetoothDeviceType.Unknown:
                    try
                    {
                        byte[] scanData = result.ScanRecord.GetBytes();

                        var beacon = BeaconRawDataParser.ParseRawData(scanData);

                        if (beacon is null || beacon.BeaconType != Core.Entities.Beacon.BeaconTypeEnum.iBeacon) {
                            return; 
                        }

                        var packet = new BeaconPacket(
                            beacon.Region, 
                            result.Device.Address.ToNumericAddress(), 
                            DateTimeOffset.FromUnixTimeMilliseconds(result.TimestampNanos / 1000),
                            (short)result.Rssi);

                        OnAdvertisementPacketReceived?.Invoke(this, new BeaconPacketArgs(packet));
                    }
                    catch (Exception)
                    {
                        Debug.WriteLine("Failed to parse beacon", LogTag);
                    }
                    break;
                default:
                        //Debug.WriteLine($"Skipped parsing bluetooth device type {result.Device.Type}", LogTag);
                    break;
            }
        }
    }
}
