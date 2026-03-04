using System.Runtime.InteropServices;
using Microsoft.FlightSimulator.SimConnect;

namespace SimpleGsxIntegrator.Core;

/// <summary>
/// Monitors core aircraft SimConnect variables (beacon, parking brake, engines, speed, title)
/// and maintains derived state such as <see cref="HasEnginesEverRun"/> and <see cref="HasMoved"/>.
/// 
/// Registration happens in <see cref="OnSimConnectConnected"/> which is wired to
/// <see cref="SimConnectHub.Connected"/> – the hub is the only component that owns SimConnect.
/// </summary>
public sealed class FlightStateTracker
{
    // -----------------------------------------------------------------
    //  SimConnect structs
    // -----------------------------------------------------------------

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi, Pack = 1)]
    private struct FlightStateStruct
    {
        public int BeaconLight;
        public int ParkingBrake;
        public int EngineRunning;   // GENERAL ENG COMBUSTION:1
        public int OnGround;
        public double GroundSpeed;     // knots
        public double Airspeed;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
        public string AircraftTitle;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    private struct ActivationLvarStruct
    {
        public double Value;
    }

    // -----------------------------------------------------------------
    //  Observed state (latest poll)
    // -----------------------------------------------------------------

    private FlightStateStruct _state;
    private bool _firstPoll = true;
    private const double MovedThreshold = 3.0; // knots

    // Previous values for edge-detection
    private bool _prevBeacon;
    private bool _prevParkingBrake;
    private bool _prevEngine;
    private string _prevTitle = string.Empty;

    // Derived flags
    private bool _enginesHaveRun;
    private bool _hasMoved;

    // Activation L:var tracking
    private string? _activationLvar;
    private double _lastActivationValue = double.NaN;

    // -----------------------------------------------------------------
    //  Public read-only state
    // -----------------------------------------------------------------

    public bool BeaconOn => _state.BeaconLight != 0;
    public bool ParkingBrake => _state.ParkingBrake != 0;
    public bool EngineOn => _state.EngineRunning != 0;
    public bool OnGround => _state.OnGround != 0;
    public double GroundSpeed => _state.GroundSpeed;
    public string AircraftTitle => _state.AircraftTitle ?? string.Empty;

    public bool HasEnginesEverRun => _enginesHaveRun;
    public bool HasMoved => _hasMoved;

    // -----------------------------------------------------------------
    //  Events
    // -----------------------------------------------------------------

    /// <summary>Fires when the beacon light state changes.</summary>
    public event Action<bool>? BeaconChanged;

    /// <summary>Fires when the parking brake state changes.</summary>
    public event Action<bool>? ParkingBrakeChanged;

    /// <summary>Fires when engine combustion state changes.</summary>
    public event Action<bool>? EngineChanged;

    /// <summary>Fires when the aircraft title changes (new aircraft loaded).</summary>
    public event Action<string>? AircraftChanged;

    /// <summary>
    /// Fires when the activation L:var (per-aircraft config) transitions to its
    /// expected trigger value.  Payload is the raw double value.
    /// </summary>
    public event Action<double>? ActivationLvarTriggered;

    // -----------------------------------------------------------------
    //  SimConnect wiring
    // -----------------------------------------------------------------

    /// <summary>Wire this to <see cref="SimConnectHub.Connected"/>.</summary>
    public void OnSimConnectConnected(SimConnect sc)
    {
        RegisterFlightStateVars(sc);
    }

    private void RegisterFlightStateVars(SimConnect sc)
    {
        void Add(string name, string? unit, SIMCONNECT_DATATYPE type)
            => sc.AddToDataDefinition(SimDef.FlightState, name, unit, type, 0.0f, SimConnect.SIMCONNECT_UNUSED);

        Add("LIGHT BEACON", "Bool", SIMCONNECT_DATATYPE.INT32);
        Add("BRAKE PARKING INDICATOR", "Bool", SIMCONNECT_DATATYPE.INT32);
        Add("GENERAL ENG COMBUSTION:1", "Bool", SIMCONNECT_DATATYPE.INT32);
        Add("SIM ON GROUND", "Bool", SIMCONNECT_DATATYPE.INT32);
        Add("GPS GROUND SPEED", "Knots", SIMCONNECT_DATATYPE.FLOAT64);
        Add("AIRSPEED INDICATED", "Knots", SIMCONNECT_DATATYPE.FLOAT64);
        Add("TITLE", null, SIMCONNECT_DATATYPE.STRING256);

        sc.RegisterDataDefineStruct<FlightStateStruct>(SimDef.FlightState);

        sc.RequestDataOnSimObject(
            SimReq.FlightState,
            SimDef.FlightState,
            SimConnect.SIMCONNECT_OBJECT_ID_USER,
            SIMCONNECT_PERIOD.SECOND,
            SIMCONNECT_DATA_REQUEST_FLAG.CHANGED,
            0, 0, 0);

        Logger.Debug("FlightStateTracker: SimConnect vars registered");
    }

    /// <summary>
    /// Registers a per-aircraft L:var to monitor.
    /// Called when the aircraft changes and has a configured activation L:var.
    /// </summary>
    public void SetActivationLvar(SimConnect sc, string lvarName)
    {
        if (string.IsNullOrWhiteSpace(lvarName)) return;

        if (!lvarName.StartsWith("L:", StringComparison.OrdinalIgnoreCase))
            lvarName = "L:" + lvarName;

        if (string.Equals(_activationLvar, lvarName, StringComparison.OrdinalIgnoreCase))
            return;

        _activationLvar = lvarName;
        _lastActivationValue = double.NaN;

        try
        {
            sc.AddToDataDefinition(SimDef.ActivationLvar, lvarName, "Number",
                SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
            sc.RegisterDataDefineStruct<ActivationLvarStruct>(SimDef.ActivationLvar);
            sc.RequestDataOnSimObject(
                SimReq.ActivationLvar,
                SimDef.ActivationLvar,
                SimConnect.SIMCONNECT_OBJECT_ID_USER,
                SIMCONNECT_PERIOD.SIM_FRAME,
                SIMCONNECT_DATA_REQUEST_FLAG.CHANGED,
                0, 0, 0);

            Logger.Debug($"FlightStateTracker: activation L:var registered → '{lvarName}'");
        }
        catch (Exception ex)
        {
            Logger.Warning($"FlightStateTracker: failed to register activation L:var '{lvarName}': {ex.Message}");
        }
    }

    // -----------------------------------------------------------------
    //  Data handling
    // -----------------------------------------------------------------

    /// <summary>Wire this to <see cref="SimConnectHub.SimObjectDataReceived"/>.</summary>
    public void OnSimObjectData(SIMCONNECT_RECV_SIMOBJECT_DATA data)
    {
        if (data.dwRequestID == (uint)SimReq.FlightState)
        {
            ProcessFlightState((FlightStateStruct)data.dwData[0]);
        }
        else if (data.dwRequestID == (uint)SimReq.ActivationLvar)
        {
            ProcessActivationLvar(((ActivationLvarStruct)data.dwData[0]).Value);
        }
    }

    private void ProcessFlightState(FlightStateStruct s)
    {
        _state = s;

        // Update derived flags
        if (_state.EngineRunning != 0) _enginesHaveRun = true;

        if (!_hasMoved && _enginesHaveRun && _state.GroundSpeed > MovedThreshold)
            _hasMoved = true;

        // First poll – seed previous values then fire initial-state notifications so the UI
        // populates immediately when the app connects to an already-running simulator.
        if (_firstPoll)
        {
            _firstPoll = false;
            _prevBeacon = BeaconOn;
            _prevParkingBrake = ParkingBrake;
            _prevEngine = EngineOn;
            _prevTitle = AircraftTitle;
            Logger.Debug($"FlightStateTracker: initial state – Beacon={BeaconOn} Brake={ParkingBrake} Engine={EngineOn} Speed={GroundSpeed:F1}kts Title='{AircraftTitle}'");

            // Push initial state to subscribers (these are "here's the current value" notifications,
            // not edge-triggered changes, so they won't confuse automation logic).
            if (!string.IsNullOrEmpty(AircraftTitle))
                AircraftChanged?.Invoke(AircraftTitle);

            return;
        }

        // Detect title change
        if (!string.IsNullOrEmpty(AircraftTitle) && AircraftTitle != _prevTitle)
        {
            _prevTitle = AircraftTitle;
            Logger.Debug($"FlightStateTracker: aircraft title → '{AircraftTitle}'");
            AircraftChanged?.Invoke(AircraftTitle);
        }

        // Detect beacon change
        if (BeaconOn != _prevBeacon)
        {
            _prevBeacon = BeaconOn;
            Logger.Debug($"FlightStateTracker: beacon → {(BeaconOn ? "ON" : "OFF")}");
            BeaconChanged?.Invoke(BeaconOn);
        }

        // Detect parking brake change
        if (ParkingBrake != _prevParkingBrake)
        {
            _prevParkingBrake = ParkingBrake;
            Logger.Debug($"FlightStateTracker: parking brake → {(ParkingBrake ? "SET" : "RELEASED")}");
            ParkingBrakeChanged?.Invoke(ParkingBrake);
        }

        // Detect engine change
        if (EngineOn != _prevEngine)
        {
            _prevEngine = EngineOn;
            Logger.Debug($"FlightStateTracker: engine → {(EngineOn ? "RUNNING" : "OFF")}");
            EngineChanged?.Invoke(EngineOn);
        }
    }

    private void ProcessActivationLvar(double value)
    {
        if (double.IsNaN(_lastActivationValue))
        {
            _lastActivationValue = value; // Seed without firing
            return;
        }

        if (value != _lastActivationValue)
        {
            _lastActivationValue = value;
            ActivationLvarTriggered?.Invoke(value);
        }
    }

    /// <summary>Resets all session-specific derived flags (call on aircraft change or manual reset).</summary>
    public void ResetSession()
    {
        _enginesHaveRun = false;
        _hasMoved = false;
        _firstPoll = true;
        _prevTitle = string.Empty;
        _activationLvar = null;
        _lastActivationValue = double.NaN;
        Logger.Debug("FlightStateTracker: session reset");
    }

    /// <summary>Forces the HasMoved flag (debug helper).</summary>
    public void ForceHasMoved(bool value)
    {
        _hasMoved = value;
        Logger.Debug($"FlightStateTracker: HasMoved forced to {value}");
    }
}
