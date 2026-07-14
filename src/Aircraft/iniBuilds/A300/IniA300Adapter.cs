using Microsoft.FlightSimulator.SimConnect;
using SimpleGsxIntegrator.Core;
using System.Runtime.InteropServices;

namespace SimpleGsxIntegrator.Aircraft.A300;

public sealed class IniA300Adapter : AircraftAdapterBase
{
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    private struct ScalarStruct { public double Value; }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    private struct GroundStateStruct
    {
        public double Chocks;    // L:INI_CHOCKS_ENABLED — 1 = set, 0 = removed
        public double Gpu;       // L:INI_gpu_avail — 1 = connected, 0 = removed
        public double CargoDoor; // L:INI_MAIN_CARGO_DOOR_TGT — 100 = open, 0 = closed
    }

    public override string DisplayName => "iniBuilds A300";
    public override string[] TitleKeywords => ["iniBuilds", "A300"];
    public override bool canRemoveAndPlaceGroundEquipment => true;
    public override bool canRemoveCovers => true;
    public override bool canManageDoors => true;

    private bool? _chocksSet;
    private bool? _gpuConnected;
    private bool? _cargoDoorOpen;
    public override bool? ChocksSet => _chocksSet;
    public override bool? GpuConnected => _gpuConnected;
    public override int? OpenDoorCount => _cargoDoorOpen.HasValue ? (_cargoDoorOpen.Value ? 1 : 0) : null;

    private SimConnect? _sc;

    public override void OnSimConnectConnected(SimConnect sc)
    {
        _sc = sc;

        // Write-only SimDefs (SetDataOnSimObject)
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

        sc.AddToDataDefinition(SimDef.A300GroundState,
            A300Constants.LVar_Chocks, "Number", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
        sc.AddToDataDefinition(SimDef.A300GroundState,
            A300Constants.LVar_Gpu, "Number", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
        sc.AddToDataDefinition(SimDef.A300GroundState,
            A300Constants.LVar_CargoDoor, "Number", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
        sc.RegisterDataDefineStruct<GroundStateStruct>(SimDef.A300GroundState);
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
        var s = (GroundStateStruct)data.dwData[0];
        var chocks = s.Chocks > 0.5;
        var gpu = s.Gpu > 0.5;
        var door = s.CargoDoor > 50.0;
        if (_chocksSet == chocks && _gpuConnected == gpu && _cargoDoorOpen == door) return;
        _chocksSet = chocks;
        _gpuConnected = gpu;
        _cargoDoorOpen = door;
        NotifyGroundStateChanged();
    }

    private void RemoveGroundEquipment()
    {
        if (_sc == null || !manageGroundEquipment) return;
        Logger.Debug($"IniA300Adapter: removing chocks ({A300Constants.LVar_Chocks} = 0)");
        WriteSimVar(SimDef.A300Chocks, 0.0);
        Logger.Debug($"IniA300Adapter: removing GPU ({A300Constants.LVar_Gpu} = 0)");
        WriteSimVar(SimDef.A300Gpu, 0.0);
    }

    private Task PlaceGroundEquipment()
    {
        if (_sc == null || !manageGroundEquipment) return Task.CompletedTask;
        Logger.Info("IniA300Adapter: Placing Chocks and GPU");
        WriteSimVar(SimDef.A300Chocks, 1.0);
        WriteSimVar(SimDef.A300Gpu, 1.0);
        return Task.CompletedTask;
    }

    public override Task OnSpawned()
    {
        if (canRemoveCovers && removeCovers)
        {
            Logger.Debug($"IniA300Adapter: removing covers ({A300Constants.LVar_Covers} = 0)");
            WriteSimVar(SimDef.A300Covers, 0.0);
        }
        return Task.CompletedTask;
    }

    public override Task OnBoardingRequested() { return Task.CompletedTask; }

    public override Task OnBoardingActive()
    {
        Logger.Debug($"IniA300Adapter: opening main cargo door ({A300Constants.LVar_CargoDoor} = 100)");
        WriteSimVar(SimDef.A300CargoDoor, 100.0);
        return Task.CompletedTask;
    }

    public override Task OnBoardingCompleted()
    {
        Logger.Debug($"IniA300Adapter: closing main cargo door ({A300Constants.LVar_CargoDoor} = 0)");
        WriteSimVar(SimDef.A300CargoDoor, 0.0);
        return Task.CompletedTask;
    }

    public override Task OnDeboardingRequested() { return Task.CompletedTask; }

    public override Task OnDeboardingActive()
    {
        Logger.Debug($"IniA300Adapter: opening main cargo door ({A300Constants.LVar_CargoDoor} = 100)");
        WriteSimVar(SimDef.A300CargoDoor, 100.0);
        return Task.CompletedTask;
    }

    public override Task OnDeboardingCompleted()
    {
        Logger.Debug($"IniA300Adapter: closing main cargo door ({A300Constants.LVar_CargoDoor} = 0)");
        WriteSimVar(SimDef.A300CargoDoor, 0.0);
        return Task.CompletedTask;
    }

    public override Task OnBeforePushback()
    {
        if (canRemoveAndPlaceGroundEquipment && manageGroundEquipment) RemoveGroundEquipment();
        return Task.CompletedTask;
    }

    public override Task OnBeforeDeboarding()
    {
        if (!canRemoveAndPlaceGroundEquipment || !manageGroundEquipment) return Task.CompletedTask;
        return PlaceGroundEquipment();
    }

    public override void Dispose()
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
