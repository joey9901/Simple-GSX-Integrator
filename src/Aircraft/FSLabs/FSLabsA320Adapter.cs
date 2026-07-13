using Microsoft.FlightSimulator.SimConnect;
using SimpleGsxIntegrator.Core;
using System.Runtime.InteropServices;

namespace SimpleGsxIntegrator.Aircraft.FlyByWire;

internal sealed class FSLabsA320Adapter : AircraftAdapterBase
{
    public override string parkingBrakeVariable => "L:FSLA320_ParkBrake";
}
