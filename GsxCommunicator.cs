using Microsoft.FlightSimulator.SimConnect;
using System.Runtime.InteropServices;

namespace SimpleGsxIntegrator;

public class GsxCommunicator
{
    private readonly SimConnect? _simConnect;
    private bool _isGsxRunning;
    
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
        
        Logger.Debug("Registering GSX L:Vars for reading...");
        
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
        
        Logger.Debug("Registering L:Vars for WRITING...");
        
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
        
        Logger.Debug("Requesting GSX data updates...");
        
        _simConnect.RequestDataOnSimObject(
            DATA_REQUESTS.GsxVarRead,
            DEFINITIONS.GsxVarRead,
            SimConnect.SIMCONNECT_OBJECT_ID_USER,
            SIMCONNECT_PERIOD.SECOND,
            SIMCONNECT_DATA_REQUEST_FLAG.CHANGED,
            0, 0, 0);
        
        Logger.Debug("Mapping EXTERNAL_SYSTEM_TOGGLE event...");
        
        _simConnect.MapClientEventToSimEvent(EVENTS.MenuToggle, GsxConstants.EventMenu);
        
        Logger.Success("GSX communication setup complete");
    }
    
    public void OnSimObjectDataReceived(SIMCONNECT_RECV_SIMOBJECT_DATA data)
    {
    if (data.dwRequestID != (uint)DATA_REQUESTS.GsxVarRead) return;
        
        var gsxData = (GsxStateStruct)data.dwData[0];
        
        bool wasRunning = _isGsxRunning;
        _isGsxRunning = gsxData.CouatlStarted > 0;
        
        if (_isGsxRunning && !wasRunning)
        {
            Logger.Success("GSX is running!");
            GsxStarted?.Invoke();
        }
        else if (!_isGsxRunning && wasRunning)
        {
            Logger.Warning("GSX stopped");
            GsxStopped?.Invoke();
        }
        
        var newBoardingState = (GsxServiceState)(int)gsxData.BoardingState;
        if (newBoardingState != _lastBoardingState)
        {
            BoardingState = newBoardingState;
            
            if (BoardingState == GsxServiceState.Active)
                Logger.Success($"Boarding ACTIVATED");
            else if (BoardingState == GsxServiceState.Completed)
                Logger.Success($"Boarding COMPLETED");
            else if (BoardingState != GsxServiceState.Bypassed) 
                Logger.Debug($"Boarding state: {BoardingState}");
            
            BoardingStateChanged?.Invoke(BoardingState);
            _lastBoardingState = newBoardingState;
        }
        
        var newDeboardingState = (GsxServiceState)(int)gsxData.DeboardingState;
        if (newDeboardingState != _lastDeboardingState)
        {
            DeboardingState = newDeboardingState;
            
            if (DeboardingState == GsxServiceState.Active)
                Logger.Success($"Deboarding ACTIVATED");
            else if (DeboardingState == GsxServiceState.Completed)
                Logger.Success($"Deboarding COMPLETED");
            else if (DeboardingState != GsxServiceState.Bypassed)
                Logger.Debug($"Deboarding state: {DeboardingState}");
            
            DeboardingStateChanged?.Invoke(DeboardingState);
            _lastDeboardingState = newDeboardingState;
        }
        
        var newPushbackState = (GsxServiceState)(int)gsxData.DepartureState;
        if (newPushbackState != _lastPushbackState)
        {
            PushbackState = newPushbackState;
            
            if (PushbackState == GsxServiceState.Active)
                Logger.Success($"Pushback ACTIVATED");
            else if (PushbackState == GsxServiceState.Completed)
                Logger.Success("Pushback COMPLETED");
            else
                Logger.Debug($"Pushback state: {PushbackState}");
            
            PushbackStateChanged?.Invoke(PushbackState);
            _lastPushbackState = newPushbackState;
        }
        
        var newRefuelingState = (GsxServiceState)(int)gsxData.RefuelingState;
        if (newRefuelingState != _lastRefuelingState)
        {
            RefuelingState = newRefuelingState;
            
            if (RefuelingState == GsxServiceState.Active)
            {
                Logger.Success($"Refueling ACTIVATED");
            }
            else if (RefuelingState == GsxServiceState.Completed)
                Logger.Success($"Refueling COMPLETED");
            else if (RefuelingState != GsxServiceState.Bypassed)
                Logger.Debug($"Refueling state: {RefuelingState}");
            
            RefuelingStateChanged?.Invoke(RefuelingState);
            _lastRefuelingState = newRefuelingState;
        }
        
        var newCateringState = (GsxServiceState)(int)gsxData.CateringState;
        if (newCateringState != _lastCateringState)
        {
            CateringState = newCateringState;
            
            if (CateringState == GsxServiceState.Active)
                Logger.Success($"Catering ACTIVATED");
            else if (CateringState == GsxServiceState.Completed)
                Logger.Success($"Catering COMPLETED");
            else if (CateringState != GsxServiceState.Bypassed)
                Logger.Debug($"Catering state: {CateringState}");
            
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
    
    public async Task SelectMenuOption(int option, int delayMs = 500)
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
        
        await Task.Delay(delayMs);
    }
    
    public async Task CallBoarding()
    {
        if (BoardingState != GsxServiceState.Callable)
        {
            Logger.Warning($"Boarding not available (current state: {BoardingState})");
            return;
        }
        
        Logger.Info("Calling GSX Boarding");
        
        OpenGsxMenu();
        await Task.Delay(1000);
        
        // Select Boarding (option 4)
        await SelectMenuOption(4, 3000);
        
        // Select operator (option 1) - only if not already selected during refueling
        if (RefuelingState != GsxServiceState.Completed)
        {
            await SelectMenuOption(1, 5000);
        }

        // We ignore all follow up questions (menu should be hidden anyway)

        if (BoardingState == GsxServiceState.Active || BoardingState == GsxServiceState.Requested)
        {
            Logger.Success("Boarding Requested!");
        }
        else
        {
            Logger.Debug($"Boarding state did not change (still {BoardingState}) - menu likely different");
        }
    }
    
    public async Task CallRefueling()
    {
        if (RefuelingState != GsxServiceState.Callable)
        {
            Logger.Warning($"Refueling not available (current state: {RefuelingState})");
            return;
        }
        
        Logger.Info("Calling GSX Refueling");
        
        OpenGsxMenu();
        await Task.Delay(1000);
        
        // Select Refueling (option 3)
        await SelectMenuOption(3, 3000);
        
        // Select operator (option 1)
        await SelectMenuOption(1, 5000);
        
        if (RefuelingState == GsxServiceState.Active || RefuelingState == GsxServiceState.Requested)
        {
            Logger.Success("Refueling Requested!");
        }
        else
        {
            Logger.Debug($"Refueling state did not change (still {RefuelingState}) - menu likely different");
        }
    }
    
    public async Task CallCatering()
    {
        if (CateringState != GsxServiceState.Callable)
        {
            Logger.Warning($"Catering not available (current state: {CateringState})");
            return;
        }
        
        Logger.Info("Calling GSX Catering");
        
        OpenGsxMenu();
        await Task.Delay(1000);
        
        // Select Catering (option 2)
        await SelectMenuOption(2, 3000);
        
        // Select operator (option 1) - only if not already selected during refueling
        if (RefuelingState != GsxServiceState.Completed)
        {
            await SelectMenuOption(1, 5000);
        }
        
        if (CateringState == GsxServiceState.Active || CateringState == GsxServiceState.Requested)
        {
            Logger.Success("Catering Requested!");
        }
        else
        {
            Logger.Debug($"Catering state did not change (still {CateringState}) - menu likely different");
        }
    }
    
    public async Task<bool> CallPushback()
    {
        if (PushbackState != GsxServiceState.Callable)
        {
            Logger.Warning($"Pushback not available (current state: {PushbackState})");
            return false;
        }
        
        if (PushbackProgress > 0 && PushbackProgress < 5)
        {
            Logger.Warning($"Pushback already in progress (status: {PushbackProgress})");
            return false;
        }
        
        Logger.Info("Calling GSX Pushback");
        
        OpenGsxMenu();
        await Task.Delay(3000);
        
        await SelectMenuOption(5, 3000);
        
        if (RefuelingState != GsxServiceState.Completed)
        {
            await SelectMenuOption(1, 1000);
        }
        
        await Task.Delay(2000);
        
        if (PushbackState == GsxServiceState.Active || PushbackState == GsxServiceState.Requested)
        {
            Logger.Debug("Pushback menu sequence sent");
            Logger.Warning("IMPORTANT: Manually select pushback direction from GSX menu");
            return true;
        }
        else
        {
            Logger.Debug($"Pushback state did not change (still {PushbackState}) - menu likely different");
            return false;
        }
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
        
        OpenGsxMenu();
        await Task.Delay(2000);
        
        await SelectMenuOption(1, 2000);
        
        await SelectMenuOption(2, 500);
        
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
