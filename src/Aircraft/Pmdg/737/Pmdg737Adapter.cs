using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using Microsoft.FlightSimulator.SimConnect;
using SimpleGsxIntegrator.Core;

namespace SimpleGsxIntegrator.Aircraft.Pmdg;

public sealed class Pmdg737Adapter : IAircraftAdapter
{

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    private struct Pmdg737VarsStruct
    {
        // Order must match AddToDataDefinition calls exactly
        public double FwdLeftCabinDoor;       // L:FwdLeftCabinDoor
        public double AftLeftCabinDoor;       // L:AftLeftCabinDoor
        public double FwdRightCabinDoor;      // L:FwdRightCabinDoor
        public double AftRightCabinDoor;      // L:AftRightCabinDoor
        public double OverwingAftLeftExit;    // L:OverwingAftLeftEmerExit
        public double OverwingAftRightExit;   // L:OverwingAftRightEmerExit
        public double OverwingFwdLeftExit;    // L:OverwingFwdLeftEmerExit
        public double OverwingFwdRightExit;   // L:OverwingFwdRightEmerExit
        public double FwdLwrCargoDoor;        // L:FwdLwrCargoDoor
        public double AftLwrCargoDoor;        // L:AftLwrCargoDoor
        public double MainCargoDoor;          // L:MainCargoDoor
        public double EquipmentHatchDoor;     // L:EEDoor
        public double WheelChocks;            // L:NGXWheelChocks
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    private struct Pmdg737ControlStruct
    {
        public uint Event;
        public uint Parameter;
    }


    private SimConnect? _sc;
    private Pmdg737VarsStruct _vars;
    private readonly DoorStateTracker _doorTracker = new();

    private readonly ConcurrentDictionary<uint, DateTime> _lastSent = new();
    private static readonly TimeSpan DebounceWindow = TimeSpan.FromSeconds(4);


    public void OnSimConnectConnected(SimConnect sc)
    {
        _sc = sc;

        RegisterLVars(sc);
        RegisterControlChannel(sc);
        ScheduleInitialSnapshot();

        Logger.Debug("Pmdg737Adapter: connected");
    }

    private void RegisterLVars(SimConnect sc)
    {
        // Doors (order must match Pmdg737VarsStruct field order)
        AddLVar(sc, Pmdg737Constants.LVAR_DOOR_FWD_L);
        AddLVar(sc, Pmdg737Constants.LVAR_DOOR_AFT_L);
        AddLVar(sc, Pmdg737Constants.LVAR_DOOR_FWD_R);
        AddLVar(sc, Pmdg737Constants.LVAR_DOOR_AFT_R);
        AddLVar(sc, Pmdg737Constants.LVAR_OVERWING_AFT_L);
        AddLVar(sc, Pmdg737Constants.LVAR_OVERWING_AFT_R);
        AddLVar(sc, Pmdg737Constants.LVAR_OVERWING_FWD_L);
        AddLVar(sc, Pmdg737Constants.LVAR_OVERWING_FWD_R);
        AddLVar(sc, Pmdg737Constants.LVAR_CARGO_FWD);
        AddLVar(sc, Pmdg737Constants.LVAR_CARGO_AFT);
        AddLVar(sc, Pmdg737Constants.LVAR_CARGO_MAIN);
        AddLVar(sc, Pmdg737Constants.LVAR_EQUIPMENT_HATCH);
        AddLVar(sc, Pmdg737Constants.LVAR_WHEEL_CHOCKS);

        sc.RegisterDataDefineStruct<Pmdg737VarsStruct>(SimDef.Pmdg737Vars);
        Logger.Debug("Pmdg737Adapter: L:var definitions registered");
    }

    private void AddLVar(SimConnect sc, string lvar)
    {
        sc.AddToDataDefinition(SimDef.Pmdg737Vars, lvar, "Number",
            SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
    }

    private void RegisterControlChannel(SimConnect sc)
    {
        try
        {
            sc.MapClientDataNameToID(
                Pmdg737Constants.CLIENT_DATA_CONTROL_NAME,
                Pmdg737DataId.Control);

            uint size = (uint)Marshal.SizeOf<Pmdg737ControlStruct>();
            sc.AddToClientDataDefinition(SimDef.Pmdg737Control, 0, size, 0, 0);
            sc.RegisterDataDefineStruct<Pmdg737ControlStruct>(SimDef.Pmdg737Control);

            Logger.Debug("Pmdg737Adapter: PMDG control channel registered");
        }
        catch (Exception ex)
        {
            Logger.Warning($"Pmdg737Adapter: control channel registration failed: {ex.Message}");
        }
    }

    private void ScheduleInitialSnapshot()
    {
        Task.Run(async () =>
        {
            await Task.Delay(3000);
            RequestDataSnapshot();
        });
    }

    private void RequestDataSnapshot()
    {
        try
        {
            _sc?.RequestDataOnSimObject(
                SimReq.Pmdg737Vars,
                SimDef.Pmdg737Vars,
                SimConnect.SIMCONNECT_OBJECT_ID_USER,
                SIMCONNECT_PERIOD.SECOND,
                SIMCONNECT_DATA_REQUEST_FLAG.DEFAULT,
                0, 0, 0);
        }
        catch (Exception ex)
        {
            Logger.Warning($"Pmdg737Adapter: RequestDataSnapshot failed: {ex.Message}");
        }
    }


    public void OnSimObjectData(SIMCONNECT_RECV_SIMOBJECT_DATA data)
    {
        if (data.dwRequestID != (uint)SimReq.Pmdg737Vars &&
            data.dwDefineID != (uint)SimDef.Pmdg737Vars) return;

        _vars = (Pmdg737VarsStruct)data.dwData[0];
        UpdateDoorStates();
    }


    private async Task CloseAllOpenDoorsAsync()
    {
        var open = Pmdg737Constants.AllDoorIds.Where(_doorTracker.IsOpen).ToList();

        if (open.Count == 0)
        {
            Logger.Debug("Pmdg737Adapter: all doors already Closed");
            return;
        }

        Logger.Debug($"Pmdg737Adapter: Closing {open.Count} open door(s)");

        foreach (uint evtCode in open)
        {
            // Aft overwing exits have no direct SDK event — close via CDU sequence
            if (evtCode == Pmdg737Constants.EVT_DOOR_OVERWING_EXIT_L2)
            {
                SendPmdgEventNow(Pmdg737Constants.EVT_CDU_R_MENU, 1); await Task.Delay(300);
                SendPmdgEventNow(Pmdg737Constants.EVT_CDU_R_R5, 1); await Task.Delay(300);
                SendPmdgEventNow(Pmdg737Constants.EVT_CDU_R_L3, 1); await Task.Delay(300);
                SendPmdgEventNow(Pmdg737Constants.EVT_CDU_R_L4, 1); await Task.Delay(300);
            }
            else if (evtCode == Pmdg737Constants.EVT_DOOR_OVERWING_EXIT_R2)
            {
                SendPmdgEventNow(Pmdg737Constants.EVT_CDU_R_MENU, 1); await Task.Delay(300);
                SendPmdgEventNow(Pmdg737Constants.EVT_CDU_R_R5, 1); await Task.Delay(300);
                SendPmdgEventNow(Pmdg737Constants.EVT_CDU_R_L3, 1); await Task.Delay(300);
                SendPmdgEventNow(Pmdg737Constants.EVT_CDU_R_R4, 1); await Task.Delay(300);
            }
            else
            {
                SendPmdgEvent(evtCode, 1);
            }
            await Task.Delay(300);
        }
    }

    private void CloseDoor(uint doorId)
    {
        if (!_doorTracker.IsOpen(doorId))
        {
            Logger.Debug($"Pmdg737Adapter: {Pmdg737Constants.GetDoorName(doorId)} is already Closed");
            return;
        }

        Logger.Info($"Pmdg737Adapter: Closing {Pmdg737Constants.GetDoorName(doorId)}");
        SendPmdgEvent(doorId, 1);
    }


    private async Task PlaceGroundEquipmentAndChocks()
    {
        if (_vars.WheelChocks >= 0.5)
        {
            Logger.Debug("Pmdg737Adapter: Chocks already Set - skipping CDU Sequence");
            return;
        }

        Logger.Info("Pmdg737Adapter: Placing Chocks and GPU via CDU Sequence");

        SendPmdgEventNow(Pmdg737Constants.EVT_CDU_R_MENU, 1); await Task.Delay(500);
        SendPmdgEventNow(Pmdg737Constants.EVT_CDU_R_R5, 1); await Task.Delay(500);
        SendPmdgEventNow(Pmdg737Constants.EVT_CDU_R_R1, 1); await Task.Delay(500);
        SendPmdgEventNow(Pmdg737Constants.EVT_CDU_R_R6, 1); await Task.Delay(1000);
        SendPmdgEventNow(Pmdg737Constants.EVT_CDU_R_L2, 1);
    }

    private async Task RemoveGroundEquipmentAsync()
    {
        if (_vars.WheelChocks <= 0.5)
        {
            Logger.Debug("Pmdg737Adapter: Chocks already Removed - skipping CDU Sequence");
            return;
        }

        Logger.Debug("Pmdg737Adapter: Removing Chocks via CDU Sequence");

        SendPmdgEventNow(Pmdg737Constants.EVT_CDU_R_MENU, 1); await Task.Delay(500);
        SendPmdgEventNow(Pmdg737Constants.EVT_CDU_R_R5, 1); await Task.Delay(500);
        SendPmdgEventNow(Pmdg737Constants.EVT_CDU_R_R1, 1); await Task.Delay(500);
        SendPmdgEventNow(Pmdg737Constants.EVT_CDU_R_R6, 1); await Task.Delay(500);
    }

    public async Task OnBeforePushbackAsync()
    {
        await RemoveGroundEquipmentAsync();
        await CloseAllOpenDoorsAsync();

        var deadline = DateTime.UtcNow.AddSeconds(60);
        while (_doorTracker.IsAnyOpen(Pmdg737Constants.AllDoorIds) && DateTime.UtcNow < deadline)
        {
            await Task.Delay(5_000);
            await CloseAllOpenDoorsAsync();
        }

        if (_doorTracker.IsAnyOpen(Pmdg737Constants.AllDoorIds))
            Logger.Warning("Pmdg737Adapter: Doors still open after 60s - proceeding with pushback");
        else
            Logger.Info("Pmdg737Adapter: All doors confirmed closed");
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
        Logger.Debug("Pmdg737Adapter: disposed");
    }

    private double GetRawDoorValue(uint evtCode)
    {
        switch (evtCode)
        {
            case Pmdg737Constants.EVT_DOOR_FWD_L: return _vars.FwdLeftCabinDoor;
            case Pmdg737Constants.EVT_DOOR_AFT_L: return _vars.AftLeftCabinDoor;
            case Pmdg737Constants.EVT_DOOR_FWD_R: return _vars.FwdRightCabinDoor;
            case Pmdg737Constants.EVT_DOOR_AFT_R: return _vars.AftRightCabinDoor;
            case Pmdg737Constants.EVT_DOOR_OVERWING_EXIT_L2: return _vars.OverwingAftLeftExit;
            case Pmdg737Constants.EVT_DOOR_OVERWING_EXIT_R2: return _vars.OverwingAftRightExit;
            case Pmdg737Constants.EVT_DOOR_OVERWING_EXIT_L: return _vars.OverwingFwdLeftExit;
            case Pmdg737Constants.EVT_DOOR_OVERWING_EXIT_R: return _vars.OverwingFwdRightExit;
            case Pmdg737Constants.EVT_DOOR_CARGO_FWD: return _vars.FwdLwrCargoDoor;
            case Pmdg737Constants.EVT_DOOR_CARGO_AFT: return _vars.AftLwrCargoDoor;
            case Pmdg737Constants.EVT_DOOR_CARGO_MAIN: return _vars.MainCargoDoor;
            case Pmdg737Constants.EVT_DOOR_EQUIPMENT_HATCH: return _vars.EquipmentHatchDoor;
            default: return double.NaN;
        }
    }

    private void UpdateDoorStates()
    {
        foreach (uint evtCode in Pmdg737Constants.AllDoorIds)
            _doorTracker.Update(evtCode, GetRawDoorValue(evtCode), Pmdg737Constants.GetDoorName(evtCode));
    }

    private void SendPmdgEvent(uint evtCode, uint param)
    {
        if (_sc == null) return;

        var now = DateTime.UtcNow;
        var last = _lastSent.GetOrAdd(evtCode, DateTime.MinValue);

        if (now - last < DebounceWindow)
        {
            Logger.Debug($"Pmdg737Adapter: evt {evtCode} debounced ({(now - last).TotalSeconds:F1}s since last send)");
            return;
        }

        SendPmdgEventNow(evtCode, param);
    }

    private void SendPmdgEventNow(uint evtCode, uint param)
    {
        if (_sc == null) return;

        try
        {
            var cmd = new Pmdg737ControlStruct { Event = evtCode, Parameter = param };
            _sc.SetClientData(
                Pmdg737DataId.Control,
                SimDef.Pmdg737Control,
                SIMCONNECT_CLIENT_DATA_SET_FLAG.DEFAULT,
                0,
                cmd);

            _lastSent[evtCode] = DateTime.UtcNow;
            Logger.Debug($"Pmdg737Adapter: sent evt={evtCode} param={param}");
        }
        catch (Exception ex)
        {
            Logger.Warning($"Pmdg737Adapter: SendPmdgEventNow({evtCode}) failed: {ex.Message}");
        }
    }
}
