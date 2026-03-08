using System.Runtime.InteropServices;
using Microsoft.FlightSimulator.SimConnect;

namespace SimpleGsxIntegrator.Core;

public sealed class FlightStateTracker
{
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi, Pack = 1)]
    private struct FlightStateStruct
    {
        public int BeaconLight;
        public int ParkingBrake;
        public int EngineRunning;   // GENERAL ENG COMBUSTION:1
        // Note that there is an extremely rare edge case where if engine 2 is used 
        // for taxi and for some reason the user wants to return to gate for 
        // deboarding, deboarding won't be called since HasEnginesEverRun is false
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

    private FlightStateStruct _state;
    private FlightStateStruct _prevState;
    private bool _firstPoll = true;
    private const double MovedThreshold = 3.0; // knots
    private const double AirSpeedThreshold = 50.0; // knots 

    private bool _enginesHaveRun;
    private bool _hasMoved;

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

    public bool EngineOn
    {
        get { return _state.EngineRunning != 0; }
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

    public bool HasEnginesEverRun
    {
        get { return _enginesHaveRun; }
    }

    public bool HasMoved
    {
        get { return _hasMoved; }
    }


    public event Action<bool>? BeaconChanged;
    public event Action<bool>? ParkingBrakeChanged;
    public event Action<bool>? EngineChanged;
    public event Action<double>? SpeedChanged;
    public event Action<string>? AircraftChanged;
    public event Action<double>? ActivationLvarTriggered;

    public void OnSimConnectConnected(SimConnect sc)
    {
        RegisterFlightStateVars(sc);
    }

    private void RegisterFlightStateVars(SimConnect sc)
    {
        AddFlightStateVar(sc, "LIGHT BEACON", "Bool", SIMCONNECT_DATATYPE.INT32);
        AddFlightStateVar(sc, "BRAKE PARKING INDICATOR", "Bool", SIMCONNECT_DATATYPE.INT32);
        AddFlightStateVar(sc, "GENERAL ENG COMBUSTION:1", "Bool", SIMCONNECT_DATATYPE.INT32);
        AddFlightStateVar(sc, "SIM ON GROUND", "Bool", SIMCONNECT_DATATYPE.INT32);
        AddFlightStateVar(sc, "GPS GROUND SPEED", "Knots", SIMCONNECT_DATATYPE.FLOAT64);
        AddFlightStateVar(sc, "AIRSPEED INDICATED", "Knots", SIMCONNECT_DATATYPE.FLOAT64);
        AddFlightStateVar(sc, "TITLE", null, SIMCONNECT_DATATYPE.STRING256);

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

        if (_state.EngineRunning != 0) _enginesHaveRun = true;

        if (!_hasMoved)
        {
            if ((_enginesHaveRun && _state.GroundSpeed > MovedThreshold)
                || (_state.OnGround < 1 && _state.Airspeed > AirSpeedThreshold))
                _hasMoved = true;
        }

        // First poll – seed previous values then fire initial-state notifications so the UI
        // populates immediately when the app connects to an already-running simulator.
        if (_firstPoll)
        {
            _firstPoll = false;
            _prevState = s;
            Logger.Debug($"FlightStateTracker: initial state – Beacon={BeaconOn} Brake={ParkingBrake} Engine={EngineOn} Speed={GroundSpeed:F1}kts Title='{AircraftTitle}'");

            if (!string.IsNullOrEmpty(AircraftTitle))
                AircraftChanged?.Invoke(AircraftTitle);

            return;
        }

        if (!string.IsNullOrEmpty(AircraftTitle) && _state.AircraftTitle != _prevState.AircraftTitle)
        {
            _prevState.AircraftTitle = _state.AircraftTitle;
            Logger.Debug($"FlightStateTracker: aircraft title → '{AircraftTitle}'");
            AircraftChanged?.Invoke(AircraftTitle);
        }

        if (_state.BeaconLight != _prevState.BeaconLight)
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

        if (_state.EngineRunning != _prevState.EngineRunning)
        {
            _prevState.EngineRunning = _state.EngineRunning;
            Logger.Debug($"FlightStateTracker: engine → {(EngineOn ? "RUNNING" : "OFF")}");
            EngineChanged?.Invoke(EngineOn);
        }
        if (Math.Abs(_state.GroundSpeed - _prevState.GroundSpeed) > 0.5)
        {
            _prevState.GroundSpeed = _state.GroundSpeed;
            SpeedChanged?.Invoke(GroundSpeed);
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
        _hasMoved = false;
        _activationLvar = null;
        _lastActivationValue = double.NaN;
        Logger.Debug("FlightStateTracker: session reset");
    }

    public void ForceHasMoved(bool value)
    {
        _hasMoved = value;
        Logger.Debug($"FlightStateTracker: HasMoved forced to {value}");
    }

    public void ForceEnginesEverRun(bool value)
    {
        _enginesHaveRun = value;
        Logger.Debug($"FlightStateTracker: HasEnginesEverRun forced to {value}");
    }
}
