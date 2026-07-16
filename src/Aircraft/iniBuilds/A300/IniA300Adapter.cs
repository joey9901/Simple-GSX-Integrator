using Microsoft.FlightSimulator.SimConnect;
using SimpleGsxIntegrator.Core;

namespace SimpleGsxIntegrator.Aircraft.A300;

public sealed class IniA300Adapter : AircraftAdapterBase, IGroundEquipment, IEngineCovers, ICargoDoor
{
    private bool? _chocksSet;
    private bool? _gpuConnected;
    private bool? _cargoDoorOpen;
    public bool? ChocksSet => _chocksSet;
    public bool? GpuConnected => _gpuConnected;
    public bool? DoorOpen => _cargoDoorOpen;

    public override void OnSimConnectConnected(SimConnect sc)
    {
        base.OnSimConnectConnected(sc);

        RegisterWritableLVar(SimDef.A300Chocks, A300Constants.LVar_Chocks);
        RegisterWritableLVar(SimDef.A300Gpu, A300Constants.LVar_Gpu);
        RegisterWritableLVar(SimDef.A300Covers, A300Constants.LVar_Covers);
        RegisterWritableLVar(SimDef.A300CargoDoor, A300Constants.LVar_CargoDoor);

        sc.AddToDataDefinition(SimDef.A300GroundState,
            A300Constants.LVar_Chocks, "Number", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
        sc.AddToDataDefinition(SimDef.A300GroundState,
            A300Constants.LVar_Gpu, "Number", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
        sc.AddToDataDefinition(SimDef.A300GroundState,
            A300Constants.LVar_CargoDoor, "Number", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
        sc.RegisterDataDefineStruct<A300GroundStateStruct>(SimDef.A300GroundState);
        sc.RequestDataOnSimObject(
            SimReq.A300GroundState, SimDef.A300GroundState,
            SimConnect.SIMCONNECT_OBJECT_ID_USER,
            SIMCONNECT_PERIOD.SECOND,
            SIMCONNECT_DATA_REQUEST_FLAG.CHANGED,
            0, 0, 0);
    }

    public override void OnSimObjectData(SIMCONNECT_RECV_SIMOBJECT_DATA data)
    {
        if (data.dwRequestID != (uint)SimReq.A300GroundState) return;
        var s = (A300GroundStateStruct)data.dwData[0];
        var chocks = s.Chocks > 0.5;
        var gpu = s.Gpu > 0.5;
        var door = s.CargoDoor > 50.0;
        if (_chocksSet == chocks && _gpuConnected == gpu && _cargoDoorOpen == door) return;
        _chocksSet = chocks;
        _gpuConnected = gpu;
        _cargoDoorOpen = door;
        NotifyGroundEquipmentStateChanged();
    }

    public Task SetGpu(bool connected)
    {
        Logger.Debug($"IniA300Adapter: GPU → {(connected ? "ON" : "OFF")}");
        WriteLVar(SimDef.A300Gpu, connected ? 1.0 : 0.0);
        return Task.CompletedTask;
    }

    public Task SetChocks(bool placed)
    {
        Logger.Debug($"IniA300Adapter: Chocks → {(placed ? "SET" : "REMOVED")}");
        WriteLVar(SimDef.A300Chocks, placed ? 1.0 : 0.0);
        return Task.CompletedTask;
    }

    public Task RemoveCovers()
    {
        Logger.Debug("IniA300Adapter: removing covers");
        WriteLVar(SimDef.A300Covers, 0.0);
        return Task.CompletedTask;
    }

    public Task OpenCargoDoor()
    {
        Logger.Debug("IniA300Adapter: opening main cargo door");
        WriteLVar(SimDef.A300CargoDoor, A300Constants.CargoDoorOpen);
        return Task.CompletedTask;
    }

    public Task CloseCargoDoor()
    {
        Logger.Debug("IniA300Adapter: closing main cargo door");
        WriteLVar(SimDef.A300CargoDoor, A300Constants.CargoDoorClosed);
        return Task.CompletedTask;
    }

    public override void Dispose()
    {
        Logger.Debug("IniA300Adapter: disposed");
    }
}
