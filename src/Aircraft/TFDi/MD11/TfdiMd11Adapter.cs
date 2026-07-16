using Microsoft.FlightSimulator.SimConnect;
using SimpleGsxIntegrator.Core;

namespace SimpleGsxIntegrator.Aircraft.TFDi;

internal sealed class TfdiMd11Adapter : AircraftAdapterBase, IGroundEquipment
{
    public override string parkingBrakeVariable => TfdiMd11Constants.LVar_ParkingBrake;

    private bool? _chocksSet;
    private bool? _gpuConnected;
    public bool? ChocksSet => _chocksSet;
    public bool? GpuConnected => _gpuConnected;

    public override void OnSimConnectConnected(SimConnect sc)
    {
        base.OnSimConnectConnected(sc);

        RegisterWritableLVar(SimDef.Md11Chocks, TfdiMd11Constants.LVar_Chocks);
        RegisterWritableLVar(SimDef.Md11Gpu, TfdiMd11Constants.LVar_Gpu);

        sc.AddToDataDefinition(SimDef.Md11GroundState,
            TfdiMd11Constants.LVar_Chocks, "Number", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
        sc.AddToDataDefinition(SimDef.Md11GroundState,
            TfdiMd11Constants.LVar_Gpu, "Number", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
        sc.RegisterDataDefineStruct<Md11GroundStateStruct>(SimDef.Md11GroundState);
        sc.RequestDataOnSimObject(
            SimReq.Md11GroundState, SimDef.Md11GroundState,
            SimConnect.SIMCONNECT_OBJECT_ID_USER,
            SIMCONNECT_PERIOD.SECOND,
            SIMCONNECT_DATA_REQUEST_FLAG.CHANGED,
            0, 0, 0);
    }

    public override void OnSimObjectData(SIMCONNECT_RECV_SIMOBJECT_DATA data)
    {
        if (data.dwRequestID != (uint)SimReq.Md11GroundState) return;
        var s = (Md11GroundStateStruct)data.dwData[0];
        var chocks = s.Chocks > 0.5;
        var gpu = s.Gpu > 0.5;
        if (_chocksSet == chocks && _gpuConnected == gpu) return;
        _chocksSet = chocks;
        _gpuConnected = gpu;
        NotifyGroundEquipmentStateChanged();
    }

    public Task SetGpu(bool connected)
    {
        Logger.Debug($"Md11Adapter: GPU → {(connected ? "ON" : "OFF")}");
        WriteLVar(SimDef.Md11Gpu, connected ? 1.0 : 0.0);
        return Task.CompletedTask;
    }

    public Task SetChocks(bool placed)
    {
        Logger.Debug($"Md11Adapter: Chocks → {(placed ? "SET" : "REMOVED")}");
        WriteLVar(SimDef.Md11Chocks, placed ? 1.0 : 0.0);
        return Task.CompletedTask;
    }

    public override void Dispose()
    {
        Logger.Debug("Md11Adapter: disposed");
    }
}
