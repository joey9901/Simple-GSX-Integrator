using Microsoft.FlightSimulator.SimConnect;
using System.Runtime.InteropServices;

namespace SimpleGsxIntegrator;

public class SimVarMonitor
{
    private readonly SimConnect? _simConnect;
    
    public bool BeaconLight { get; private set; }
    public bool ParkingBrake { get; private set; }
    public bool EngineRunning { get; private set; }
    public bool OnGround { get; private set; }
    public double GroundSpeed { get; private set; }
    public double Airspeed { get; private set; }
    public double ExternalPower { get; private set; }
    public string AircraftTitle { get; private set; } = "";
    
    private bool _enginesHaveRun = false;
    private bool _aircraftHasMoved = false;
    private bool _pushbackCompleted = false;
    private const double MovementThreshold = 5.0; // knots
    private bool _firstDataReceived = false;
    
    private bool _prevBeaconLight = false;
    private bool _prevParkingBrake = false;
    private bool _prevEngineRunning = false;
    private string _prevAircraftTitle = "";
    
    public event Action<bool>? BeaconChanged;
    public event Action<bool>? ParkingBrakeChanged;
    public event Action<bool>? EngineChanged;
    public event Action<string>? AircraftChanged;
    public event Action? RefuelingConditionsMet;
    public event Action? BoardingConditionsMet;
    public event Action? PushbackConditionsMet;
    public event Action? DeboardingConditionsMet;
    public event Action? CateringConditionsMet;
    
    public double FwdLeftCabinDoor { get; private set; }
    public double FwdLeftCabinDoorFlag { get; private set; }
    public double AftLeftCabinDoor { get; private set; }
    public double AftLeftCabinDoorFlag { get; private set; }
    public double FwdLwrCargoDoor { get; private set; }
    public double FwdRightCabinDoor { get; private set; }
    public double FwdRightCabinDoorFlag { get; private set; }
    public double AftRightCabinDoor { get; private set; }
    public double AftRightCabinDoorFlag { get; private set; }
    public double AftLwrCargoDoor { get; private set; }
    
    public SimVarMonitor(SimConnect simConnect)
    {
        _simConnect = simConnect;
        RegisterSimVariables();
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
            "Knots",
            SIMCONNECT_DATATYPE.FLOAT64,
            0.0f,
            SimConnect.SIMCONNECT_UNUSED);
            
        _simConnect.AddToDataDefinition(
            DEFINITIONS.AircraftState,
            "AIRSPEED INDICATED",
            "Knots",
            SIMCONNECT_DATATYPE.FLOAT64,
            0.0f,
            SimConnect.SIMCONNECT_UNUSED);
            
        _simConnect.AddToDataDefinition(
            DEFINITIONS.AircraftState,
            "L:EXTERNAL POWER ON",
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

        _simConnect.AddToDataDefinition(
            DEFINITIONS.GsxVar,
            "L:FwdLeftCabinDoor",
            "Number",
            SIMCONNECT_DATATYPE.FLOAT64,
            0.0f,
            SimConnect.SIMCONNECT_UNUSED);

        _simConnect.AddToDataDefinition(
            DEFINITIONS.GsxVar,
            "L:FwdLeftCabinDoorFlag",
            "Number",
            SIMCONNECT_DATATYPE.FLOAT64,
            0.0f,
            SimConnect.SIMCONNECT_UNUSED);

        _simConnect.AddToDataDefinition(
            DEFINITIONS.GsxVar,
            "L:AftLeftCabinDoor",
            "Number",
            SIMCONNECT_DATATYPE.FLOAT64,
            0.0f,
            SimConnect.SIMCONNECT_UNUSED);

        _simConnect.AddToDataDefinition(
            DEFINITIONS.GsxVar,
            "L:AftLeftCabinDoorFlag",
            "Number",
            SIMCONNECT_DATATYPE.FLOAT64,
            0.0f,
            SimConnect.SIMCONNECT_UNUSED);

        _simConnect.AddToDataDefinition(
            DEFINITIONS.GsxVar,
            "L:FwdRightCabinDoor",
            "Number",
            SIMCONNECT_DATATYPE.FLOAT64,
            0.0f,
            SimConnect.SIMCONNECT_UNUSED);

        _simConnect.AddToDataDefinition(
            DEFINITIONS.GsxVar,
            "L:FwdRightCabinDoorFlag",
            "Number",
            SIMCONNECT_DATATYPE.FLOAT64,
            0.0f,
            SimConnect.SIMCONNECT_UNUSED);

        _simConnect.AddToDataDefinition(
            DEFINITIONS.GsxVar,
            "L:AftRightCabinDoor",
            "Number",
            SIMCONNECT_DATATYPE.FLOAT64,
            0.0f,
            SimConnect.SIMCONNECT_UNUSED);

        _simConnect.AddToDataDefinition(
            DEFINITIONS.GsxVar,
            "L:AftRightCabinDoorFlag",
            "Number",
            SIMCONNECT_DATATYPE.FLOAT64,
            0.0f,
            SimConnect.SIMCONNECT_UNUSED);

        _simConnect.AddToDataDefinition(
            DEFINITIONS.GsxVar,
            "L:FwdLwrCargoDoor",
            "Number",
            SIMCONNECT_DATATYPE.FLOAT64,
            0.0f,
            SimConnect.SIMCONNECT_UNUSED);

        _simConnect.AddToDataDefinition(
            DEFINITIONS.GsxVar,
            "L:AftLwrCargoDoor",
            "Number",
            SIMCONNECT_DATATYPE.FLOAT64,
            0.0f,
            SimConnect.SIMCONNECT_UNUSED);

        _simConnect.RegisterDataDefineStruct<DoorVarsStruct>(DEFINITIONS.GsxVar);

        _simConnect.RequestDataOnSimObject(
            DATA_REQUESTS.GsxVar,
            DEFINITIONS.GsxVar,
            SimConnect.SIMCONNECT_OBJECT_ID_USER,
            SIMCONNECT_PERIOD.SECOND,
            SIMCONNECT_DATA_REQUEST_FLAG.CHANGED,
            0, 0, 0);
    }
    
    public void OnSimObjectDataReceived(SIMCONNECT_RECV_SIMOBJECT_DATA data)
    {
        if (data.dwRequestID == (uint)DATA_REQUESTS.AircraftState)
        {
            var aircraftState = (AircraftStateStruct)data.dwData[0];

            BeaconLight = aircraftState.BeaconLight != 0;
            ParkingBrake = aircraftState.ParkingBrake != 0;
            EngineRunning = aircraftState.EngineRunning != 0;
            OnGround = aircraftState.OnGround != 0;
            GroundSpeed = aircraftState.GroundSpeed;
            Airspeed = aircraftState.Airspeed;
            ExternalPower = aircraftState.ExternalPower;
            AircraftTitle = aircraftState.AircraftTitle;
        }
        else if (data.dwRequestID == (uint)DATA_REQUESTS.GsxVar)
        {
            var doorVars = (DoorVarsStruct)data.dwData[0];
            FwdLeftCabinDoor = doorVars.FwdLeftCabinDoor;
            FwdLeftCabinDoorFlag = doorVars.FwdLeftCabinDoorFlag;
            AftLeftCabinDoor = doorVars.AftLeftCabinDoor;
            AftLeftCabinDoorFlag = doorVars.AftLeftCabinDoorFlag;
            FwdRightCabinDoor = doorVars.FwdRightCabinDoor;
            FwdRightCabinDoorFlag = doorVars.FwdRightCabinDoorFlag;
            AftRightCabinDoor = doorVars.AftRightCabinDoor;
            AftRightCabinDoorFlag = doorVars.AftRightCabinDoorFlag;
            FwdLwrCargoDoor = doorVars.FwdLwrCargoDoor;
            AftLwrCargoDoor = doorVars.AftLwrCargoDoor;
            return;
        }
        
        if (!_firstDataReceived)
        {
            _prevBeaconLight = BeaconLight;
            _prevParkingBrake = ParkingBrake;
            _prevEngineRunning = EngineRunning;
            _firstDataReceived = true;
            Logger.Debug($"Initial aircraft state - Beacon: {BeaconLight}, Parking Brake: {ParkingBrake}, Engines: {EngineRunning}");
        }
        
        if (!string.IsNullOrEmpty(AircraftTitle) && AircraftTitle != _prevAircraftTitle)
        {
            Logger.Debug($"Aircraft detected: {AircraftTitle}");
            _prevAircraftTitle = AircraftTitle;
            AircraftChanged?.Invoke(AircraftTitle);
        }
        
        if (EngineRunning && !_enginesHaveRun)
            _enginesHaveRun = true;
        
        if ((_pushbackCompleted && GroundSpeed > MovementThreshold) || (_enginesHaveRun && GroundSpeed > MovementThreshold))
        {
            _aircraftHasMoved = true;
        }
        
        if (BeaconLight != _prevBeaconLight)
        {
            _prevBeaconLight = BeaconLight;
            BeaconChanged?.Invoke(BeaconLight);
        }
        
        if (ParkingBrake != _prevParkingBrake)
        {
            _prevParkingBrake = ParkingBrake;
            ParkingBrakeChanged?.Invoke(ParkingBrake);
        }
        
        if (EngineRunning != _prevEngineRunning)
        {
            _prevEngineRunning = EngineRunning;
            EngineChanged?.Invoke(EngineRunning);
        }
        
        CheckTriggerConditions();
    }
    
    private void CheckTriggerConditions()
    {
        bool enginesOff = !EngineRunning;
        bool stationary = GroundSpeed < 0.5;
        
        if (enginesOff && !BeaconLight && OnGround && stationary)
        {
            CateringConditionsMet?.Invoke();
            RefuelingConditionsMet?.Invoke();
            BoardingConditionsMet?.Invoke();
        }
        
        if (BeaconLight && ParkingBrake && enginesOff && OnGround && stationary && !_enginesHaveRun)
        {
            PushbackConditionsMet?.Invoke();
        }
        
        if (!BeaconLight && ParkingBrake && OnGround && stationary)
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
    public double ExternalPower;
    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
    public string AircraftTitle;
}

[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi, Pack = 1)]
public struct DoorVarsStruct
{
    public double FwdLeftCabinDoor;
    public double FwdLeftCabinDoorFlag;
    public double AftLeftCabinDoor;
    public double AftLeftCabinDoorFlag;
    public double FwdRightCabinDoor;
    public double FwdRightCabinDoorFlag;
    public double AftRightCabinDoor;
    public double AftRightCabinDoorFlag;
    public double FwdLwrCargoDoor;
    public double AftLwrCargoDoor;
}
