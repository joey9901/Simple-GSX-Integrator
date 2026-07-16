using Microsoft.FlightSimulator.SimConnect;
using SimpleGsxIntegrator.Core;

namespace SimpleGsxIntegrator.Aircraft.FSS;

internal sealed class FSSEJetsAdapter : AircraftAdapterBase, IGroundEquipment
{
    public override string DisplayName => "FSS E-Jets Series";

    private bool? _gpuConnected;
    private bool? _chocksSet;
    public bool? GpuConnected => _gpuConnected;
    public bool? ChocksSet => _chocksSet;

    private double _gpuState = FSSGpuState.ObjectHidden;

    public override void OnSimConnectConnected(SimConnect sc)
    {
        base.OnSimConnectConnected(sc);

        RegisterWritableLVar(SimDef.FSSEJetsToggleGpu, FSSEJetsConstants.LVar_ToggleGpu);
        RegisterWritableLVar(SimDef.FSSEJetsChockF, FSSEJetsConstants.LVar_ChockF);
        RegisterWritableLVar(SimDef.FSSEJetsChockL, FSSEJetsConstants.LVar_ChockL);
        RegisterWritableLVar(SimDef.FSSEJetsChockR, FSSEJetsConstants.LVar_ChockR);

        sc.AddToDataDefinition(SimDef.FSSEJetsGroundState, FSSEJetsConstants.LVar_GpuState, null, SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
        sc.AddToDataDefinition(SimDef.FSSEJetsGroundState, FSSEJetsConstants.LVar_ChockF, null, SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
        sc.AddToDataDefinition(SimDef.FSSEJetsGroundState, FSSEJetsConstants.LVar_ChockL, null, SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
        sc.AddToDataDefinition(SimDef.FSSEJetsGroundState, FSSEJetsConstants.LVar_ChockR, null, SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
        sc.RegisterDataDefineStruct<FSSGroundStateStruct>(SimDef.FSSEJetsGroundState);
        sc.RequestDataOnSimObject(
            SimReq.FSSEJetsGroundState, SimDef.FSSEJetsGroundState,
            SimConnect.SIMCONNECT_OBJECT_ID_USER,
            SIMCONNECT_PERIOD.SECOND,
            SIMCONNECT_DATA_REQUEST_FLAG.CHANGED,
            0, 0, 0);
    }

    public override void OnSimObjectData(SIMCONNECT_RECV_SIMOBJECT_DATA data)
    {
        if (data.dwRequestID != (uint)SimReq.FSSEJetsGroundState) return;
        var s = (FSSGroundStateStruct)data.dwData[0];

        _gpuState = s.GpuState;
        var gpuConnected = FSSGpuState.IsOn(s.GpuState);
        var chocksSet = s.ChockF > 0.5 && s.ChockL > 0.5 && s.ChockR > 0.5;

        if (_gpuConnected == gpuConnected && _chocksSet == chocksSet) return;
        _gpuConnected = gpuConnected;
        _chocksSet = chocksSet;
        NotifyGroundEquipmentStateChanged();
    }

    public Task SetGpu(bool connected)
    {
        if (FSSGpuState.IsOn(_gpuState) == connected) return Task.CompletedTask;
        if (FSSGpuState.IsBusy(_gpuState)) return Task.CompletedTask;
        WriteLVar(SimDef.FSSEJetsToggleGpu, 1.0);
        Logger.Debug($"FSSEJetsAdapter: GPU → {(connected ? "ON" : "OFF")} (was state {_gpuState})");
        return Task.CompletedTask;
    }

    public Task SetChocks(bool placed)
    {
        double val = placed ? 1.0 : 0.0;
        WriteLVar(SimDef.FSSEJetsChockF, val);
        WriteLVar(SimDef.FSSEJetsChockL, val);
        WriteLVar(SimDef.FSSEJetsChockR, val);
        Logger.Debug($"FSSEJetsAdapter: Chocks → {(placed ? "SET" : "REMOVED")}");
        return Task.CompletedTask;
    }

    public override void Dispose()
    {
        Logger.Debug("FSSEJetsAdapter: disposed");
    }
}
