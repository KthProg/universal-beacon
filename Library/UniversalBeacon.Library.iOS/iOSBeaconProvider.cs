using CoreLocation;

namespace UniversalBeacon.Library
{
    public class iOSBeaconProvider : CocoaBeaconProvider { 
        public iOSBeaconProvider(CLBeaconRegion beaconRegion) : base(beaconRegion)
        {

        }
    }
}
