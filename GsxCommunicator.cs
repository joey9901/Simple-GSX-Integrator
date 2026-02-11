using Microsoft.FlightSimulator.SimConnect;
using System.Runtime.InteropServices;

namespace SimpleGsxIntegrator;

public class GsxCommunicator
{
    private readonly SimConnect? _simConnect;
    private bool _isGsxRunning;
    private bool _handlingOperatorSelected = false;

    private GsxServiceState _lastBoardingState = GsxServiceState.Unknown;
    private GsxServiceState _lastDeboardingState = GsxServiceState.Unknown;
    private GsxServiceState _lastPushbackState = GsxServiceState.Unknown;
    private GsxServiceState _lastRefuelingState = GsxServiceState.Unknown;
    private GsxServiceState _lastCateringState = GsxServiceState.Unknown;

    public GsxServiceState BoardingState { get; private set; } = GsxServiceState.Unknown;
    public GsxServiceState DeboardingState { get; private set; } = GsxServiceState.Unknown;
    public GsxServiceState PushbackState { get; private set; } = GsxServiceState.Unknown;
    public GsxServiceState RefuelingState { get; private set; } = GsxServiceState.Unknown;
    public GsxServiceState CateringState { get; private set; } = GsxServiceState.Unknown;
    public int PushbackProgress { get; private set; } = 0;

    public event Action? GsxStarted;
    public event Action? GsxStopped;
    public event Action<GsxServiceState>? BoardingStateChanged;
    public event Action<GsxServiceState>? DeboardingStateChanged;
    public event Action<GsxServiceState>? PushbackStateChanged;
    public event Action<GsxServiceState>? RefuelingStateChanged;
    public event Action<GsxServiceState>? CateringStateChanged;

    public GsxCommunicator(SimConnect simConnect)
    {
        _simConnect = simConnect;
        RegisterGsxVariables();
    }

    private void RegisterGsxVariables()
    {
        if (_simConnect == null) return;

        _simConnect.AddToDataDefinition(
            DEFINITIONS.GsxVarRead,
            GsxConstants.VarCouatlStarted,
            null,
            SIMCONNECT_DATATYPE.FLOAT64,
            0.0f,
            SimConnect.SIMCONNECT_UNUSED);

        _simConnect.AddToDataDefinition(
            DEFINITIONS.GsxVarRead,
            GsxConstants.VarMenuOpen,
            null,
            SIMCONNECT_DATATYPE.FLOAT64,
            0.0f,
            SimConnect.SIMCONNECT_UNUSED);

        _simConnect.AddToDataDefinition(
            DEFINITIONS.GsxVarRead,
            GsxConstants.VarMenuChoice,
            null,
            SIMCONNECT_DATATYPE.FLOAT64,
            0.0f,
            SimConnect.SIMCONNECT_UNUSED);

        _simConnect.AddToDataDefinition(
            DEFINITIONS.GsxVarRead,
            GsxConstants.VarServiceBoarding,
            null,
            SIMCONNECT_DATATYPE.FLOAT64,
            0.0f,
            SimConnect.SIMCONNECT_UNUSED);

        _simConnect.AddToDataDefinition(
            DEFINITIONS.GsxVarRead,
            GsxConstants.VarServiceDeboarding,
            null,
            SIMCONNECT_DATATYPE.FLOAT64,
            0.0f,
            SimConnect.SIMCONNECT_UNUSED);

        _simConnect.AddToDataDefinition(
            DEFINITIONS.GsxVarRead,
            GsxConstants.VarServiceDeparture,
            null,
            SIMCONNECT_DATATYPE.FLOAT64,
            0.0f,
            SimConnect.SIMCONNECT_UNUSED);

        _simConnect.AddToDataDefinition(
            DEFINITIONS.GsxVarRead,
            GsxConstants.VarPushbackStatus,
            null,
            SIMCONNECT_DATATYPE.FLOAT64,
            0.0f,
            SimConnect.SIMCONNECT_UNUSED);

        _simConnect.AddToDataDefinition(
            DEFINITIONS.GsxVarRead,
            GsxConstants.VarServiceRefueling,
            null,
            SIMCONNECT_DATATYPE.FLOAT64,
            0.0f,
            SimConnect.SIMCONNECT_UNUSED);

        _simConnect.AddToDataDefinition(
            DEFINITIONS.GsxVarRead,
            GsxConstants.VarServiceCatering,
            null,
            SIMCONNECT_DATATYPE.FLOAT64,
            0.0f,
            SimConnect.SIMCONNECT_UNUSED);

        _simConnect.RegisterDataDefineStruct<GsxStateStruct>(DEFINITIONS.GsxVarRead);

        _simConnect.AddToDataDefinition(
            DEFINITIONS.GsxMenuOpenWrite,
            GsxConstants.VarMenuOpen,
            null,
            SIMCONNECT_DATATYPE.FLOAT64,
            0.0f,
            SimConnect.SIMCONNECT_UNUSED);

        _simConnect.RegisterDataDefineStruct<SetGsxMenuChoice>(DEFINITIONS.GsxMenuOpenWrite);

        _simConnect.AddToDataDefinition(
            DEFINITIONS.GsxMenuChoiceWrite,
            GsxConstants.VarMenuChoice,
            null,
            SIMCONNECT_DATATYPE.FLOAT64,
            0.0f,
            SimConnect.SIMCONNECT_UNUSED);

        _simConnect.RegisterDataDefineStruct<SetGsxMenuChoice>(DEFINITIONS.GsxMenuChoiceWrite);

        _simConnect.RequestDataOnSimObject(
            DATA_REQUESTS.GsxVarRead,
            DEFINITIONS.GsxVarRead,
            SimConnect.SIMCONNECT_OBJECT_ID_USER,
            SIMCONNECT_PERIOD.SECOND,
            SIMCONNECT_DATA_REQUEST_FLAG.CHANGED,
            0, 0, 0);

        _simConnect.MapClientEventToSimEvent(EVENTS.MenuToggle, GsxConstants.EventMenu);
    }

    public void OnSimObjectDataReceived(SIMCONNECT_RECV_SIMOBJECT_DATA data)
    {
        if (data.dwRequestID != (uint)DATA_REQUESTS.GsxVarRead) return;

        var gsxData = (GsxStateStruct)data.dwData[0];

        bool wasRunning = _isGsxRunning;
        _isGsxRunning = gsxData.CouatlStarted > 0;

        if (_isGsxRunning && !wasRunning)
        {
            GsxStarted?.Invoke();
        }
        else if (!_isGsxRunning && wasRunning)
        {
            GsxStopped?.Invoke();
            _handlingOperatorSelected = false;
        }

        var newBoardingState = (GsxServiceState)(int)gsxData.BoardingState;
        if (newBoardingState != _lastBoardingState)
        {
            BoardingState = newBoardingState;
            BoardingStateChanged?.Invoke(BoardingState);
            _lastBoardingState = newBoardingState;
        }

        var newDeboardingState = (GsxServiceState)(int)gsxData.DeboardingState;
        if (newDeboardingState != _lastDeboardingState)
        {
            DeboardingState = newDeboardingState;
            DeboardingStateChanged?.Invoke(DeboardingState);
            _lastDeboardingState = newDeboardingState;
        }

        var newPushbackState = (GsxServiceState)(int)gsxData.DepartureState;
        if (newPushbackState != _lastPushbackState)
        {
            PushbackState = newPushbackState;
            PushbackStateChanged?.Invoke(PushbackState);
            _lastPushbackState = newPushbackState;
        }

        var newRefuelingState = (GsxServiceState)(int)gsxData.RefuelingState;
        if (newRefuelingState != _lastRefuelingState)
        {
            RefuelingState = newRefuelingState;
            RefuelingStateChanged?.Invoke(RefuelingState);
            _lastRefuelingState = newRefuelingState;
        }

        var newCateringState = (GsxServiceState)(int)gsxData.CateringState;
        if (newCateringState != _lastCateringState)
        {
            CateringState = newCateringState;
            CateringStateChanged?.Invoke(CateringState);
            _lastCateringState = newCateringState;
        }

        PushbackProgress = (int)gsxData.PushbackStatus;
    }

    public bool IsGsxRunning() => _isGsxRunning;

    public void OpenGsxMenu()
    {
        if (_simConnect == null) return;

        Logger.Debug("Opening GSX menu (writing 1 to L:FSDT_GSX_MENU_OPEN)");
        var data = new SetGsxMenuChoice { MenuChoice = 1 };
        _simConnect.SetDataOnSimObject(
            DEFINITIONS.GsxMenuOpenWrite,
            SimConnect.SIMCONNECT_OBJECT_ID_USER,
            SIMCONNECT_DATA_SET_FLAG.DEFAULT,
            data);
    }

    public void CloseGsxMenu()
    {
        if (_simConnect == null) return;

        Logger.Debug("Closing GSX menu (writing 0 to L:FSDT_GSX_MENU_OPEN)");
        var data = new SetGsxMenuChoice { MenuChoice = 0 };
        _simConnect.SetDataOnSimObject(
            DEFINITIONS.GsxMenuOpenWrite,
            SimConnect.SIMCONNECT_OBJECT_ID_USER,
            SIMCONNECT_DATA_SET_FLAG.DEFAULT,
            data);
    }

    public async Task SelectMenuOption(int option)
    {
        if (_simConnect == null) return;

        int valueToWrite = option - 1;
        Logger.Debug($"Selecting menu option {option} (writing {valueToWrite} to L:FSDT_GSX_MENU_CHOICE)");
        var data = new SetGsxMenuChoice { MenuChoice = valueToWrite };
        _simConnect.SetDataOnSimObject(
            DEFINITIONS.GsxMenuChoiceWrite,
            SimConnect.SIMCONNECT_OBJECT_ID_USER,
            SIMCONNECT_DATA_SET_FLAG.DEFAULT,
            data);
    }

    private async Task SelectHandlingOperator()
    {
        if (_simConnect == null) return;
        if (_handlingOperatorSelected) return;

        await SelectMenuOption(1);
        _handlingOperatorSelected = true;
    }

    public async Task CallBoarding()
    {
        GsxServiceState oldBoardingState = BoardingState;
        if (BoardingState != GsxServiceState.Callable)
        {
            Logger.Warning($"Boarding not available (current state: {BoardingState})");
            return;
        }

        Logger.Info("Calling GSX Boarding");

        CloseGsxMenu();
        await Task.Delay(1500);
        OpenGsxMenu();
        await Task.Delay(1500);
        await SelectMenuOption(4);
        await Task.Delay(1500);
        await SelectHandlingOperator();
        await Task.Delay(1500);
        CloseGsxMenu();

        if (oldBoardingState == BoardingState)
        {
            Logger.Debug($"Boarding state did not change (still {BoardingState}) - menu likely different");
        }
    }

    public async Task CallRefueling()
    {
        GsxServiceState oldRefuelingState = RefuelingState;
        if (RefuelingState != GsxServiceState.Callable)
        {
            Logger.Warning($"Refueling not available (current state: {RefuelingState})");
            return;
        }

        Logger.Info("Calling GSX Refueling");

        CloseGsxMenu();
        await Task.Delay(1500);
        OpenGsxMenu();
        await Task.Delay(1500);
        await SelectMenuOption(3);
        await Task.Delay(1500);
        await SelectHandlingOperator();
        await Task.Delay(1500);
        CloseGsxMenu();

        Logger.Debug("Refueling menu sequence sent");

        if (oldRefuelingState == RefuelingState)
        {
            Logger.Debug($"Refueling state did not change (still {RefuelingState}) - menu likely different");
        }
    }

    public async Task CallCatering()
    {
        GsxServiceState oldCateringState = CateringState;
        if (CateringState != GsxServiceState.Callable)
        {
            Logger.Warning($"Catering not available (current state: {CateringState})");
            return;
        }

        Logger.Info("Calling GSX Catering");

        CloseGsxMenu();
        await Task.Delay(1500);
        OpenGsxMenu();
        await Task.Delay(1500);
        await SelectMenuOption(2);
        await Task.Delay(1500);
        await SelectHandlingOperator();
        await Task.Delay(1500);
        CloseGsxMenu();

        Logger.Debug("Catering menu sequence sent");

        if (oldCateringState == CateringState)
        {
            Logger.Debug($"Catering state did not change (still {CateringState}) - menu likely different");
        }
    }

    public async Task CallPushback()
    {
        if (PushbackState != GsxServiceState.Callable)
        {
            Logger.Warning($"Pushback not available (current state: {PushbackState})");
            return;
        }

        if (PushbackProgress > 0 && PushbackProgress < 5)
        {
            Logger.Warning($"Pushback already in progress (status: {PushbackProgress})");
            return;
        }

        Logger.Info("Calling GSX Pushback");

        CloseGsxMenu();
        await Task.Delay(1500);
        OpenGsxMenu();
        await Task.Delay(1500);
        await SelectMenuOption(5);
        await Task.Delay(1500);
        await SelectHandlingOperator();
        await Task.Delay(1500);
        CloseGsxMenu();

        Logger.Debug("Pushback menu sequence sent");
    }

    public async Task CallDeboarding()
    {
        if (DeboardingState != GsxServiceState.Callable)
        {
            Logger.Warning($"Deboarding not available (current state: {DeboardingState})");
            return;
        }

        Logger.Info("Calling GSX Deboarding");
        Logger.Warning("If map opens, select your gate position in GSX first, then call deboarding manually.");

        CloseGsxMenu();
        await Task.Delay(1500);
        OpenGsxMenu();
        await Task.Delay(1500);
        await SelectMenuOption(1);
        await Task.Delay(1500);
        await SelectMenuOption(2);
        await Task.Delay(1500);
        CloseGsxMenu();

        Logger.Debug("Deboarding menu sequence sent");
    }
}

[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi, Pack = 1)]
public struct GsxStateStruct
{
    public double CouatlStarted;
    public double MenuOpen;
    public double MenuChoice;
    public double BoardingState;
    public double DeboardingState;
    public double DepartureState;
    public double PushbackStatus;
    public double RefuelingState;
    public double CateringState;
}

[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi, Pack = 1)]
public struct SetGsxMenuChoice
{
    public double MenuChoice;
}

public enum EVENTS
{
    MenuToggle
}

public enum GsxServiceState
{
    Unknown = 0,
    Callable = 1,
    NotAvailable = 2,
    Bypassed = 3,
    Requested = 4,
    Active = 5,
    Completed = 6,
}
