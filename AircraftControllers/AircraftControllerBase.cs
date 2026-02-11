using Microsoft.FlightSimulator.SimConnect;

namespace SimpleGsxIntegrator
{
    public abstract class AircraftControllerBase : IAircraftController
    {
        protected readonly SimConnect _simConnect;
        protected readonly SimVarMonitor? _simVarMonitor;

        protected AircraftControllerBase(SimConnect simConnect, SimVarMonitor? simVarMonitor)
        {
            _simConnect = simConnect ?? throw new System.ArgumentNullException(nameof(simConnect));
            _simVarMonitor = simVarMonitor;
        }

        public abstract void Connect();

        public abstract void OnSimObjectDataReceived(SIMCONNECT_RECV_SIMOBJECT_DATA data);

        public virtual void Dispose()
        {
        }

        public virtual void CloseOpenDoors() { }

        public virtual bool AreAnyDoorsOpen() => false;

        public virtual void RemoveGroundEquipment() { }

        public virtual void RequestSnapshot() { }

        public virtual bool IsConnected { get; protected set; } = false;
    }
}
