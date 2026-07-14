using Microsoft.FlightSimulator.SimConnect;
using SimpleGsxIntegrator.Core;
using System.Runtime.InteropServices;

namespace SimpleGsxIntegrator.Aircraft.FlyByWire;

internal sealed class AerosoftA346Adapter : AircraftAdapterBase
{
    public override string DisplayName => "Aerosoft/Toliss A346";
    public override string[] TitleKeywords => ["A346"];
    public override string parkingBrakeVariable => "L:ParkingBrake_Position";
    public override string beaconLightVariable => "L:AB_VC_OVH_EXTLIGHT_BEACON_SW";
}
