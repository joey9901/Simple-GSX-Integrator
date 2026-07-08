using Microsoft.FlightSimulator.SimConnect;
using SimpleGsxIntegrator.Core;
using System.Runtime.InteropServices;

namespace SimpleGsxIntegrator.Aircraft.FlyByWire;

internal sealed class FbwA380Adapter : AircraftAdapterBase
{
    public override string ParkingBrakeVariable => "L:A32NX_PARK_BRAKE_LEVER_POS";
}
