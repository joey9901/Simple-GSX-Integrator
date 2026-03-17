using Microsoft.FlightSimulator.SimConnect;
using SimpleGsxIntegrator.Core;
using System.Runtime.InteropServices;

namespace SimpleGsxIntegrator.Aircraft.A300;

public sealed class IniA300Adapter : IAircraftAdapter
{
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    private struct ScalarStruct { public double Value; }

    private SimConnect? _sc;

    public void OnSimConnectConnected(SimConnect sc)
    {
        _sc = sc;

        sc.AddToDataDefinition(SimDef.A300Chocks,
            A300Constants.LVar_Chocks, "Number", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
        sc.RegisterDataDefineStruct<ScalarStruct>(SimDef.A300Chocks);

        sc.AddToDataDefinition(SimDef.A300Gpu,
            A300Constants.LVar_Gpu, "Number", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
        sc.RegisterDataDefineStruct<ScalarStruct>(SimDef.A300Gpu);

        sc.AddToDataDefinition(SimDef.A300Covers,
            A300Constants.LVar_Covers, "Number", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
        sc.RegisterDataDefineStruct<ScalarStruct>(SimDef.A300Covers);

        sc.AddToDataDefinition(SimDef.A300CargoDoor,
            A300Constants.LVar_CargoDoor, "Number", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
        sc.RegisterDataDefineStruct<ScalarStruct>(SimDef.A300CargoDoor);
    }

    public void OnSimObjectData(SIMCONNECT_RECV_SIMOBJECT_DATA data) { }

    private void RemoveGroundEquipment()
    {
        if (_sc == null) return;
        Logger.Debug($"IniA300Adapter: removing chocks ({A300Constants.LVar_Chocks} = 0)");
        WriteSimVar(SimDef.A300Chocks, 0.0);
        Logger.Debug($"IniA300Adapter: removing GPU ({A300Constants.LVar_Gpu} = 0)");
        WriteSimVar(SimDef.A300Gpu, 0.0);
    }

    private Task PlaceGroundEquipmentAndChocks()
    {
        if (_sc == null) return Task.CompletedTask;
        Logger.Info("IniA300Adapter: Placing Chocks and GPU");
        Logger.Debug($"IniA300Adapter: placing chocks ({A300Constants.LVar_Chocks} = 1)");
        WriteSimVar(SimDef.A300Chocks, 1.0);
        Logger.Debug($"IniA300Adapter: placing GPU ({A300Constants.LVar_Gpu} = 1)");
        WriteSimVar(SimDef.A300Gpu, 1.0);
        return Task.CompletedTask;
    }

    public Task OnSpawned()
    {
        Logger.Debug($"IniA300Adapter: removing covers ({A300Constants.LVar_Covers} = 0)");
        WriteSimVar(SimDef.A300Covers, 0.0);
        return Task.CompletedTask;
    }

    public Task OnBoardingRequested() { return Task.CompletedTask; }

    public Task OnBoardingActive()
    {
        Logger.Debug($"IniA300Adapter: opening main cargo door ({A300Constants.LVar_CargoDoor} = 100)");
        WriteSimVar(SimDef.A300CargoDoor, 100.0);
        return Task.CompletedTask;
    }

    public Task OnBoardingCompleted()
    {
        // Logger.Debug($"IniA300Adapter: closing main cargo door ({A300Constants.LVar_CargoDoor} = 0)");
        // WriteSimVar(SimDef.A300CargoDoor, 0.0);
        return Task.CompletedTask;
    }

    public Task OnDeboardingRequested() { return Task.CompletedTask; }

    public Task OnDeboardingActive()
    {
        Logger.Debug($"IniA300Adapter: opening main cargo door ({A300Constants.LVar_CargoDoor} = 100)");
        WriteSimVar(SimDef.A300CargoDoor, 100.0);
        return Task.CompletedTask;
    }

    public Task OnDeboardingCompleted()
    {
        // Logger.Debug($"IniA300Adapter: closing main cargo door ({A300Constants.LVar_CargoDoor} = 0)");
        // WriteSimVar(SimDef.A300CargoDoor, 0.0);
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
        Logger.Debug("IniA300Adapter: disposed");
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
            Logger.Warning($"IniA300Adapter: WriteSimVar({def}) = {value} failed: {ex.Message}");
        }
    }
}
