using Microsoft.FlightSimulator.SimConnect;
using SimpleGsxIntegrator.Core;
using System.Runtime.InteropServices;

namespace SimpleGsxIntegrator.Aircraft.FlyByWire;

internal sealed class FbwA380Adapter : AircraftAdapterBase
{
    public override string DisplayName => "FlyByWire A380";
    public override string[] TitleKeywords => ["FlyByWire", "A380"];
    public override string parkingBrakeVariable => "L:A32NX_PARK_BRAKE_LEVER_POS";
    public override string beaconLightVariable => "L:LIGHTING_BEACON_0";
}
