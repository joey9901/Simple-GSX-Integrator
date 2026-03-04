using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using Microsoft.FlightSimulator.SimConnect;
using SimpleGsxIntegrator.Core;

namespace SimpleGsxIntegrator.Aircraft.Pmdg;

/// <summary>
/// PMDG 777 aircraft adapter.
///
/// Responsibilities:
///   • Registers all required PMDG 777 L:vars and the PMDG control client-data area
///     with SimConnect (via the instance received in <see cref="OnSimConnectConnected"/>).
///   • Reads door, chock and ground-power state from L:vars.
///   • Sends PMDG event commands via the PMDG_777X_Control client-data channel.
///   • Removes ground equipment (GPU and chocks) via the appropriate events.
///
/// No SimConnect connection or message-pump logic lives here – that belongs to
/// the <see cref="SimConnectHub"/>.
/// </summary>
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

    /// <summary>
    /// Per-event debounce: we track last-sent time so we do not spam the same
    /// PMDG event multiple times within a short window.
    /// </summary>
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
        void Add(string lvar)
            => sc.AddToDataDefinition(SimDef.Pmdg777Vars, lvar, "Number",
                SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);

        // Doors (order must match Pmdg777VarsStruct field order)
        Add(Pmdg777Constants.LVAR_DOOR_1L);
        Add(Pmdg777Constants.LVAR_DOOR_1R);
        Add(Pmdg777Constants.LVAR_DOOR_2L);
        Add(Pmdg777Constants.LVAR_DOOR_2R);
        Add(Pmdg777Constants.LVAR_DOOR_3L);
        Add(Pmdg777Constants.LVAR_DOOR_3R);
        Add(Pmdg777Constants.LVAR_DOOR_4L);
        Add(Pmdg777Constants.LVAR_DOOR_4R);
        Add(Pmdg777Constants.LVAR_DOOR_5L);
        Add(Pmdg777Constants.LVAR_DOOR_5R);
        Add(Pmdg777Constants.LVAR_CARGO_FWD);
        Add(Pmdg777Constants.LVAR_CARGO_AFT);
        Add(Pmdg777Constants.LVAR_CARGO_MAIN);
        Add(Pmdg777Constants.LVAR_CARGO_BULK);
        Add(Pmdg777Constants.LVAR_AVIONICS);
        Add(Pmdg777Constants.LVAR_EE_HATCH);

        // Ground equipment
        Add(Pmdg777Constants.LVAR_WHEEL_CHOCKS);
        Add(Pmdg777Constants.LVAR_EXT_PWR_SEC);
        Add(Pmdg777Constants.LVAR_EXT_PWR_PRIM);

        sc.RegisterDataDefineStruct<Pmdg777VarsStruct>(SimDef.Pmdg777Vars);
        Logger.Debug("Pmdg777Adapter: L:var definitions registered");
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

        try
        {
            _vars = (Pmdg777VarsStruct)data.dwData[0];
            UpdateDoorStates();
        }
        catch (Exception ex)
        {
            Logger.Warning($"Pmdg777Adapter: data parse failed: {ex.Message}");
        }
    }


    public bool AreAnyDoorsOpen()
        => _doorTracker.IsAnyOpen(Pmdg777Constants.AllDoorIds);

    public IReadOnlySet<uint> GetOpenDoorIds()
        => _doorTracker.GetOpenIds(Pmdg777Constants.AllDoorIds);

    public async Task CloseAllOpenDoorsAsync()
    {
        var open = Pmdg777Constants.AllDoorIds.Where(_doorTracker.IsOpen).ToList();

        if (open.Count == 0) return;

        Logger.Info($"Pmdg777Adapter: Closing {open.Count} open door(s)");

        foreach (uint evtCode in open)
        {
            SendPmdgEvent(evtCode, 1);
            await Task.Delay(300);
        }
    }

    public void CloseDoor(uint doorId)
    {
        if (!_doorTracker.IsOpen(doorId))
        {
            Logger.Debug($"Pmdg777Adapter: {Pmdg777Constants.GetDoorName(doorId)} is already Closed");
            return;
        }

        Logger.Info($"Pmdg777Adapter: Closing {Pmdg777Constants.GetDoorName(doorId)}");
        SendPmdgEvent(doorId, 1);
    }


    public void RemoveGroundEquipment()
    {
        _ = RemoveGroundEquipmentAsync();
    }

    public async Task PlaceGroundEquipmentAndChocks()
    {
        try
        {
            if (_vars.WheelChocks >= 0.5)
            {
                Logger.Debug("Pmdg777Adapter: Chocks not set - skipping CDU Sequence");
                return;
            }

            Logger.Info("Pmdg777Adapter: Placing Chocks and GPU via CDU Sequence");

            SendPmdgEventNow(Pmdg777Constants.EVT_CDU_R_MENU, 1); await Task.Delay(500);
            SendPmdgEventNow(Pmdg777Constants.EVT_CDU_R_R6, 1); await Task.Delay(500);
            SendPmdgEventNow(Pmdg777Constants.EVT_CDU_R_R1, 1); await Task.Delay(500);
            SendPmdgEventNow(Pmdg777Constants.EVT_CDU_R_R6, 1); await Task.Delay(500);
            SendPmdgEventNow(Pmdg777Constants.EVT_CDU_R_L2, 1);
        }
        catch (Exception ex)
        {
            Logger.Error($"Pmdg777Adapter: RemoveGroundEquipment failed: {ex}");
        }
    }

    private async Task RemoveGroundEquipmentAsync()
    {
        try
        {
            // Disconnect GPU(s) if connected
            if (_vars.ExtPwrSec > 0.5) SendPmdgEvent(Pmdg777Constants.EVT_OH_ELEC_GRD_PWR_SEC, 1);
            if (_vars.ExtPwrPrim > 0.5) SendPmdgEvent(Pmdg777Constants.EVT_OH_ELEC_GRD_PWR_PRIM, 1);

            // Remove chocks via CDU sequence (only if chocks are actually set)
            if (_vars.WheelChocks <= 0.5)
            {
                Logger.Debug("Pmdg777Adapter: Chocks not set - skipping CDU Sequence");
                return;
            }

            Logger.Info("Pmdg777Adapter: Removing Chocks via CDU Sequence");

            // Use SendPmdgEventNow (bypasses debounce) so the repeated R6 press is never suppressed.
            SendPmdgEventNow(Pmdg777Constants.EVT_CDU_R_MENU, 1); await Task.Delay(500);
            SendPmdgEventNow(Pmdg777Constants.EVT_CDU_R_R6, 1); await Task.Delay(500);
            SendPmdgEventNow(Pmdg777Constants.EVT_CDU_R_R1, 1); await Task.Delay(500);
            SendPmdgEventNow(Pmdg777Constants.EVT_CDU_R_R6, 1);
        }
        catch (Exception ex)
        {
            Logger.Error($"Pmdg777Adapter: RemoveGroundEquipment failed: {ex}");
        }
    }


    public void Dispose()
    {
        _doorTracker.Reset();
        _sc = null;
        Logger.Debug("Pmdg777Adapter: disposed");
    }


    /// <summary>Reads the current raw L:var value for a door by its event code.</summary>
    private double GetRawDoorValue(uint evtCode) => evtCode switch
    {
        Pmdg777Constants.EVT_DOOR_1L => _vars.Door1L,
        Pmdg777Constants.EVT_DOOR_1R => _vars.Door1R,
        Pmdg777Constants.EVT_DOOR_2L => _vars.Door2L,
        Pmdg777Constants.EVT_DOOR_2R => _vars.Door2R,
        Pmdg777Constants.EVT_DOOR_3L => _vars.Door3L,
        Pmdg777Constants.EVT_DOOR_3R => _vars.Door3R,
        Pmdg777Constants.EVT_DOOR_4L => _vars.Door4L,
        Pmdg777Constants.EVT_DOOR_4R => _vars.Door4R,
        Pmdg777Constants.EVT_DOOR_5L => _vars.Door5L,
        Pmdg777Constants.EVT_DOOR_5R => _vars.Door5R,
        Pmdg777Constants.EVT_DOOR_CARGO_FWD => _vars.CargoDoorFwd,
        Pmdg777Constants.EVT_DOOR_CARGO_AFT => _vars.CargoDoorAft,
        Pmdg777Constants.EVT_DOOR_CARGO_MAIN => _vars.CargoDoorMain,
        Pmdg777Constants.EVT_DOOR_CARGO_BULK => _vars.CargoDoorBulk,
        Pmdg777Constants.EVT_DOOR_AVIONICS => _vars.AvionicsDoor,
        Pmdg777Constants.EVT_DOOR_EE_HATCH => _vars.EEHatch,
        _ => double.NaN,
    };

    /// <summary>
    /// Pushes the latest raw L:var readings into the <see cref="DoorStateTracker"/>.
    /// Called on every SimConnect data callback so state transitions are captured promptly.
    /// </summary>
    private void UpdateDoorStates()
    {
        foreach (var (evtCode, _) in Pmdg777Constants.AllDoors)
            _doorTracker.Update(evtCode, GetRawDoorValue(evtCode), Pmdg777Constants.GetDoorName(evtCode));
    }

    /// <summary>
    /// Sends a PMDG event via the SimConnect client-data channel with per-event debouncing.
    /// Use this for door toggles to prevent double-firing from rapid polling.
    /// </summary>
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

    /// <summary>
    /// Sends a PMDG event unconditionally, bypassing the debounce window.
    /// Use this for CDU key sequences where the same key must be pressed multiple times.
    /// </summary>
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
