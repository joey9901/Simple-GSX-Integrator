using Microsoft.FlightSimulator.SimConnect;
using SimpleGsxIntegrator.Core;

namespace SimpleGsxIntegrator.Aircraft.A330;

public sealed class IniA330Adapter : AircraftAdapterBase, IGroundEquipment, IEngineCovers
{
    private bool? _chocksSet;
    private bool? _gpuConnected;
    public bool? ChocksSet => _chocksSet;
    public bool? GpuConnected => _gpuConnected;

    public override void OnSimConnectConnected(SimConnect sc)
    {
        base.OnSimConnectConnected(sc);

        RegisterWritableLVar(SimDef.A330Chocks, A330Constants.AVar_Chocks);
        RegisterWritableLVar(SimDef.A330Gpu, A330Constants.LVar_Gpu);
        RegisterWritableLVar(SimDef.A330EngineCover, A330Constants.AVar_EngineCover);
        RegisterWritableLVar(SimDef.A330PitotCover, A330Constants.AVar_PitotCover);

        sc.AddToDataDefinition(SimDef.A330GroundState,
            A330Constants.AVar_Chocks, "Number", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
        sc.AddToDataDefinition(SimDef.A330GroundState,
            A330Constants.LVar_Gpu, "Number", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
        sc.RegisterDataDefineStruct<A330GroundStateStruct>(SimDef.A330GroundState);
        sc.RequestDataOnSimObject(
            SimReq.A330GroundState, SimDef.A330GroundState,
            SimConnect.SIMCONNECT_OBJECT_ID_USER,
            SIMCONNECT_PERIOD.SECOND,
            SIMCONNECT_DATA_REQUEST_FLAG.CHANGED,
            0, 0, 0);
    }

    public override void OnSimObjectData(SIMCONNECT_RECV_SIMOBJECT_DATA data)
    {
        if (data.dwRequestID != (uint)SimReq.A330GroundState) return;
        var s = (A330GroundStateStruct)data.dwData[0];
        var chocks = s.Chocks > 0.5;
        var gpu = s.Gpu > 0.5;
        if (_chocksSet == chocks && _gpuConnected == gpu) return;
        _chocksSet = chocks;
        _gpuConnected = gpu;
        NotifyGroundEquipmentStateChanged();
    }

    public Task SetGpu(bool connected)
    {
        Logger.Debug($"IniA330Adapter: GPU → {(connected ? "ON" : "OFF")}");
        WriteLVar(SimDef.A330Gpu, connected ? 1.0 : 0.0);
        return Task.CompletedTask;
    }

    public Task SetChocks(bool placed)
    {
        Logger.Debug($"IniA330Adapter: Chocks → {(placed ? "SET" : "REMOVED")}");
        WriteLVar(SimDef.A330Chocks, placed ? 1.0 : 0.0);
        return Task.CompletedTask;
    }

    public Task RemoveCovers()
    {
        Logger.Debug("IniA330Adapter: removing covers");
        WriteLVar(SimDef.A330EngineCover, 0.0);
        WriteLVar(SimDef.A330PitotCover, 0.0);
        return Task.CompletedTask;
    }

    public override void Dispose()
    {
        Logger.Debug("IniA330Adapter: disposed");
    }
}
