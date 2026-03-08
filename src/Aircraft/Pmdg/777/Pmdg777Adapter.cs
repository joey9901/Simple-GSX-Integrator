using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using Microsoft.FlightSimulator.SimConnect;
using SimpleGsxIntegrator.Core;

namespace SimpleGsxIntegrator.Aircraft.Pmdg;

public sealed class Pmdg777Adapter : IAircraftAdapter
{

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    private struct Pmdg777VarsStruct
    {
        // Doors – order must match the AddToDataDefinition calls below
        public double Door1L, Door1R, Door2L, Door2R, Door3L, Door3R;
        public double Door4L, Door4R, Door5L, Door5R;
        public double CargoDoorFwd, CargoDoorAft, CargoDoorMain, CargoDoorBulk;
        public double AvionicsDoor, EEHatch;

        // Ground equipment
        public double WheelChocks;
        public double ExtPwrSec;     // L:switch_07_b  (secondary GPU)
        public double ExtPwrPrim;    // L:switch_08_b  (primary GPU)
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    private struct Pmdg777ControlStruct
    {
        public uint Event;
        public uint Parameter;
    }


    private SimConnect? _sc;
    private Pmdg777VarsStruct _vars;
    private readonly DoorStateTracker _doorTracker = new();

    private readonly ConcurrentDictionary<uint, DateTime> _lastSent = new();
    private static readonly TimeSpan DebounceWindow = TimeSpan.FromSeconds(4);

    public void OnSimConnectConnected(SimConnect sc)
    {
        _sc = sc;

        RegisterLVars(sc);
        RegisterControlChannel(sc);
        ScheduleInitialSnapshot();

        Logger.Debug("Pmdg777Adapter: connected");
    }

    private void RegisterLVars(SimConnect sc)
    {
        // Doors (order must match Pmdg777VarsStruct field order)
        AddLVar(sc, Pmdg777Constants.LVAR_DOOR_1L);
        AddLVar(sc, Pmdg777Constants.LVAR_DOOR_1R);
        AddLVar(sc, Pmdg777Constants.LVAR_DOOR_2L);
        AddLVar(sc, Pmdg777Constants.LVAR_DOOR_2R);
        AddLVar(sc, Pmdg777Constants.LVAR_DOOR_3L);
        AddLVar(sc, Pmdg777Constants.LVAR_DOOR_3R);
        AddLVar(sc, Pmdg777Constants.LVAR_DOOR_4L);
        AddLVar(sc, Pmdg777Constants.LVAR_DOOR_4R);
        AddLVar(sc, Pmdg777Constants.LVAR_DOOR_5L);
        AddLVar(sc, Pmdg777Constants.LVAR_DOOR_5R);
        AddLVar(sc, Pmdg777Constants.LVAR_CARGO_FWD);
        AddLVar(sc, Pmdg777Constants.LVAR_CARGO_AFT);
        AddLVar(sc, Pmdg777Constants.LVAR_CARGO_MAIN);
        AddLVar(sc, Pmdg777Constants.LVAR_CARGO_BULK);
        AddLVar(sc, Pmdg777Constants.LVAR_AVIONICS);
        AddLVar(sc, Pmdg777Constants.LVAR_EE_HATCH);

        // Ground equipment
        AddLVar(sc, Pmdg777Constants.LVAR_WHEEL_CHOCKS);
        AddLVar(sc, Pmdg777Constants.LVAR_EXT_PWR_SEC);
        AddLVar(sc, Pmdg777Constants.LVAR_EXT_PWR_PRIM);

        sc.RegisterDataDefineStruct<Pmdg777VarsStruct>(SimDef.Pmdg777Vars);
        Logger.Debug("Pmdg777Adapter: L:var definitions registered");
    }

    private void AddLVar(SimConnect sc, string lvar)
    {
        sc.AddToDataDefinition(SimDef.Pmdg777Vars, lvar, "Number",
            SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
    }

    private void RegisterControlChannel(SimConnect sc)
    {
        try
        {
            sc.MapClientDataNameToID(
                Pmdg777Constants.CLIENT_DATA_CONTROL_NAME,
                Pmdg777DataId.Control);

            uint size = (uint)Marshal.SizeOf<Pmdg777ControlStruct>();
            sc.AddToClientDataDefinition(SimDef.Pmdg777Control, 0, size, 0, 0);
            sc.RegisterDataDefineStruct<Pmdg777ControlStruct>(SimDef.Pmdg777Control);

            Logger.Debug("Pmdg777Adapter: PMDG control channel registered");
        }
        catch (Exception ex)
        {
            Logger.Warning($"Pmdg777Adapter: control channel registration failed: {ex.Message}");
        }
    }

    private void ScheduleInitialSnapshot()
    {
        Task.Run(async () =>
        {
            await Task.Delay(3000); // Give PMDG time to initialise
            RequestDataSnapshot();
        });
    }

    private void RequestDataSnapshot()
    {
        try
        {
            _sc?.RequestDataOnSimObject(
                SimReq.Pmdg777Vars,
                SimDef.Pmdg777Vars,
                SimConnect.SIMCONNECT_OBJECT_ID_USER,
                SIMCONNECT_PERIOD.SECOND,
                SIMCONNECT_DATA_REQUEST_FLAG.DEFAULT,
                0, 0, 0);
        }
        catch (Exception ex)
        {
            Logger.Warning($"Pmdg777Adapter: RequestDataSnapshot failed: {ex.Message}");
        }
    }

    public void OnSimObjectData(SIMCONNECT_RECV_SIMOBJECT_DATA data)
    {
        if (data.dwRequestID != (uint)SimReq.Pmdg777Vars &&
            data.dwDefineID != (uint)SimDef.Pmdg777Vars) return;

        _vars = (Pmdg777VarsStruct)data.dwData[0];
        UpdateDoorStates();
    }

    private async Task CloseAllOpenDoorsAsync()
    {
        var open = Pmdg777Constants.AllDoorIds.Where(_doorTracker.IsOpen).ToList();

        if (open.Count == 0) return;

        Logger.Debug($"Pmdg777Adapter: Closing {open.Count} open door(s)");

        foreach (uint evtCode in open)
        {
            SendPmdgEvent(evtCode, 1);
            await Task.Delay(300);
        }
    }

    private void CloseDoor(uint doorId)
    {
        if (!_doorTracker.IsOpen(doorId))
        {
            Logger.Debug($"Pmdg777Adapter: {Pmdg777Constants.GetDoorName(doorId)} is already Closed");
            return;
        }

        Logger.Info($"Pmdg777Adapter: Closing {Pmdg777Constants.GetDoorName(doorId)}");
        SendPmdgEvent(doorId, 1);
    }

    private async Task PlaceGroundEquipmentAndChocks()
    {
        if (_vars.WheelChocks >= 0.5)
        {
            Logger.Debug("Pmdg777Adapter: Chocks already Set - skipping CDU Sequence");
            return;
        }

        Logger.Info("Pmdg777Adapter: Placing Chocks and GPU via CDU Sequence");

        SendPmdgEventNow(Pmdg777Constants.EVT_CDU_C_MENU, 1); await Task.Delay(500);
        SendPmdgEventNow(Pmdg777Constants.EVT_CDU_C_R6, 1); await Task.Delay(500);
        SendPmdgEventNow(Pmdg777Constants.EVT_CDU_C_R1, 1); await Task.Delay(500);
        SendPmdgEventNow(Pmdg777Constants.EVT_CDU_C_R6, 1); await Task.Delay(500);
        SendPmdgEventNow(Pmdg777Constants.EVT_CDU_C_L2, 1);
    }

    private async Task RemoveGroundEquipmentAsync()
    {
        if (_vars.WheelChocks <= 0.5)
        {
            Logger.Debug("Pmdg777Adapter: Chocks already Removed - skipping CDU Sequence");
            return;
        }

        // This presses the OVHD GPU buttons to turn OFF GPU (NOT DISCONNECT)
        if (_vars.ExtPwrSec > 0.5) SendPmdgEvent(Pmdg777Constants.EVT_OH_ELEC_GRD_PWR_SEC, 1);
        if (_vars.ExtPwrPrim > 0.5) SendPmdgEvent(Pmdg777Constants.EVT_OH_ELEC_GRD_PWR_PRIM, 1);

        Logger.Debug("Pmdg777Adapter: Removing Chocks via CDU Sequence");

        // CDU Sequence to remove chocks AND GPU
        SendPmdgEventNow(Pmdg777Constants.EVT_CDU_C_MENU, 1); await Task.Delay(500);
        SendPmdgEventNow(Pmdg777Constants.EVT_CDU_C_R6, 1); await Task.Delay(500);
        SendPmdgEventNow(Pmdg777Constants.EVT_CDU_C_R1, 1); await Task.Delay(500);
        SendPmdgEventNow(Pmdg777Constants.EVT_CDU_C_R6, 1);
    }

    public async Task OnBeforePushbackAsync()
    {
        Logger.Info("Pmdg777Adapter: Removing Ground Equipment and Closing Doors.");
        await RemoveGroundEquipmentAsync();
        await CloseAllOpenDoorsAsync();

        var deadline = DateTime.UtcNow.AddSeconds(60);
        while (_doorTracker.IsAnyOpen(Pmdg777Constants.AllDoorIds) && DateTime.UtcNow < deadline)
        {
            // Wait longer than the debounce window so the next CloseAllOpenDoorsAsync
            // call is actually sent (and any still-opening door has finished animating).
            await Task.Delay(5_000);
            await CloseAllOpenDoorsAsync();
        }

        if (_doorTracker.IsAnyOpen(Pmdg777Constants.AllDoorIds))
            Logger.Warning("Pmdg777Adapter: Doors still open after 60s - proceeding with pushback");
        else
            Logger.Info("Pmdg777Adapter: All doors confirmed closed");
    }

    public Task OnBeforeDeboardingAsync()
    {
        return PlaceGroundEquipmentAndChocks();
    }

    public async Task OnBoardingCompleted()
    {
        await Task.Delay(15_000);
        await CloseAllOpenDoorsAsync();
    }

    public void Dispose()
    {
        _doorTracker.Reset();
        _sc = null;
        Logger.Debug("Pmdg777Adapter: disposed");
    }

    private double GetRawDoorValue(uint evtCode)
    {
        switch (evtCode)
        {
            case Pmdg777Constants.EVT_DOOR_1L: return _vars.Door1L;
            case Pmdg777Constants.EVT_DOOR_1R: return _vars.Door1R;
            case Pmdg777Constants.EVT_DOOR_2L: return _vars.Door2L;
            case Pmdg777Constants.EVT_DOOR_2R: return _vars.Door2R;
            case Pmdg777Constants.EVT_DOOR_3L: return _vars.Door3L;
            case Pmdg777Constants.EVT_DOOR_3R: return _vars.Door3R;
            case Pmdg777Constants.EVT_DOOR_4L: return _vars.Door4L;
            case Pmdg777Constants.EVT_DOOR_4R: return _vars.Door4R;
            case Pmdg777Constants.EVT_DOOR_5L: return _vars.Door5L;
            case Pmdg777Constants.EVT_DOOR_5R: return _vars.Door5R;
            case Pmdg777Constants.EVT_DOOR_CARGO_FWD: return _vars.CargoDoorFwd;
            case Pmdg777Constants.EVT_DOOR_CARGO_AFT: return _vars.CargoDoorAft;
            case Pmdg777Constants.EVT_DOOR_CARGO_MAIN: return _vars.CargoDoorMain;
            case Pmdg777Constants.EVT_DOOR_CARGO_BULK: return _vars.CargoDoorBulk;
            case Pmdg777Constants.EVT_DOOR_AVIONICS: return _vars.AvionicsDoor;
            case Pmdg777Constants.EVT_DOOR_EE_HATCH: return _vars.EEHatch;
            default: return double.NaN;
        }
    }

    private void UpdateDoorStates()
    {
        foreach (uint evtCode in Pmdg777Constants.AllDoorIds)
            _doorTracker.Update(evtCode, GetRawDoorValue(evtCode), Pmdg777Constants.GetDoorName(evtCode));
    }

    private void SendPmdgEvent(uint evtCode, uint param)
    {
        if (_sc == null) return;

        var now = DateTime.UtcNow;
        var last = _lastSent.GetOrAdd(evtCode, DateTime.MinValue);

        if (now - last < DebounceWindow)
        {
            Logger.Debug($"Pmdg777Adapter: evt {evtCode} debounced ({(now - last).TotalSeconds:F1}s since last send)");
            return;
        }

        SendPmdgEventNow(evtCode, param);
    }

    private void SendPmdgEventNow(uint evtCode, uint param)
    {
        if (_sc == null) return;

        try
        {
            var cmd = new Pmdg777ControlStruct { Event = evtCode, Parameter = param };
            _sc.SetClientData(
                Pmdg777DataId.Control,
                SimDef.Pmdg777Control,
                SIMCONNECT_CLIENT_DATA_SET_FLAG.DEFAULT,
                0,
                cmd);

            _lastSent[evtCode] = DateTime.UtcNow;
            Logger.Debug($"Pmdg777Adapter: sent evt={evtCode} param={param}");
        }
        catch (Exception ex)
        {
            Logger.Warning($"Pmdg777Adapter: SendPmdgEventNow({evtCode}) failed: {ex.Message}");
        }
    }
}
