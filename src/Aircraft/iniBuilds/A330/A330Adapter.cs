using Microsoft.FlightSimulator.SimConnect;
using SimpleGsxIntegrator.Core;
using System.Runtime.InteropServices;

namespace SimpleGsxIntegrator.Aircraft.A330;

public sealed class IniA330Adapter : IAircraftAdapter
{
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    private struct ScalarStruct { public double Value; }

    private SimConnect? _sc;

    public void OnSimConnectConnected(SimConnect sc)
    {
        _sc = sc;

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
    }

    public void OnSimObjectData(SIMCONNECT_RECV_SIMOBJECT_DATA data) { }

    private void RemoveGroundEquipment()
    {
        if (_sc == null) return;
        Logger.Debug("IniA330Adapter: removing chocks (L:COVER ON:0 = 0)");
        WriteSimVar(SimDef.A330Chocks, 0.0);
        Logger.Debug("IniA330Adapter: removing GPU (L:INI_GPU_AVAIL = 0)");
        WriteSimVar(SimDef.A330Gpu, 0.0);
    }

    private Task PlaceGroundEquipmentAndChocks()
    {
        if (_sc == null) return Task.CompletedTask;
        Logger.Info("IniA330Adapter: Placing Chocks and GPU");
        Logger.Debug("IniA330Adapter: placing chocks (COVER ON:0 = 1)");
        WriteSimVar(SimDef.A330Chocks, 1.0);
        Logger.Debug("IniA330Adapter: placing GPU (L:INI_GPU_AVAIL = 1)");
        WriteSimVar(SimDef.A330Gpu, 1.0);
        return Task.CompletedTask;
    }

    public Task OnSpawned()
    {
        Logger.Debug("IniA330Adapter: removing engine covers (COVER ON:1 = 0)");
        WriteSimVar(SimDef.A330EngineCover, 0.0);
        Logger.Debug("IniA330Adapter: removing pitot covers (COVER ON:2 = 0)");
        WriteSimVar(SimDef.A330PitotCover, 0.0);
        return Task.CompletedTask;
    }

    public Task OnBeforePushbackAsync()
    {
        RemoveGroundEquipment();
        return Task.CompletedTask;
    }

    public Task OnBeforeDeboardingAsync()
    {
        return PlaceGroundEquipmentAndChocks();
    }

    public void Dispose()
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
