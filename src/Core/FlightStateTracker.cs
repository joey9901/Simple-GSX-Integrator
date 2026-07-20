using System.Runtime.InteropServices;
using Microsoft.FlightSimulator.SimConnect;
using SimpleGsxIntegrator.Aircraft;

namespace SimpleGsxIntegrator.Core;

public sealed class FlightStateTracker
{
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi, Pack = 1)]
    private struct FlightStateStruct
    {
        public int BeaconLight;
        public int ParkingBrake;
        public int Engine1Running;
        public int Engine2Running;
        public int Engine3Running;
        public int Engine4Running;
        public int OnGround;
        public int CameraState;
        public int UserInputEnabled;
        public double GroundSpeed;     // knots
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
        public string AircraftTitle;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)]
        public string LiveryName;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    private struct ActivationLvarStruct
    {
        public double Value;
    }

    private FlightStateStruct _state;
    private FlightStateStruct _prevState;
    private bool _firstPoll = true;

    private string? _activationLvar;
    private double _lastActivationValue = double.NaN;

    public bool BeaconOn
    {
        get { return _state.BeaconLight != 0; }
    }

    public bool ParkingBrake
    {
        get { return _state.ParkingBrake != 0; }
    }

    private bool _prevEngineOn = false;
    public bool EngineOn
    {
        get
        {
            return _state.Engine1Running != 0 || _state.Engine2Running != 0
                || _state.Engine3Running != 0 || _state.Engine4Running != 0;
        }
    }

    public bool OnGround
    {
        get { return _state.OnGround != 0; }
    }

    public double GroundSpeed
    {
        get { return _state.GroundSpeed; }
    }

    public string AircraftTitle
    {
        get { return _state.AircraftTitle ?? string.Empty; }
    }

    public string LiveryName
    {
        get { return _state.LiveryName ?? string.Empty; }
    }

    private bool _enginesHaveRun = false;
    public bool HasEnginesEverRun
    {
        get { return _enginesHaveRun; }
    }

    private bool _hasMoved = false;
    public bool HasMoved
    {
        get { return _hasMoved; }
    }

    private bool _prevIsInMenu = true; // this can trigger OnSpawned if app wasnt loaded in menu

    private bool _isInMenu = false;
    public bool IsInMenu
    {
        get { return _isInMenu; }
    }

    private static readonly TimeSpan SpawnSettleDuration = TimeSpan.FromSeconds(5);
    private DateTime _settleUntil = DateTime.MinValue;
    private bool _settlingBaselinePending;

    // True for a short window after spawning into flight, while the addon's own systems are
    // still applying their default panel preset. Beacon/engine readings during this window
    // don't reflect a genuine user action, so consumers should not react to them.
    public bool IsSettling
    {
        get { return DateTime.UtcNow < _settleUntil; }
    }


    public event Action<bool>? BeaconChanged;
    public event Action<bool>? ParkingBrakeChanged;
    public event Action<bool>? EngineChanged;
    public event Action<bool>? EnginesEverRunChanged;
    public event Action<bool>? HasMovedChanged;
    public event Action<string>? AircraftChanged;
    public event Action<double>? ActivationLvarTriggered;
    public event Action? SpawnedAtGate;
    public event Action? MenuStateChanged;

    public void OnSimConnectConnected(SimConnect sc, AircraftAdapterBase? adapter = null)
    {
        RegisterFlightStateVars(sc, adapter);
    }

    private void RegisterFlightStateVars(SimConnect sc, AircraftAdapterBase? adapter = null)
    {
        sc.ClearDataDefinition(SimDef.FlightState);

        AddFlightStateVar(sc, adapter?.beaconLightVariable ?? "LIGHT BEACON", "Bool", SIMCONNECT_DATATYPE.INT32);
        AddFlightStateVar(sc, adapter?.parkingBrakeVariable ?? "BRAKE PARKING INDICATOR", "Bool", SIMCONNECT_DATATYPE.INT32);
        AddFlightStateVar(sc, adapter?.engine1RunningVariable ?? "GENERAL ENG COMBUSTION:1", "Bool", SIMCONNECT_DATATYPE.INT32);
        AddFlightStateVar(sc, adapter?.engine2RunningVariable ?? "GENERAL ENG COMBUSTION:2", "Bool", SIMCONNECT_DATATYPE.INT32);
        AddFlightStateVar(sc, adapter?.engine3RunningVariable ?? "GENERAL ENG COMBUSTION:3", "Bool", SIMCONNECT_DATATYPE.INT32);
        AddFlightStateVar(sc, adapter?.engine4RunningVariable ?? "GENERAL ENG COMBUSTION:4", "Bool", SIMCONNECT_DATATYPE.INT32);
        AddFlightStateVar(sc, "SIM ON GROUND", "Bool", SIMCONNECT_DATATYPE.INT32);
        AddFlightStateVar(sc, "CAMERA STATE", "Number", SIMCONNECT_DATATYPE.INT32);
        AddFlightStateVar(sc, "USER INPUT ENABLED", "Bool", SIMCONNECT_DATATYPE.INT32);
        AddFlightStateVar(sc, "GPS GROUND SPEED", "Knots", SIMCONNECT_DATATYPE.FLOAT64);
        AddFlightStateVar(sc, "TITLE", null, SIMCONNECT_DATATYPE.STRING256);
        AddFlightStateVar(sc, "LIVERY NAME", null, SIMCONNECT_DATATYPE.STRING64);

        sc.RegisterDataDefineStruct<FlightStateStruct>(SimDef.FlightState);

        sc.RequestDataOnSimObject(
            SimReq.FlightState,
            SimDef.FlightState,
            SimConnect.SIMCONNECT_OBJECT_ID_USER,
            SIMCONNECT_PERIOD.SECOND,
            SIMCONNECT_DATA_REQUEST_FLAG.DEFAULT, // unconditional heartbeat - our own diffing needs a steady poll for debouncing to resolve, CHANGED would stall it while values are stable
            0, 0, 0);

        Logger.Debug("FlightStateTracker: SimConnect vars registered");
    }

    private void AddFlightStateVar(SimConnect sc, string name, string? unit, SIMCONNECT_DATATYPE type)
    {
        sc.AddToDataDefinition(SimDef.FlightState, name, unit, type, 0.0f, SimConnect.SIMCONNECT_UNUSED);
    }

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

        if (_state.CameraState == 31.0 || _state.CameraState == 32.0 || _state.CameraState == 12.0)
            _isInMenu = true;
        else if (_state.CameraState == 2.0)
            _isInMenu = false;

        if (_firstPoll)
        {
            _firstPoll = false;
            _prevState = s;
            _prevIsInMenu = IsInMenu;
            Logger.Debug($"FlightStateTracker: initial state - Beacon={BeaconOn} Brake={ParkingBrake} Engine={EngineOn} Speed={GroundSpeed:F1}kts Title='{AircraftTitle}' Livery='{LiveryName}'");

            if (!string.IsNullOrEmpty(AircraftTitle))
            {
                AircraftChanged?.Invoke(AircraftTitle);
            }

            return;
        }

        if (!string.IsNullOrEmpty(AircraftTitle) && _state.AircraftTitle != _prevState.AircraftTitle)
        {
            _prevState.AircraftTitle = _state.AircraftTitle;
            Logger.Debug($"FlightStateTracker: aircraft title → '{AircraftTitle}'");
            AircraftChanged?.Invoke(AircraftTitle);
        }

        if (_prevIsInMenu != IsInMenu)
        {
            var spawnedIntoFlight = _prevIsInMenu && !IsInMenu;

            _prevIsInMenu = IsInMenu;
            Logger.Debug($"FlightStateTracker: Menu state changed → {(IsInMenu ? "IN MENU" : "IN FLIGHT")}");
            MenuStateChanged?.Invoke();

            if (spawnedIntoFlight)
            {
                _settleUntil = DateTime.UtcNow.Add(SpawnSettleDuration);
                _settlingBaselinePending = true;
                SpawnedAtGate?.Invoke();
                return;
            }
        }

        if (IsInMenu) return;

        if (IsSettling) return;

        if (_settlingBaselinePending)
        {
            _settlingBaselinePending = false;
            _prevState = _state;
            _prevEngineOn = EngineOn;
            return;
        }

        if (_prevState.BeaconLight != _state.BeaconLight)
        {
            _prevState.BeaconLight = _state.BeaconLight;
            Logger.Debug($"FlightStateTracker: beacon → {(BeaconOn ? "ON" : "OFF")}");
            BeaconChanged?.Invoke(BeaconOn);
        }

        if (_state.ParkingBrake != _prevState.ParkingBrake)
        {
            _prevState.ParkingBrake = _state.ParkingBrake;
            Logger.Debug($"FlightStateTracker: parking brake → {(ParkingBrake ? "SET" : "RELEASED")}");
            ParkingBrakeChanged?.Invoke(ParkingBrake);
        }

        if (EngineOn != _prevEngineOn)
        {
            _prevEngineOn = EngineOn;
            Logger.Debug($"FlightStateTracker: engine → {(EngineOn ? "RUNNING" : "OFF")}");
            EngineChanged?.Invoke(EngineOn);
        }

        if (_prevEngineOn && !_enginesHaveRun)
        {
            _enginesHaveRun = true;
            EnginesEverRunChanged?.Invoke(true);
        }

        if (_state.GroundSpeed > 5)
        {
            if (!_hasMoved)
            {
                _hasMoved = true;
                HasMovedChanged?.Invoke(_hasMoved);
            }
        }
    }

    private void ProcessActivationLvar(double value)
    {
        if (double.IsNaN(_lastActivationValue))
        {
            _lastActivationValue = value;
            return;
        }

        if (value != _lastActivationValue)
        {
            _lastActivationValue = value;
            ActivationLvarTriggered?.Invoke(value);
        }
    }

    public void ResetSession()
    {
        _enginesHaveRun = false;
        EnginesEverRunChanged?.Invoke(false);
        _hasMoved = false;
        HasMovedChanged?.Invoke(false);
        _activationLvar = null;
        _lastActivationValue = double.NaN;
        Logger.Debug("FlightStateTracker: session reset");
    }

    public void ForceHasMoved(bool value)
    {
        _hasMoved = value;
        HasMovedChanged?.Invoke(value);
        Logger.Debug($"FlightStateTracker: HasMoved forced to {value}");
    }

    public void ForceEnginesEverRun(bool value)
    {
        _enginesHaveRun = value;
        EnginesEverRunChanged?.Invoke(value);
        Logger.Debug($"FlightStateTracker: HasEnginesEverRun forced to {value}");
    }
}
