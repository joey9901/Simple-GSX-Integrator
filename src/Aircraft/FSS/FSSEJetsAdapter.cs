using Microsoft.FlightSimulator.SimConnect;
using SimpleGsxIntegrator.Core;
using System.Runtime.InteropServices;

namespace SimpleGsxIntegrator.Aircraft.FSS;

internal sealed class FSSEJetsAdapter : AircraftAdapterBase
{
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    private struct ScalarStruct { public double Value; }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    private struct GroundStateStruct
    {
        public double GpuState;   // L:FSS_EXX_EXT_GPU_STATE
        public double ChockF;     // L:FSS_EXX_GNDOBJ_WHEEL_CHOKE_F
        public double ChockL;     // L:FSS_EXX_GNDOBJ_WHEEL_CHOKE_L
        public double ChockR;     // L:FSS_EXX_GNDOBJ_WHEEL_CHOKE_R
    }

    private static class GpuState
    {
        public const double ObjectHidden = -1;
        public const double Inactive     =  0;
        public const double Requested    =  1;
        public const double Available    =  2;
        public const double Startup      =  3;
        public const double Stopping     =  4;
        public const double Running      =  5;

        public static bool IsOn(double s)   => s >= Available;
        public static bool IsBusy(double s) => s is Requested or Startup or Stopping;
    }

    public override string DisplayName => "FSS E-Jets Series";
    public override string[] TitleKeywords => ["FSS"];
    public override bool canRemoveAndPlaceGroundEquipment => true;

    private bool? _gpuConnected;
    private bool? _chocksSet;
    public override bool? GpuConnected => _gpuConnected;
    public override bool? ChocksSet => _chocksSet;

    private double _gpuState = GpuState.ObjectHidden;
    private SimConnect? _sc;

    public override void OnSimConnectConnected(SimConnect sc)
    {
        _sc = sc;

        // Write-only: GPU toggle — WASM watches for 0→1 edge, fires toggle, resets to 0
        RegisterWriteLVar(sc, SimDef.FSSEJetsToggleGpu, "L:FSS_EXX_TOGGLE_CGPU");

        // Write-only: individual chock positions (direct 0/1)
        RegisterWriteLVar(sc, SimDef.FSSEJetsChockF, "L:FSS_EXX_GNDOBJ_WHEEL_CHOKE_F");
        RegisterWriteLVar(sc, SimDef.FSSEJetsChockL, "L:FSS_EXX_GNDOBJ_WHEEL_CHOKE_L");
        RegisterWriteLVar(sc, SimDef.FSSEJetsChockR, "L:FSS_EXX_GNDOBJ_WHEEL_CHOKE_R");

        // Read: GPU + all chock positions, polled every second
        sc.AddToDataDefinition(SimDef.FSSEJetsGroundState, "L:FSS_EXX_EXT_GPU_STATE",          null, SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
        sc.AddToDataDefinition(SimDef.FSSEJetsGroundState, "L:FSS_EXX_GNDOBJ_WHEEL_CHOKE_F",   null, SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
        sc.AddToDataDefinition(SimDef.FSSEJetsGroundState, "L:FSS_EXX_GNDOBJ_WHEEL_CHOKE_L",   null, SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
        sc.AddToDataDefinition(SimDef.FSSEJetsGroundState, "L:FSS_EXX_GNDOBJ_WHEEL_CHOKE_R",   null, SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
        sc.RegisterDataDefineStruct<GroundStateStruct>(SimDef.FSSEJetsGroundState);
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
        var s = (GroundStateStruct)data.dwData[0];

        _gpuState = s.GpuState;
        var gpuConnected = GpuState.IsOn(s.GpuState);
        var chocksSet = s.ChockF > 0.5 && s.ChockL > 0.5 && s.ChockR > 0.5;

        if (_gpuConnected == gpuConnected && _chocksSet == chocksSet) return;
        _gpuConnected = gpuConnected;
        _chocksSet = chocksSet;
        NotifyGroundStateChanged();
    }

    public override Task OnBeforeDeboarding()
    {
        if (!manageGroundEquipment) return Task.CompletedTask;
        SetGpu(true);
        SetChocks(true);
        return Task.CompletedTask;
    }

    public override Task OnBeforePushback()
    {
        if (!manageGroundEquipment) return Task.CompletedTask;
        SetGpu(false);
        SetChocks(false);
        return Task.CompletedTask;
    }

    public override void Dispose()
    {
        _sc = null;
        Logger.Debug("FSSEJetsAdapter: disposed");
    }

    private void SetGpu(bool enable)
    {
        if (_sc == null) return;
        if (GpuState.IsOn(_gpuState) == enable) return;
        if (GpuState.IsBusy(_gpuState)) return;
        WriteSimVar(SimDef.FSSEJetsToggleGpu, 1.0);
        Logger.Debug($"FSSEJetsAdapter: GPU → {(enable ? "ON" : "OFF")} (was state {_gpuState})");
    }

    private void SetChocks(bool place)
    {
        if (_sc == null) return;
        double val = place ? 1.0 : 0.0;
        WriteSimVar(SimDef.FSSEJetsChockF, val);
        WriteSimVar(SimDef.FSSEJetsChockL, val);
        WriteSimVar(SimDef.FSSEJetsChockR, val);
        Logger.Debug($"FSSEJetsAdapter: Chocks → {(place ? "SET" : "REMOVED")}");
    }

    private void RegisterWriteLVar(SimConnect sc, SimDef def, string lvar)
    {
        sc.AddToDataDefinition(def, lvar, null, SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
        sc.RegisterDataDefineStruct<ScalarStruct>(def);
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
            Logger.Warning($"FSSEJetsAdapter: Write({def}) = {value} failed: {ex.Message}");
        }
    }
}
