using Microsoft.FlightSimulator.SimConnect;
using SimpleGsxIntegrator.Core;
using System.Runtime.InteropServices;

namespace SimpleGsxIntegrator.Aircraft.A330;

public sealed class IniA330Adapter : AircraftAdapterBase
{
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    private struct ScalarStruct { public double Value; }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    private struct GroundStateStruct
    {
        public double Chocks;  // A:COVER ON:0  — 1 = set, 0 = removed
        public double Gpu;     // L:INI_GPU_AVAIL — 1 = connected, 0 = removed
    }

    public override string DisplayName => "Microsoft/iniBuilds A330";
    public override string[] TitleKeywords => ["A330"];
    public override bool canRemoveAndPlaceGroundEquipment => true;
    public override bool canRemoveCovers => true;

    private bool? _chocksSet;
    private bool? _gpuConnected;
    public override bool? ChocksSet => _chocksSet;
    public override bool? GpuConnected => _gpuConnected;

    private SimConnect? _sc;

    public override void OnSimConnectConnected(SimConnect sc)
    {
        _sc = sc;

        // Write-only SimDefs (SetDataOnSimObject)
        sc.AddToDataDefinition(SimDef.A330Chocks,
            A330Constants.AVar_Chocks, "Number", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
        sc.RegisterDataDefineStruct<ScalarStruct>(SimDef.A330Chocks);

        sc.AddToDataDefinition(SimDef.A330Gpu,
            A330Constants.LVar_Gpu, "Number", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
        sc.RegisterDataDefineStruct<ScalarStruct>(SimDef.A330Gpu);

        sc.AddToDataDefinition(SimDef.A330EngineCover,
            A330Constants.AVar_EngineCover, "Number", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
        sc.RegisterDataDefineStruct<ScalarStruct>(SimDef.A330EngineCover);

        sc.AddToDataDefinition(SimDef.A330PitotCover,
            A330Constants.AVar_PitotCover, "Number", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
        sc.RegisterDataDefineStruct<ScalarStruct>(SimDef.A330PitotCover);

        // Combined read struct — polled every second
        sc.AddToDataDefinition(SimDef.A330GroundState,
            A330Constants.AVar_Chocks, "Number", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
        sc.AddToDataDefinition(SimDef.A330GroundState,
            A330Constants.LVar_Gpu, "Number", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
        sc.RegisterDataDefineStruct<GroundStateStruct>(SimDef.A330GroundState);
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
        var s = (GroundStateStruct)data.dwData[0];
        var chocks = s.Chocks > 0.5;
        var gpu = s.Gpu > 0.5;
        if (_chocksSet == chocks && _gpuConnected == gpu) return;
        _chocksSet = chocks;
        _gpuConnected = gpu;
        NotifyGroundStateChanged();
    }

    private void RemoveGroundEquipment()
    {
        if (_sc == null || !manageGroundEquipment) return;
        Logger.Debug("IniA330Adapter: removing chocks (COVER ON:0 = 0)");
        WriteSimVar(SimDef.A330Chocks, 0.0);
        Logger.Debug("IniA330Adapter: removing GPU (L:INI_GPU_AVAIL = 0)");
        WriteSimVar(SimDef.A330Gpu, 0.0);
    }

    private Task PlaceGroundEquipment()
    {
        if (_sc == null || !manageGroundEquipment) return Task.CompletedTask;
        Logger.Info("IniA330Adapter: Placing Chocks and GPU");
        WriteSimVar(SimDef.A330Chocks, 1.0);
        WriteSimVar(SimDef.A330Gpu, 1.0);
        return Task.CompletedTask;
    }

    public override Task OnSpawned()
    {
        if (!removeCovers) return Task.CompletedTask;
        Logger.Debug("IniA330Adapter: removing engine covers (COVER ON:1 = 0)");
        WriteSimVar(SimDef.A330EngineCover, 0.0);
        Logger.Debug("IniA330Adapter: removing pitot covers (COVER ON:2 = 0)");
        WriteSimVar(SimDef.A330PitotCover, 0.0);
        return Task.CompletedTask;
    }

    public override Task OnBeforePushback()
    {
        if (!manageGroundEquipment) return Task.CompletedTask;
        RemoveGroundEquipment();
        return Task.CompletedTask;
    }

    public override Task OnBeforeDeboarding()
    {
        if (!manageGroundEquipment) return Task.CompletedTask;
        return PlaceGroundEquipment();
    }

    public override void Dispose()
    {
        _sc = null;
        Logger.Debug("IniA330Adapter: disposed");
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
            Logger.Warning($"IniA330Adapter: WriteSimVar({def}) = {value} failed: {ex.Message}");
        }
    }
}
