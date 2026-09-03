using Microsoft.FlightSimulator.SimConnect;
using SimpleGsxIntegrator.Core;

namespace SimpleGsxIntegrator.Aircraft.Synaptic;

internal sealed class SynapticA220Adapter : AircraftAdapterBase, IGroundEquipment
{
    public override string DisplayName => "Synaptic A22X";
    public override string engine1RunningVariable => "L:A22X Engine 1 N1";
    public override string engine2RunningVariable => "L:A22X Engine 2 N1";
    public override string beaconLightVariable => "L:A22X Beacon Lights";
    public override string parkingBrakeVariable => "L:A22X Parking Brake";

    private bool _chocksSet = false;
    private bool _gpuConnected = false;

    public bool? ChocksSet => _chocksSet;
    public bool? GpuConnected => _gpuConnected;

    public override void OnSimConnectConnected(SimConnect sc)
    {
        base.OnSimConnectConnected(sc);

        RegisterWritableLVar(SimDef.A220Chocks, A220Constants.LVar_Chocks);
        RegisterWritableLVar(SimDef.A220Gpu, A220Constants.LVar_Gpu);

        sc.AddToDataDefinition(SimDef.A220GroundState,
            A220Constants.LVar_Chocks, "Number", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
        sc.AddToDataDefinition(SimDef.A220GroundState,
            A220Constants.LVar_Gpu, "Number", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
        sc.RegisterDataDefineStruct<A220GroundStateStruct>(SimDef.A220GroundState);
        sc.RequestDataOnSimObject(
            SimReq.SynapticA220GroundState, SimDef.A220GroundState,
            SimConnect.SIMCONNECT_OBJECT_ID_USER,
            SIMCONNECT_PERIOD.SECOND,
            SIMCONNECT_DATA_REQUEST_FLAG.CHANGED,
            0, 0, 0);
    }

    public override void OnSimObjectData(SIMCONNECT_RECV_SIMOBJECT_DATA data)
    {
        if (data.dwRequestID != (uint)SimReq.SynapticA220GroundState) return;
        var s = (A220GroundStateStruct)data.dwData[0];
        var chocks = s.Chocks > 0.5;
        var gpu = s.Gpu > 0.5;
        if (_chocksSet == chocks && _gpuConnected == gpu) return;
        _chocksSet = chocks;
        _gpuConnected = gpu;
        NotifyGroundEquipmentStateChanged();
    }

    public Task SetGpu(bool connected)
    {
        Logger.Debug($"SynapticA220Adapter: GPU → {(connected ? "ON" : "OFF")}");
        WriteLVar(SimDef.A220Gpu, connected ? 1.0 : 0.0);
        return Task.CompletedTask;
    }

    public Task SetChocks(bool placed)
    {
        Logger.Debug($"SynapticA220Adapter: Chocks → {(placed ? "SET" : "REMOVED")}");
        WriteLVar(SimDef.A220Chocks, placed ? 1.0 : 0.0);
        return Task.CompletedTask;
    }
}
