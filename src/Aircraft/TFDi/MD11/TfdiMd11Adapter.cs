using Microsoft.FlightSimulator.SimConnect;
using SimpleGsxIntegrator.Core;
using System.Runtime.InteropServices;

namespace SimpleGsxIntegrator.Aircraft.TFDi;

internal sealed class TfdiMd11Adapter : AircraftAdapterBase
{
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    private struct ScalarStruct { public double Value; }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    private struct GroundStateStruct
    {
        public double Chocks;  // L:MD11_EXT_CHOCKS — 1 = placed, 0 = removed
        public double Gpu;     // L:MD11_EXT_GPU    — 1 = connected, 0 = disconnected
    }

    private SimConnect? _sc;

    public override string DisplayName => "TFDi MD-11";
    public override string[] TitleKeywords => ["MD-11"];
    public override string parkingBrakeVariable => TfdiMd11Constants.LVar_ParkingBrake;
    public override bool canRemoveAndPlaceGroundEquipment => true;

    private bool? _chocksSet;
    private bool? _gpuConnected;
    public override bool? ChocksSet => _chocksSet;
    public override bool? GpuConnected => _gpuConnected;

    public override void OnSimConnectConnected(SimConnect sc)
    {
        _sc = sc;

        // Write-only SimDefs (SetDataOnSimObject)
        sc.AddToDataDefinition(SimDef.Md11Chocks,
            TfdiMd11Constants.LVar_Chocks, "Number", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
        sc.RegisterDataDefineStruct<ScalarStruct>(SimDef.Md11Chocks);

        sc.AddToDataDefinition(SimDef.Md11Gpu,
            TfdiMd11Constants.LVar_Gpu, "Number", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
        sc.RegisterDataDefineStruct<ScalarStruct>(SimDef.Md11Gpu);

        // Combined read struct — polled every second
        sc.AddToDataDefinition(SimDef.Md11GroundState,
            TfdiMd11Constants.LVar_Chocks, "Number", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
        sc.AddToDataDefinition(SimDef.Md11GroundState,
            TfdiMd11Constants.LVar_Gpu, "Number", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
        sc.RegisterDataDefineStruct<GroundStateStruct>(SimDef.Md11GroundState);
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
        var s = (GroundStateStruct)data.dwData[0];
        var chocks = s.Chocks > 0.5;
        var gpu = s.Gpu > 0.5;
        if (_chocksSet == chocks && _gpuConnected == gpu) return;
        _chocksSet = chocks;
        _gpuConnected = gpu;
        NotifyGroundStateChanged();
    }

    public override Task OnBeforeDeboarding()
    {
        if (!canRemoveAndPlaceGroundEquipment || !manageGroundEquipment) return Task.CompletedTask;
        Logger.Info("Md11Adapter: Placing chocks and GPU");
        WriteSimVar(SimDef.Md11Chocks, 1.0);
        WriteSimVar(SimDef.Md11Gpu, 1.0);
        return Task.CompletedTask;
    }

    public override Task OnBeforePushback()
    {
        if (!canRemoveAndPlaceGroundEquipment || !manageGroundEquipment) return Task.CompletedTask;
        Logger.Info("Md11Adapter: Removing chocks and GPU");
        WriteSimVar(SimDef.Md11Gpu, 0.0);
        WriteSimVar(SimDef.Md11Chocks, 0.0);
        return Task.CompletedTask;
    }

    public override void Dispose()
    {
        _sc = null;
        Logger.Debug("Md11Adapter: disposed");
    }

    private void WriteSimVar(SimDef def, double value)
    {
        if (_sc == null) return;
        try
        {
            _sc.SetDataOnSimObject(def, SimConnect.SIMCONNECT_OBJECT_ID_USER,
                SIMCONNECT_DATA_SET_FLAG.DEFAULT, new ScalarStruct { Value = value });
        }
        catch (Exception ex)
        {
            Logger.Warning($"Md11Adapter: WriteSimVar({def}) = {value} failed: {ex.Message}");
        }
    }
}
