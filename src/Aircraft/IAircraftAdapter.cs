using Microsoft.FlightSimulator.SimConnect;

namespace SimpleGsxIntegrator.Aircraft;

public interface IAircraftAdapter : IDisposable
{
    void OnSimConnectConnected(SimConnect sc);

    void OnSimObjectData(SIMCONNECT_RECV_SIMOBJECT_DATA data);

    event Action? GroundEquipmentStateChanged; // Used to update the UI, ground equipment vars are updated internally
}
