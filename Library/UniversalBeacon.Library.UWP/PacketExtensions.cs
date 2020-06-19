using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Devices.Bluetooth.Advertisement;
using UniversalBeacon.Library.Core.Interop;

namespace UniversalBeacon.Library.UWP
{
    internal static class PacketExtensions
    {
        /// <summary>
        /// Convert the Windows specific Bluetooth LE advertisment data to the universal cross-platform structure
        /// used by the Universal Beacon Library.
        /// </summary>
        /// <param name="args">Windows Bluetooth LE advertisment data to convert to cross-platform format.</param>
        /// <returns>Packet data converted to cross-platform format.</returns>
        public static BeaconPacket ToUniversalBLEPacket(this BluetoothLEAdvertisementReceivedEventArgs args)
        {
            var packet = new BeaconPacket
            {
                Timestamp = args.Timestamp,
                BluetoothAddress = args.BluetoothAddress,
                RawSignalStrengthInDBm = args.RawSignalStrengthInDBm,
                // AdvertisementType = (BeaconType) args.AdvertisementType,
                // Advertisement = args.Advertisement.ToUniversalAdvertisement()
            };

            return packet;
        }
    }
}
