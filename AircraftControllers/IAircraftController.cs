using Microsoft.FlightSimulator.SimConnect;

namespace SimpleGsxIntegrator
{
    public interface IAircraftController
    {
        void Connect();
        void OnSimObjectDataReceived(SIMCONNECT_RECV_SIMOBJECT_DATA data);
        void Dispose();
    }
}
