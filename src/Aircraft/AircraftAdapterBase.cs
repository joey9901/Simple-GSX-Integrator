using System.Runtime.InteropServices;
using Microsoft.FlightSimulator.SimConnect;
using SimpleGsxIntegrator.Core;

namespace SimpleGsxIntegrator.Aircraft;

public class AircraftAdapterBase : IAircraftAdapter
{
    public virtual string DisplayName => GetType().Name;

    public virtual string parkingBrakeVariable => "BRAKE PARKING INDICATOR";
    public virtual string beaconLightVariable => "LIGHT BEACON";
    public virtual string engine1RunningVariable => "GENERAL ENG COMBUSTION:1";
    public virtual string engine2RunningVariable => "GENERAL ENG COMBUSTION:2";
    public virtual string engine3RunningVariable => "GENERAL ENG COMBUSTION:3";
    public virtual string engine4RunningVariable => "GENERAL ENG COMBUSTION:4";

    public event Action? GroundEquipmentStateChanged;
    protected void NotifyGroundEquipmentStateChanged() => GroundEquipmentStateChanged?.Invoke();

    protected SimConnect? SimConnection { get; private set; }

    public virtual void OnSimConnectConnected(SimConnect sc) => SimConnection = sc;

    public virtual void OnSimObjectData(SIMCONNECT_RECV_SIMOBJECT_DATA data) { }

    public virtual void Dispose() { }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    protected struct SimVarValue { public double Value; }

    private protected void RegisterWritableLVar(SimDef def, string lvar)
    {
        SimConnection!.AddToDataDefinition(def, lvar, "Number", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
        SimConnection.RegisterDataDefineStruct<SimVarValue>(def);
    }

    private protected void WriteLVar(SimDef def, double value)
    {
        if (SimConnection == null) return;
        try
        {
            SimConnection.SetDataOnSimObject(def, SimConnect.SIMCONNECT_OBJECT_ID_USER,
                SIMCONNECT_DATA_SET_FLAG.DEFAULT, new SimVarValue { Value = value });
        }
        catch (Exception ex)
        {
            Logger.Warning($"{GetType().Name}: WriteLVar({def}) = {value} failed: {ex.Message}");
        }
    }
}
