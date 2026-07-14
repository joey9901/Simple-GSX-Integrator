using Microsoft.FlightSimulator.SimConnect;
using SimpleGsxIntegrator.Core;
using System.Runtime.InteropServices;

namespace SimpleGsxIntegrator.Aircraft.FlyByWire;

internal sealed class JustFlightF100Adapter : AircraftAdapterBase
{
    public override string DisplayName => "Just Flight Fokker 70/100";
    public override string[] TitleKeywords => ["Fokker"];
    public override string parkingBrakeVariable => "L:F100_PED_PARKING_BRAKE_LEVER";
}
