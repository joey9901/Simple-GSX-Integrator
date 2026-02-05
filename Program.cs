using Microsoft.FlightSimulator.SimConnect;
using System.Runtime.InteropServices;

namespace SimpleGsxIntegrator;

class Program
{
    private const int TriggerCooldownSeconds = 30;
    private const int KeyPollingDelayMs = 100;
    private const int SimConnectPumpDelayMs = 10;
    private const int ServiceTriggerDelayMs = 1000;
    private const int VK_MENU = 0x12;
    private const int VK_CONTROL = 0x11;
    private const int VK_SHIFT = 0x10;  
    
    private static SimConnect? _simConnect;
    private static SimVarMonitor? _simVariableMonitor;
    private static GsxCommunicator? _gsxCommunicator;
    private static IntPtr _windowHandle = IntPtr.Zero;
    private static MainForm? _mainForm;
    
    private static bool _isRunning = true;
    private static bool _systemActivated = false;
    private static bool _hotkeyPolling = true;
    private static volatile bool _rebindingInProgress = false;
    private static int _rebindCooldown = 0;
    private static bool _monitoringMsfs = false;
    
    private static AppConfig? _config;
    private static ParsedHotkey? _activationHotkey;
    private static ParsedHotkey? _resetHotkey;
    private static ParsedHotkey? _toggleRefuelHotkey;
    
    private static bool _deboardingCompleted = false;
    private static bool _pushbackCompleted = false;
    private static bool _boardingCompleted = false;
    private static bool _refuelingCompleted = false;
    private static bool _refuelingWasActive = false;
    
    private static DateTime _lastBoardingTrigger = DateTime.MinValue;
    private static DateTime _lastPushbackTrigger = DateTime.MinValue;
    private static DateTime _lastDeboardingTrigger = DateTime.MinValue;
    private static DateTime _lastRefuelingTrigger = DateTime.MinValue;
    
    private static bool _boardingBlockedLogged = false;
    private static bool _pushbackBlockedLogged = false;
    private static bool _boardingCompletedWarningLogged = false;
    private static bool _deboardingCompletedWarningLogged = false;
    private static bool _refuelingBlockedLogged = false;
    
    private static bool? _prevBeaconOn = null;
    private static bool? _prevParkingBrakeSet = null;
    private static string? _currentAircraft = null;
    private static Mutex? _instanceMutex;
    
    [STAThread]
    static void Main(string[] args)
    {
        _instanceMutex = new Mutex(true, "SimpleGSXIntegrator_SingleInstance", out bool createdNew);
        
        if (!createdNew)
        {
            MessageBox.Show("Simple GSX Integrator is already running!", "Already Running", 
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        
        _config = ConfigManager.Load();
        _activationHotkey = HotkeyParser.Parse(_config.Hotkeys.ActivationKey);
        _resetHotkey = HotkeyParser.Parse(_config.Hotkeys.ResetKey);
        _toggleRefuelHotkey = HotkeyParser.Parse(_config.Hotkeys.ToggleRefuelKey);
        
        _mainForm = new MainForm();
        Logger.MainForm = _mainForm;
        _mainForm.SetHotkeys(_config.Hotkeys.ActivationKey, _config.Hotkeys.ResetKey, _config.Hotkeys.ToggleRefuelKey);
        
        _ = Task.Run(InitializeAsync);
        
        Application.Run(_mainForm);
    }
    
    static async Task InitializeAsync()
    {
        try
        {
            _windowHandle = _mainForm!.Handle;
            
            _ = Task.Run(PollForHotkeys);
            Logger.Debug($"Registered global hotkeys: {_config!.Hotkeys.ActivationKey} (activate), {_config!.Hotkeys.ResetKey} (reset), {_config!.Hotkeys.ToggleRefuelKey} (toggle refuel)");
            
            bool msfsWasRunningAtStartup = IsMsfsRunning();
            if (msfsWasRunningAtStartup)
            {
                _ = Task.Run(MonitorMsfsProcess);
                Logger.Debug("MSFS was running at startup - will close when MSFS closes");
            }
            else
            {
                Logger.Debug("Started manually - waiting for MSFS to launch...");
            }
            
            _simConnect = new SimConnect("SimpleGSXIntegrator", _windowHandle, 0, null, 0);
            Logger.Success("Connected to SimConnect");
            _mainForm?.SetSimConnectStatus(true);
            
            _simVariableMonitor = new SimVarMonitor(_simConnect);
            _gsxCommunicator = new GsxCommunicator(_simConnect);
            
            _simVariableMonitor.BeaconChanged += OnBeaconChanged;
            _simVariableMonitor.ParkingBrakeChanged += OnParkingBrakeChanged;
            _simVariableMonitor.EngineChanged += OnEngineChanged;
            _simVariableMonitor.AircraftChanged += OnAircraftChanged;
            _simVariableMonitor.RefuelingConditionsMet += OnRefuelingConditions;
            _simVariableMonitor.BoardingConditionsMet += OnBoardingConditions;
            _simVariableMonitor.PushbackConditionsMet += OnPushbackConditions;
            _simVariableMonitor.DeboardingConditionsMet += OnDeboardingConditions;
            
            _gsxCommunicator.DeboardingStateChanged += OnDeboardingStateChanged;
            _gsxCommunicator.PushbackStateChanged += OnPushbackStateChanged;
            _gsxCommunicator.BoardingStateChanged += OnBoardingStateChanged;
            _gsxCommunicator.RefuelingStateChanged += OnRefuelingStateChanged;
            _gsxCommunicator.GsxStarted += () => _mainForm?.SetGsxStatus(true);
            _gsxCommunicator.GsxStopped += () => _mainForm?.SetGsxStatus(false);
            
            Logger.Info($"SYSTEM STATUS: {(_systemActivated ? "ACTIVATED" : $"STANDBY - Press {_config!.Hotkeys.ActivationKey} to activate")}");
            _mainForm?.SetSystemStatus(_systemActivated);
            
            _simConnect.OnRecvSimobjectData += OnReceiveSimObjectData;
            _simConnect.OnRecvQuit += OnReceiveQuit;
            
            await MessagePump();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"\nError: {ex.Message}");
            Console.WriteLine(ex.StackTrace);
        }
        finally
        {
            _hotkeyPolling = false;
            _simConnect?.Dispose();
        }
    }
    
    static async Task MessagePump()
    {
        while (_isRunning && _simConnect != null)
        {
            try
            {
                _simConnect.ReceiveMessage();
                await Task.Delay(SimConnectPumpDelayMs);
            }
            catch (Exception ex)
            {
                Logger.Error($"Message pump error: {ex.Message}");
            }
        }
    }
    
    static void OnReceiveSimObjectData(SimConnect sender, SIMCONNECT_RECV_SIMOBJECT_DATA data)
    {
        _simVariableMonitor?.OnSimObjectDataReceived(data);
        _gsxCommunicator?.OnSimObjectDataReceived(data);
    }
    
    static void OnReceiveQuit(SimConnect sender, SIMCONNECT_RECV data)
    {
        Logger.Info("SimConnect connection closed by simulator");
        Logger.SessionEnd();
        _isRunning = false;
    }
    
    static async Task PollForHotkeys()
    {
        bool lastActivationState = false;
        bool lastResetState = false;
        bool lastToggleRefuelState = false;
        
        while (_hotkeyPolling)
        {
            try
            {
                if (_rebindingInProgress || _rebindCooldown > 0)
                {
                    lastActivationState = false;
                    lastResetState = false;
                    lastToggleRefuelState = false;
                    
                    if (_rebindCooldown > 0)
                        _rebindCooldown--;
                    
                    await Task.Delay(KeyPollingDelayMs);
                    continue;
                }
                
                bool activationPressed = _activationHotkey != null && _activationHotkey.KeyCode != 0 
                    && IsHotkeyPressed(_activationHotkey);
                    
                if (lastActivationState && !activationPressed)
                {
                    OnActivationHotkeyPressed();
                }
                
                bool resetPressed = _resetHotkey != null && _resetHotkey.KeyCode != 0 
                    && IsHotkeyPressed(_resetHotkey);
                    
                if (lastResetState && !resetPressed)
                {
                    OnResetHotkeyPressed();
                }
                
                bool toggleRefuelPressed = _toggleRefuelHotkey != null && _toggleRefuelHotkey.KeyCode != 0 
                    && IsHotkeyPressed(_toggleRefuelHotkey);
                    
                if (lastToggleRefuelState && !toggleRefuelPressed)
                {
                    OnToggleRefuelHotkeyPressed();
                }
                
                lastActivationState = activationPressed;
                lastResetState = resetPressed;
                lastToggleRefuelState = toggleRefuelPressed;
            }
            catch { }
            
            await Task.Delay(KeyPollingDelayMs);
        }
    }
    
    static bool IsHotkeyPressed(ParsedHotkey hotkey)
    {
        if ((GetAsyncKeyState(hotkey.KeyCode) & 0x8000) == 0) return false;
        
        if (hotkey.RequiresAlt && (GetAsyncKeyState(VK_MENU) & 0x8000) == 0) return false;
        if (hotkey.RequiresCtrl && (GetAsyncKeyState(VK_CONTROL) & 0x8000) == 0) return false;
        if (hotkey.RequiresShift && (GetAsyncKeyState(VK_SHIFT) & 0x8000) == 0) return false;
        
        return true;
    }
    
    static void OnActivationHotkeyPressed()
    {
        _systemActivated = !_systemActivated;
        _mainForm?.SetSystemStatus(_systemActivated);
        
        if (_systemActivated)
        {
            Logger.Success("SYSTEM ACTIVATED - GSX automation enabled!");
        }
        else
        {
            Logger.Warning($"SYSTEM DEACTIVATED - GSX automation disabled! Press {_config?.Hotkeys.ActivationKey} again to re-activate.");
        }
    }
    
    static void OnResetHotkeyPressed()
    {
        ResetSession();
        Logger.Success("Session reset - ready for turnaround flight");
    }
    
    static void OnToggleRefuelHotkeyPressed()
    {
        string aircraftTitle = _simVariableMonitor?.AircraftTitle ?? "";
        if (string.IsNullOrEmpty(aircraftTitle))
        {
            Logger.Warning("Cannot toggle refuel setting - no aircraft detected");
            return;
        }
        
        var aircraftConfig = ConfigManager.GetAircraftConfig(aircraftTitle);
        aircraftConfig.RefuelBeforeBoarding = !aircraftConfig.RefuelBeforeBoarding;
        ConfigManager.SaveAircraftConfig(aircraftTitle, aircraftConfig);
        
        _mainForm?.UpdateRefuelCheckbox(aircraftConfig.RefuelBeforeBoarding);
        Logger.Success($"Refuel before boarding for '{aircraftTitle}': {(aircraftConfig.RefuelBeforeBoarding ? "ENABLED" : "DISABLED")}");
    }
    
    static void OnBeaconChanged(bool beaconOn)
    {
        Logger.Info($"Beacon light: {(beaconOn ? "ON" : "OFF")}");
        _prevBeaconOn = beaconOn;
    }
    
    static void OnParkingBrakeChanged(bool brakeSet)
    {
        Logger.Info($"Parking brake: {(brakeSet ? "SET" : "RELEASED")}");
        _prevParkingBrakeSet = brakeSet;
    }
    
    static void OnEngineChanged(bool running)
    {
        Logger.Info($"Engine: {(running ? "RUNNING" : "OFF")}");
    }
    
    static void OnAircraftChanged(string aircraftTitle)
    {
        if (!string.IsNullOrEmpty(aircraftTitle))
        {
            _currentAircraft = aircraftTitle;
            var config = ConfigManager.GetAircraftConfig(aircraftTitle);
            _mainForm?.SetCurrentAircraft(aircraftTitle, config.RefuelBeforeBoarding);
        }
    }
    
    static bool IsSystemReady()
    {
        return _systemActivated && _gsxCommunicator != null && _gsxCommunicator.IsGsxRunning();
    }
    
    static bool CanTriggerService(DateTime lastTrigger)
    {
        return (DateTime.Now - lastTrigger).TotalSeconds >= TriggerCooldownSeconds;
    }
    
    static bool HasAircraftMoved()
    {
        return _simVariableMonitor != null && _simVariableMonitor.GetAircraftHasMoved();
    }
    
    static async void OnRefuelingConditions()
    {
        if (!IsSystemReady()) return;
        
        if (HasAircraftMoved())
        {
            if (!_refuelingBlockedLogged)
            {
                Logger.Debug("Refueling blocked - aircraft has moved (flight completed)");
                _refuelingBlockedLogged = true;
            }
            return;
        }
        
        if (_refuelingCompleted) return;
        if (_boardingCompleted) return;
        
        if (_deboardingCompleted)
        {
            if (!_refuelingBlockedLogged)
            {
                Logger.Warning($"Refueling blocked until system is reset! (Use [{_config!.Hotkeys.ResetKey}] to reset for turnaround)");
                _refuelingBlockedLogged = true;
            }
            return;
        }
        
        string aircraftTitle = _simVariableMonitor?.AircraftTitle ?? "";
        if (string.IsNullOrEmpty(aircraftTitle)) return;
        
        var aircraftConfig = ConfigManager.GetAircraftConfig(aircraftTitle);
        if (!aircraftConfig.RefuelBeforeBoarding) return;
        
        if (_gsxCommunicator!.RefuelingState != GsxServiceState.Callable) return;
        if (!CanTriggerService(_lastRefuelingTrigger)) return;
        
        Logger.Info($"Aircraft '{aircraftTitle}' configured for refueling before boarding");
        Logger.Debug("TRIGGER: Refueling conditions met!");
        _lastRefuelingTrigger = DateTime.Now;
        
        await Task.Delay(ServiceTriggerDelayMs);
        await _gsxCommunicator.CallRefueling();
    }
    
    static async void OnBoardingConditions()
    {
        if (!IsSystemReady()) return;
        
        if (HasAircraftMoved())
        {
            if (!_boardingBlockedLogged)
            {
                Logger.Debug("Boarding blocked - aircraft has moved (flight completed)");
                _boardingBlockedLogged = true;
            }
            return;
        }
        
        if (_boardingCompleted)
        {
            if (!_boardingCompletedWarningLogged)
            {
                Logger.Debug($"Boarding blocked - already completed (use [{_config!.Hotkeys.ResetKey}] to reset for turnaround)");
                _boardingCompletedWarningLogged = true;
            }
            return;
        }
        
        if (_deboardingCompleted)
        {
            if (!_deboardingCompletedWarningLogged)
            {
                Logger.Warning($"Boarding blocked until system is reset! (Use [{_config!.Hotkeys.ResetKey}] to reset for turnaround)");
                _deboardingCompletedWarningLogged = true;
            }
            return;
        }
        
        if (_gsxCommunicator!.DeboardingState == GsxServiceState.Active || 
            _gsxCommunicator.DeboardingState == GsxServiceState.Requested)
        {
            return;
        }
        
        if (_gsxCommunicator.DeboardingState == GsxServiceState.Callable && 
            !_deboardingCompleted && 
            _simVariableMonitor != null && 
            _simVariableMonitor.GetEnginesHaveRun())
        {
            return;
        }
        
        string aircraftTitle = _simVariableMonitor?.AircraftTitle ?? "";
        if (!string.IsNullOrEmpty(aircraftTitle))
        {
            var aircraftConfig = ConfigManager.GetAircraftConfig(aircraftTitle);
            
            if (aircraftConfig.RefuelBeforeBoarding)
            {
                if (!_refuelingCompleted && _gsxCommunicator.RefuelingState == GsxServiceState.Unknown)
                {
                    return;
                }
                
                if (_gsxCommunicator.RefuelingState == GsxServiceState.Active ||
                    _gsxCommunicator.RefuelingState == GsxServiceState.Requested)
                {
                    return; 
                }
                
                if (!_refuelingCompleted && _gsxCommunicator.RefuelingState == GsxServiceState.Callable)
                {
                    return; 
                }
            }
        }
        
        if (_gsxCommunicator.BoardingState != GsxServiceState.Callable) return;
        if (!CanTriggerService(_lastBoardingTrigger)) return;
        
        Logger.Debug("TRIGGER: Boarding conditions met!");
        _lastBoardingTrigger = DateTime.Now;
        
        await Task.Delay(ServiceTriggerDelayMs);
        await _gsxCommunicator.CallBoarding();
    }
    
    static async void OnPushbackConditions()
    {
        if (!IsSystemReady()) return;
        
        if (HasAircraftMoved())
        {
            if (!_pushbackBlockedLogged)
            {
                Logger.Debug("Pushback blocked - aircraft has moved");
                _pushbackBlockedLogged = true;
            }
            return;
        }
        
        if (_gsxCommunicator!.PushbackState != GsxServiceState.Callable) return;
        if (_pushbackCompleted) return;
        
        if (_gsxCommunicator.PushbackProgress > 0 && _gsxCommunicator.PushbackProgress < 5) return;
        
        if (_gsxCommunicator.BoardingState == GsxServiceState.Active || 
            _gsxCommunicator.BoardingState == GsxServiceState.Requested)         
        {
            Logger.Debug("Pushback blocked - boarding is ongoing");
            return;
        }
        
        if (_gsxCommunicator.DeboardingState == GsxServiceState.Active || 
            _gsxCommunicator.DeboardingState == GsxServiceState.Requested)
        {
            Logger.Debug("Pushback blocked - deboarding is ongoing");
            return;
        }
        
        if (!CanTriggerService(_lastPushbackTrigger)) return;
        
        Logger.Debug("TRIGGER: Pushback conditions met!");
        _lastPushbackTrigger = DateTime.Now;
        
        await Task.Delay(ServiceTriggerDelayMs);
        bool success = await _gsxCommunicator.CallPushback();
        
        if (!success)
        {
            Logger.Warning("Pushback menu sequence did not activate GSX - likely not at gate");
            _pushbackCompleted = true;
        }
    }
    
    static async void OnDeboardingConditions()
    {
        if (!IsSystemReady()) return;
        
        if (_simVariableMonitor != null && !_simVariableMonitor.GetEnginesHaveRun())
        {
            return;
        }
        
        if (_gsxCommunicator!.DeboardingState != GsxServiceState.Callable) return;
        if (!CanTriggerService(_lastDeboardingTrigger)) return;
        
        Logger.Debug("TRIGGER: Deboarding conditions met!");
        _lastDeboardingTrigger = DateTime.Now;
        
        await Task.Delay(ServiceTriggerDelayMs);
        await _gsxCommunicator.CallDeboarding();
    }
    
    static void ResetSession()
    {
        _deboardingCompleted = false;
        _pushbackCompleted = false;
        _boardingCompleted = false;
        _refuelingCompleted = false;
        _refuelingWasActive = false;
        _boardingBlockedLogged = false;
        _pushbackBlockedLogged = false;
        _boardingCompletedWarningLogged = false;
        _deboardingCompletedWarningLogged = false;
        _refuelingBlockedLogged = false;
        _simVariableMonitor?.ResetEnginesHaveRun();
        _lastBoardingTrigger = DateTime.MinValue;
        _lastPushbackTrigger = DateTime.MinValue;
        _lastDeboardingTrigger = DateTime.MinValue;
        _lastRefuelingTrigger = DateTime.MinValue;
        Logger.Debug("Session state reset.");
    }
    
    static void OnDeboardingStateChanged(GsxServiceState state)
    {
        if (state == GsxServiceState.Completed)
        {
            _deboardingCompleted = true;
            Logger.Debug("Deboarding Completed!");
        }
    }
    
    static void OnPushbackStateChanged(GsxServiceState state)
    {
        if (state == GsxServiceState.Completed)
        {
            _pushbackCompleted = true;
            Logger.Debug("Pushback Completed!");
        }
    }
    
    static void OnBoardingStateChanged(GsxServiceState state)
    {
        if (state == GsxServiceState.Completed)
        {
            _boardingCompleted = true;
            Logger.Debug("Boarding Completed!");
        }
    }
    
    static void OnRefuelingStateChanged(GsxServiceState state)
    {
        if (state == GsxServiceState.Active)
        {
            _refuelingWasActive = true;
        }
        
        if (_refuelingWasActive && state != GsxServiceState.Active && state != GsxServiceState.Requested)
        {
            _refuelingCompleted = true;
            _refuelingWasActive = false;
            Logger.Debug("Refueling Completed!");
        }
    }
    
    public static void UpdateRefuelSetting(string aircraftTitle, bool enabled)
    {
        if (string.IsNullOrEmpty(aircraftTitle)) return;
        
        var config = ConfigManager.GetAircraftConfig(aircraftTitle);
        config.RefuelBeforeBoarding = enabled;
        ConfigManager.SaveAircraftConfig(aircraftTitle, config);
    }
    
    public static void SetRebindingMode(bool rebinding)
    {
        _rebindingInProgress = rebinding;
        if (!rebinding)
        {
            _rebindCooldown = 3;
        }
    }
    
    public static void UpdateHotkey(string hotkeyType, string hotkeyString)
    {
        if (_config == null) return;
        
        if (hotkeyType == "Activation")
        {
            _config.Hotkeys.ActivationKey = hotkeyString;
            _activationHotkey = HotkeyParser.Parse(hotkeyString);
        }
        else if (hotkeyType == "Reset")
        {
            _config.Hotkeys.ResetKey = hotkeyString;
            _resetHotkey = HotkeyParser.Parse(hotkeyString);
        }
        else if (hotkeyType == "ToggleRefuel")
        {
            _config.Hotkeys.ToggleRefuelKey = hotkeyString;
            _toggleRefuelHotkey = HotkeyParser.Parse(hotkeyString);
        }
        
        ConfigManager.Save(_config);
        Logger.Success($"{hotkeyType} hotkey updated to: {hotkeyString}");
    }
    
    public static void PrintCurrentState()
    {
        if (_simVariableMonitor == null || _gsxCommunicator == null)
        {
            Logger.Warning("SimConnect not initialized");
            return;
        }
        
        Logger.Info("=== Current State ===");
        Logger.Info($"Aircraft: {_simVariableMonitor.AircraftTitle}");
        Logger.Info($"Beacon: {(_simVariableMonitor.BeaconLight ? "ON" : "OFF")}, Parking Brake: {(_simVariableMonitor.ParkingBrake ? "SET" : "RELEASED")}, On Ground: {_simVariableMonitor.OnGround}");
        Logger.Info($"Speed: {_simVariableMonitor.GroundSpeed:F1} kts, Engine(s) Running: {_simVariableMonitor.EngineRunning}, Has Moved: {_simVariableMonitor.GetAircraftHasMoved()}");
        Logger.Info($"GSX Running: {_gsxCommunicator.IsGsxRunning()}, Deboarding: {_gsxCommunicator.DeboardingState}, Boarding: {_gsxCommunicator.BoardingState}");
        Logger.Info($"Pushback: {_gsxCommunicator.PushbackState}, Refueling: {_gsxCommunicator.RefuelingState}");
        Logger.Info($"System Active: {_systemActivated}, Deboarding Done: {_deboardingCompleted}, Boarding Done: {_boardingCompleted}");
        Logger.Info("====================");
    }
    
    public static void PrintMovementDebug()
    {
        if (_simVariableMonitor == null)
        {
            Logger.Warning("SimConnect not initialized");
            return;
        }
        
        Logger.Info($"Movement Debug - Speed: {_simVariableMonitor.GroundSpeed:F1} kts, EnginesRun: {_simVariableMonitor.GetEnginesHaveRun()}, HasMoved: {_simVariableMonitor.GetAircraftHasMoved()}");
    }
    
    public static void ToggleMovementFlag()
    {
        if (_simVariableMonitor == null)
        {
            Logger.Warning("SimConnect not initialized");
            return;
        }
        
        bool currentState = _simVariableMonitor.GetAircraftHasMoved();
        if (currentState)
        {
            _simVariableMonitor.ResetAircraftHasMoved();
            Logger.Success("Aircraft movement flag CLEARED - boarding/pushback enabled");
        }
        else
        {
            _simVariableMonitor.SetAircraftHasMoved();
            Logger.Success("Aircraft movement flag SET - boarding/pushback blocked");
        }
    }
    
    static bool IsMsfsRunning()
    {
        var msfsProcesses = System.Diagnostics.Process.GetProcesses()
            .Where(p => p.ProcessName.Contains("FlightSimulator", StringComparison.OrdinalIgnoreCase) || 
                       p.ProcessName.Contains("MSFS", StringComparison.OrdinalIgnoreCase))
            .ToArray();
        
        return msfsProcesses.Length > 0;
    }
    
    static async Task MonitorMsfsProcess()
    {
        _monitoringMsfs = true;
        
        while (_monitoringMsfs && _isRunning)
        {
            await Task.Delay(5000);
            
            var msfsProcesses = System.Diagnostics.Process.GetProcesses()
                .Where(p => p.ProcessName.Contains("FlightSimulator", StringComparison.OrdinalIgnoreCase) || 
                           p.ProcessName.Contains("MSFS", StringComparison.OrdinalIgnoreCase))
                .ToArray();
            
            if (msfsProcesses.Length == 0)
            {
                Logger.Info("MSFS has closed. Exiting...");
                _isRunning = false;
                Environment.Exit(0);
            }
        }
    }
    
    // Windows API imports
    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int vKey);
}
