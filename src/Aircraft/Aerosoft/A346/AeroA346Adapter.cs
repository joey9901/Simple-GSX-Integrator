using Microsoft.FlightSimulator.SimConnect;
using SimpleGsxIntegrator.Core;
using System.Runtime.InteropServices;

namespace SimpleGsxIntegrator.Aircraft.FlyByWire;

internal sealed class A346Adapter : AircraftAdapterBase
{
    public override string parkingBrakeVariable => "L:ParkingBrake_Position";
    public override string beaconLightVariable => "L:AB_VC_OVH_EXTLIGHT_BEACON_SW";
}
