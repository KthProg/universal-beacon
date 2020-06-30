using System;
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

                        // TODO: move to beacon parser, check for other beacons not just iBeacon

                        // https://github.com/inthepocket/ibeacon-scanner-android/blob/1.2.1/ibeaconscanner/src/main/java/mobi/inthepocket/android/beacons/ibeaconscanner/ScannerScanCallback.java#L73
                        int startByte = 2;
                        bool patternFound = false;
                        while (startByte <= 5)
                        {
                            if ((scanData[startByte + 2] & 0xff) == 0x02 && // identifies an iBeacon
                                    (scanData[startByte + 3] & 0xff) == 0x15)
                            {
                                // identifies correct data length
                                patternFound = true;
                                break;
                            }
                            startByte++;
                        }

                        if (!patternFound) {
                            Debug.WriteLine($"Packet is not iBeacon at {result.Device.Address}", LogTag);
                            return; 
                        }

                        Debug.WriteLine($"Packet is iBeacon at {result.Device.Address}", LogTag);

                        // get the UUID from the hex result
                        byte[] uuidBytes = new byte[16];
                        Array.Copy(scanData, startByte + 4, uuidBytes, 0, 16);

                        // get the major from hex result
                        byte[] majorBytes = new byte[2];
                        Array.Copy(scanData, startByte + 20, majorBytes, 0, 2);

                        // get the minor from hex result
                        byte[] minorBytes = new byte[2];
                        Array.Copy(scanData, startByte + 22, minorBytes, 0, 2);

                        var p = new BeaconPacket
                        {
                            Region = new BeaconRegion
                            {
                                Uuid = BitConverter.ToString(uuidBytes),
                                MajorVersion = BitConverter.ToUInt16(majorBytes),
                                MinorVersion = BitConverter.ToUInt16(minorBytes),
                            },
                            BluetoothAddress = result.Device.Address.ToNumericAddress(),
                            RawSignalStrengthInDBm = (short)result.Rssi,
                            Timestamp = DateTimeOffset.FromUnixTimeMilliseconds(result.TimestampNanos / 1000),
                        };

                        OnAdvertisementPacketReceived?.Invoke(this, new BeaconPacketArgs(p));
                    }
                    catch (Exception)
                    {
                        Debug.WriteLine("Failed to parse beacon", LogTag);
                    }
                    break;
                default:
                    break;
            }
        }
    }
}
