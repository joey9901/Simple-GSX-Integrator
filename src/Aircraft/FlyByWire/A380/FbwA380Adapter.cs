using Microsoft.FlightSimulator.SimConnect;
using SimpleGsxIntegrator.Core;
using System.Runtime.InteropServices;

namespace SimpleGsxIntegrator.Aircraft.FlyByWire;

internal sealed class FbwA380Adapter : IAircraftAdapter
{
    public IReadOnlyDictionary<SimVarOverride, string> GetSimVarOverrides()
    {
        return new Dictionary<SimVarOverride, string>
        {
            { SimVarOverride.ParkingBrake, "L:A32NX_PARK_BRAKE_LEVER_POS" },
        };
    }

    public void OnSimConnectConnected(SimConnect sc) { }

    public void OnSimObjectData(SIMCONNECT_RECV_SIMOBJECT_DATA data) { }

    public void Dispose() { }
}
