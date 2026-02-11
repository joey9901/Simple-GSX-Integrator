using Microsoft.FlightSimulator.SimConnect;
using System.Runtime.InteropServices;

namespace SimpleGsxIntegrator;

public class SimVarMonitor
{
    private readonly SimConnect? _simConnect;

    public AircraftStateStruct AircraftState;

    private bool _enginesHaveRun = false;
    private bool _aircraftHasMoved = false;
    private bool _pushbackCompleted = false;
    private const double MovementThreshold = 5.0; // knots
    private bool _firstDataReceived = false;

    private bool _prevBeaconLight = false;
    private bool _prevParkingBrake = false;
    private bool _prevEngineRunning = false;
    private string _prevAircraftTitle = "";
    private string? _registeredActivationLvar = null;

    public event Action<bool>? BeaconChanged;
    public event Action<bool>? ParkingBrakeChanged;
    public event Action<bool>? EngineChanged;
    public event Action<string>? AircraftChanged;
    public event Action? RefuelingConditionsMet;
    public event Action? BoardingConditionsMet;
    public event Action? PushbackConditionsMet;
    public event Action? DeboardingConditionsMet;
    public event Action? CateringConditionsMet;
    public event Action<double>? ActivationVarReceived;

    public SimVarMonitor(SimConnect simConnect)
    {
        _simConnect = simConnect;
        RegisterSimVariables();
    }

    public void RegisterActivationLvar(string lvarName)
    {
        if (_simConnect == null) return;

        if (string.IsNullOrWhiteSpace(lvarName)) return;

        if (!lvarName.StartsWith("L:", StringComparison.OrdinalIgnoreCase))
            lvarName = "L:" + lvarName;

        if (_registeredActivationLvar != null && string.Equals(_registeredActivationLvar, lvarName, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        try
        {
            _simConnect.AddToDataDefinition(
                DEFINITIONS.ActivationVarRead,
                lvarName,
                "Number",
                SIMCONNECT_DATATYPE.FLOAT64,
                0.0f,
                SimConnect.SIMCONNECT_UNUSED);
            _simConnect.RegisterDataDefineStruct<ActivationVarStruct>(DEFINITIONS.ActivationVarRead);
            _simConnect.RequestDataOnSimObject(
                DATA_REQUESTS.ActivationVarRead,
                DEFINITIONS.ActivationVarRead,
                SimConnect.SIMCONNECT_OBJECT_ID_USER,
                SIMCONNECT_PERIOD.SIM_FRAME,
                SIMCONNECT_DATA_REQUEST_FLAG.CHANGED,
                0, 0, 0);
            _registeredActivationLvar = lvarName;
            Logger.Debug($"Registered activation L:var '{lvarName}' for monitoring");
        }
        catch (Exception ex)
        {
            Logger.Warning($"Failed to register activation L:var '{lvarName}': {ex.Message}");
        }
    }

    private void RegisterSimVariables()
    {
        if (_simConnect == null) return;

        _simConnect.AddToDataDefinition(
            DEFINITIONS.AircraftState,
            "LIGHT BEACON",
            "Bool",
            SIMCONNECT_DATATYPE.INT32,
            0.0f,
            SimConnect.SIMCONNECT_UNUSED);

        _simConnect.AddToDataDefinition(
            DEFINITIONS.AircraftState,
            "BRAKE PARKING INDICATOR",
            "Bool",
            SIMCONNECT_DATATYPE.INT32,
            0.0f,
            SimConnect.SIMCONNECT_UNUSED);

        _simConnect.AddToDataDefinition(
            DEFINITIONS.AircraftState,
            "GENERAL ENG COMBUSTION:1",
            "Bool",
            SIMCONNECT_DATATYPE.INT32,
            0.0f,
            SimConnect.SIMCONNECT_UNUSED);

        _simConnect.AddToDataDefinition(
            DEFINITIONS.AircraftState,
            "SIM ON GROUND",
            "Bool",
            SIMCONNECT_DATATYPE.INT32,
            0.0f,
            SimConnect.SIMCONNECT_UNUSED);

        _simConnect.AddToDataDefinition(
            DEFINITIONS.AircraftState,
            "GPS GROUND SPEED",
            "Number",
            SIMCONNECT_DATATYPE.FLOAT64,
            0.0f,
            SimConnect.SIMCONNECT_UNUSED);

        _simConnect.AddToDataDefinition(
            DEFINITIONS.AircraftState,
            "AIRSPEED INDICATED",
            "Number",
            SIMCONNECT_DATATYPE.FLOAT64,
            0.0f,
            SimConnect.SIMCONNECT_UNUSED);

        _simConnect.AddToDataDefinition(
            DEFINITIONS.AircraftState,
            "TITLE",
            null,
            SIMCONNECT_DATATYPE.STRING256,
            0.0f,
            SimConnect.SIMCONNECT_UNUSED);

        _simConnect.RegisterDataDefineStruct<AircraftStateStruct>(DEFINITIONS.AircraftState);

        _simConnect.RequestDataOnSimObject(
            DATA_REQUESTS.AircraftState,
            DEFINITIONS.AircraftState,
            SimConnect.SIMCONNECT_OBJECT_ID_USER,
            SIMCONNECT_PERIOD.SECOND,
            SIMCONNECT_DATA_REQUEST_FLAG.CHANGED,
            0, 0, 0);
    }

    public void OnSimObjectDataReceived(SIMCONNECT_RECV_SIMOBJECT_DATA data)
    {
        if (data.dwRequestID == (uint)DATA_REQUESTS.AircraftState)
        {
            AircraftState = (AircraftStateStruct)data.dwData[0];
        }
        else if (data.dwRequestID == (uint)DATA_REQUESTS.ActivationVarRead)
        {
            var act = (ActivationVarStruct)data.dwData[0];
            ActivationVarReceived?.Invoke(act.Value);
            return;
        }

        if (!_firstDataReceived)
        {
            _prevBeaconLight = AircraftState.BeaconLight != 0;
            _prevParkingBrake = AircraftState.ParkingBrake != 0;
            _prevEngineRunning = AircraftState.EngineRunning != 0;
            _firstDataReceived = true;
            Logger.Debug($"Initial aircraft state - Beacon: {_prevBeaconLight}, Parking Brake: {_prevParkingBrake}, Engines: {_prevEngineRunning}");
        }

        if (!string.IsNullOrEmpty(AircraftState.AircraftTitle) && AircraftState.AircraftTitle != _prevAircraftTitle)
        {
            Logger.Debug($"Aircraft detected: {AircraftState.AircraftTitle}");
            _prevAircraftTitle = AircraftState.AircraftTitle;
            AircraftChanged?.Invoke(AircraftState.AircraftTitle);
        }

        if (AircraftState.EngineRunning != 0 && !_enginesHaveRun)
            _enginesHaveRun = true;

        if ((_pushbackCompleted && AircraftState.GroundSpeed > MovementThreshold) || (_enginesHaveRun && AircraftState.GroundSpeed > MovementThreshold))
        {
            _aircraftHasMoved = true;
        }

        bool beaconOn = AircraftState.BeaconLight != 0;
        if (beaconOn != _prevBeaconLight)
        {
            _prevBeaconLight = beaconOn;
            BeaconChanged?.Invoke(beaconOn);
        }

        bool parkingBrakeOn = AircraftState.ParkingBrake != 0;
        if (parkingBrakeOn != _prevParkingBrake)
        {
            _prevParkingBrake = parkingBrakeOn;
            ParkingBrakeChanged?.Invoke(parkingBrakeOn);
        }

        bool engineRunning = AircraftState.EngineRunning != 0;
        if (engineRunning != _prevEngineRunning)
        {
            _prevEngineRunning = engineRunning;
            EngineChanged?.Invoke(engineRunning);
        }

        CheckTriggerConditions();
    }

    private void CheckTriggerConditions()
    {
        bool enginesOff = AircraftState.EngineRunning == 0;
        bool stationary = AircraftState.GroundSpeed < 0.5;
        bool beaconOn = AircraftState.BeaconLight != 0;
        bool parkingBrakeOn = AircraftState.ParkingBrake != 0;
        bool onGround = AircraftState.OnGround != 0;

        if (enginesOff && !beaconOn && onGround && stationary)
        {
            CateringConditionsMet?.Invoke();
            RefuelingConditionsMet?.Invoke();
            BoardingConditionsMet?.Invoke();
        }

        if (beaconOn && parkingBrakeOn && enginesOff && onGround && stationary && !_enginesHaveRun)
        {
            PushbackConditionsMet?.Invoke();
        }

        if (!beaconOn && parkingBrakeOn && onGround && stationary)
        {
            DeboardingConditionsMet?.Invoke();
        }
    }

    public bool GetEnginesHaveRun() => _enginesHaveRun;

    public bool GetAircraftHasMoved() => _aircraftHasMoved;

    public void SetEnginesHaveRun() => _enginesHaveRun = true;

    public void SetAircraftHasMoved() => _aircraftHasMoved = true;

    public void SetPushbackCompleted() => _pushbackCompleted = true;

    public void ResetAircraftHasMoved() => _aircraftHasMoved = false;

    public void ResetEnginesHaveRun()
    {
        _enginesHaveRun = false;
        _aircraftHasMoved = false;
        _pushbackCompleted = false;
    }
}

[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi, Pack = 1)]
public struct AircraftStateStruct
{
    public int BeaconLight;
    public int ParkingBrake;
    public int EngineRunning;
    public int OnGround;
    public double GroundSpeed;
    public double Airspeed;
    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
    public string AircraftTitle;
}

[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi, Pack = 1)]
public struct ActivationVarStruct
{
    public double Value;
}
