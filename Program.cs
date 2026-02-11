using Microsoft.FlightSimulator.SimConnect;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace SimpleGsxIntegrator;

internal enum DEFINITIONS : uint
{
    AircraftState = 1,
    GsxVar = 2,
    GsxVarRead = 3,
    GsxMenuOpenWrite = 4,
    GsxMenuChoiceWrite = 5,
    ActivationVarRead = 6,
    PmdgVar737 = 7,
    PmdgVar777 = 8
}

internal enum DATA_REQUESTS : uint
{
    AircraftState = 1,
    GsxVar = 2,
    GsxVarRead = 3,
    ActivationVarRead = 4,
    PmdgVar737 = 5,
    PmdgVar777 = 6
}

internal enum REQUESTS : uint
{
    AircraftLoaded = 1
}

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
    private static Pmdg737Controller? _pmdg737Controller;
    private static Pmdg777Controller? _pmdg777Controller;
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

    // These variables are used as internal vars, in case GSX restarts we still know if the service were completed or not, to prevent unwanted triggers
    private static bool _deboardingCompleted = false;
    private static bool _pushbackCompleted = false;
    private static bool _boardingCompleted = false;
    private static bool _refuelingCompleted = false;
    private static bool _cateringCompleted = false;
    private static bool _checkedInitialGsxStates = false;

    private static DateTime _lastBoardingTrigger = DateTime.MinValue;
    private static DateTime _lastPushbackTrigger = DateTime.MinValue;
    private static DateTime _lastDeboardingTrigger = DateTime.MinValue;
    private static DateTime _lastRefuelingTrigger = DateTime.MinValue;
    private static DateTime _lastCateringTrigger = DateTime.MinValue;

    private static bool _boardingBlockedLogged = false;
    private static bool _pushbackBlockedLogged = false;
    private static bool _boardingCompletedWarningLogged = false;
    private static bool _deboardingCompletedWarningLogged = false;
    private static bool _refuelingBlockedLogged = false;
    private static bool _cateringBlockedLogged = false;
    private static bool _isInTurnaround = false;

    private static string _prevAircraftTitle = "";
    private static bool _isPmdg737 = false;
    public static bool IsPmdg737 => _isPmdg737;
    private static bool _isPmdg777 = false;
    public static bool IsPmdg777 => _isPmdg777;
    private static Mutex? _instanceMutex;
    private static double _lastActivationLvarValue = double.NaN;

    [STAThread]
    static void Main(string[] args)
    {
        try
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

            _mainForm = new MainForm();
            Logger.MainForm = _mainForm;
            _mainForm.SetHotkeys(_config.Hotkeys.ActivationKey, _config.Hotkeys.ResetKey);

            _ = Task.Run(InitializeAsync);

            Application.Run(_mainForm);
        }
        catch (Exception ex)
        {
            Logger.Error($"UNHANDLED EXCEPTION in Main: {ex.Message}");
            Logger.Error($"Stack: {ex.StackTrace}");
            throw;
        }
    }

    static async Task InitializeAsync()
    {
        try
        {
            _windowHandle = _mainForm!.Handle;

            _ = Task.Run(PollForHotkeys);
            Logger.Debug($"Registered global hotkeys: {_config!.Hotkeys.ActivationKey} (activate), {_config!.Hotkeys.ResetKey} (reset)");

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

            AircraftControllerRegistry.RegisterDefaults();

            _simVariableMonitor.BeaconChanged += OnBeaconChanged;
            _simVariableMonitor.ParkingBrakeChanged += OnParkingBrakeChanged;
            _simVariableMonitor.EngineChanged += OnEngineChanged;
            _simVariableMonitor.AircraftChanged += OnAircraftChanged;
            _simVariableMonitor.ActivationVarReceived += OnActivationLvarReceived;
            _simVariableMonitor.RefuelingConditionsMet += OnRefuelingConditions;
            _simVariableMonitor.CateringConditionsMet += OnCateringConditions;
            _simVariableMonitor.BoardingConditionsMet += OnBoardingConditions;
            _simVariableMonitor.PushbackConditionsMet += OnPushbackConditions;
            _simVariableMonitor.DeboardingConditionsMet += OnDeboardingConditions;

            _gsxCommunicator.DeboardingStateChanged += OnDeboardingStateChanged;
            _gsxCommunicator.PushbackStateChanged += OnPushbackStateChanged;
            _gsxCommunicator.BoardingStateChanged += OnBoardingStateChanged;
            _gsxCommunicator.RefuelingStateChanged += OnRefuelingStateChanged;
            _gsxCommunicator.CateringStateChanged += OnCateringStateChanged;
            _gsxCommunicator.GsxStarted += () => _mainForm?.SetGsxStatus(true);
            _gsxCommunicator.GsxStopped += () => _mainForm?.SetGsxStatus(false);

            Logger.Info($"SYSTEM STATUS: {(_systemActivated ? "ACTIVATED" : $"STANDBY - Press {_config!.Hotkeys.ActivationKey} to activate")}");
            _mainForm?.SetSystemStatus(_systemActivated);

            _simConnect.OnRecvSimobjectData += OnReceiveSimObjectData;
            _simConnect.OnRecvSystemState += OnReceiveSystemState;
            _simConnect.OnRecvQuit += OnReceiveQuit;

            _simConnect.RequestSystemState(REQUESTS.AircraftLoaded, "AircraftLoaded");

            CheckInitialGsxStates();
            if (_systemActivated && _gsxCommunicator != null)
            {
                OnDeboardingConditions();
                OnBoardingConditions();
                OnPushbackConditions();
                OnRefuelingConditions();
                OnCateringConditions();
            }

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
        _pmdg737Controller?.OnSimObjectDataReceived(data);
        _pmdg777Controller?.OnSimObjectDataReceived(data);
    }

    // Receives full aircraft path when aircraft is changed
    static void OnReceiveSystemState(SimConnect sender, SIMCONNECT_RECV_SYSTEM_STATE data)
    {
        if (data.dwRequestID != (uint)REQUESTS.AircraftLoaded) return;

        string aircraftPath = data.szString ?? string.Empty;

        try { _pmdg737Controller?.Dispose(); } catch { }
        try { _pmdg777Controller?.Dispose(); } catch { }
        _pmdg737Controller = null;
        _pmdg777Controller = null;

        if (_simConnect == null) return;

        var controller = AircraftControllerRegistry.CreateAircraftController(aircraftPath, _simConnect, _simVariableMonitor);
        if (controller != null)
        {
            controller.Connect();

            if (controller is Pmdg737Controller pmdg737Controller) _pmdg737Controller = pmdg737Controller;
            else if (controller is Pmdg777Controller pmdg777Controller) _pmdg777Controller = pmdg777Controller;
        }

        _isPmdg737 = _pmdg737Controller != null;
        _isPmdg777 = _pmdg777Controller != null;

        if (_prevAircraftTitle != _simVariableMonitor?.AircraftState.AircraftTitle
            && !string.IsNullOrEmpty(_simVariableMonitor?.AircraftState.AircraftTitle) && !string.IsNullOrEmpty(_prevAircraftTitle))
        {
            Logger.Info($"Aircraft changed: {_simVariableMonitor?.AircraftState.AircraftTitle}");
            ResetSession();
        }

        _prevAircraftTitle = _simVariableMonitor?.AircraftState.AircraftTitle ?? "";
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

        while (_hotkeyPolling)
        {
            try
            {
                if (_rebindingInProgress || _rebindCooldown > 0)
                {
                    lastActivationState = false;
                    lastResetState = false;

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

                lastActivationState = activationPressed;
                lastResetState = resetPressed;
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

        CheckInitialGsxStates();

        if (_systemActivated && _gsxCommunicator != null)
        {
            OnDeboardingStateChanged(_gsxCommunicator.DeboardingState);
            OnBoardingStateChanged(_gsxCommunicator.BoardingState, true);
            OnPushbackStateChanged(_gsxCommunicator.PushbackState);
            OnRefuelingStateChanged(_gsxCommunicator.RefuelingState);
            OnCateringStateChanged(_gsxCommunicator.CateringState);
        }
    }

    static void OnResetHotkeyPressed()
    {
        ResetSession();
    }

    static void OnBeaconChanged(bool beaconOn)
    {
        Logger.Info($"Beacon light: {(beaconOn ? "ON" : "OFF")}");
    }

    static void OnParkingBrakeChanged(bool brakeSet)
    {
        Logger.Info($"Parking brake: {(brakeSet ? "SET" : "RELEASED")}");
    }

    static void OnEngineChanged(bool running)
    {
        Logger.Info($"Engine: {(running ? "RUNNING" : "OFF")}");
    }

    static void OnAircraftChanged(string aircraftTitle)
    {
        if (!string.IsNullOrEmpty(aircraftTitle))
        {
            var config = ConfigManager.GetAircraftConfig(aircraftTitle);
            _mainForm?.SetCurrentAircraft(aircraftTitle, config.RefuelBeforeBoarding);

            _simConnect?.RequestSystemState(REQUESTS.AircraftLoaded, "AircraftLoaded");

            try
            {
                if (_simVariableMonitor != null && !string.IsNullOrEmpty(config.ActivationLvar))
                {
                    _simVariableMonitor.RegisterActivationLvar(config.ActivationLvar);
                    _lastActivationLvarValue = double.NaN;
                    Logger.Debug($"Activation L:var for '{aircraftTitle}' set to '{config.ActivationLvar}' value {config.ActivationValue}");
                }
            }
            catch (Exception ex)
            {
                Logger.Warning($"Failed to register activation L:var: {ex.Message}");
            }
        }
    }

    public static void RegisterActivationForCurrentAircraft()
    {
        try
        {
            string aircraftTitle = _simVariableMonitor?.AircraftState.AircraftTitle ?? "";
            if (string.IsNullOrEmpty(aircraftTitle)) return;

            var cfg = ConfigManager.GetAircraftConfig(aircraftTitle);
            if (!string.IsNullOrEmpty(cfg.ActivationLvar) && _simVariableMonitor != null)
            {
                _simVariableMonitor.RegisterActivationLvar(cfg.ActivationLvar);
                _lastActivationLvarValue = double.NaN;
                Logger.Debug($"Activation L:var for '{aircraftTitle}' registered as '{cfg.ActivationLvar}' (value {cfg.ActivationValue})");
            }
        }
        catch (Exception ex)
        {
            Logger.Warning($"Failed to register activation L:var from UI: {ex.Message}");
        }
    }

    static void OnActivationLvarReceived(double value)
    {
        string aircraftTitle = _simVariableMonitor?.AircraftState.AircraftTitle ?? "";
        if (string.IsNullOrEmpty(aircraftTitle)) return;

        var cfg = ConfigManager.GetAircraftConfig(aircraftTitle);
        if (string.IsNullOrEmpty(cfg.ActivationLvar)) return;

        if (double.IsNaN(_lastActivationLvarValue))
        {
            _lastActivationLvarValue = value;
            return;
        }

        if (value == cfg.ActivationValue && _lastActivationLvarValue != cfg.ActivationValue)
        {
            OnActivationHotkeyPressed();
        }

        _lastActivationLvarValue = value;
        CheckInitialGsxStates();
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

    static int GetTurnaroundDelaySeconds()
    {
        string aircraftTitle = _simVariableMonitor?.AircraftState.AircraftTitle ?? "";
        if (string.IsNullOrEmpty(aircraftTitle)) return 60;

        var aircraftConfig = ConfigManager.GetAircraftConfig(aircraftTitle);
        return aircraftConfig.TurnaroundDelaySeconds;
    }

    static void CheckInitialGsxStates()
    {
        if (_checkedInitialGsxStates)
        {
            return;
        }

        _checkedInitialGsxStates = true;

        if (_gsxCommunicator == null) return;

        if (_gsxCommunicator.DeboardingState == GsxServiceState.Completed && !_deboardingCompleted)
        {
            Logger.Debug("Detected deboarding already completed - setting internal state");
            _deboardingCompleted = true;

            string aircraftTitle = _simVariableMonitor?.AircraftState.AircraftTitle ?? "";
            if (!string.IsNullOrEmpty(aircraftTitle))
            {
                var aircraftConfig = ConfigManager.GetAircraftConfig(aircraftTitle);
                if (aircraftConfig.AutoCallTurnaroundServices)
                {
                    StartTurnaround(aircraftConfig);
                }
            }
        }

        if (_gsxCommunicator.PushbackState == GsxServiceState.Completed && !_pushbackCompleted)
        {
            Logger.Debug("Detected pushback already completed - setting internal state");
            _pushbackCompleted = true;
            _simVariableMonitor?.SetPushbackCompleted();
        }

        if (_gsxCommunicator.BoardingState == GsxServiceState.Completed && !_boardingCompleted)
        {
            Logger.Debug("Detected boarding already completed - setting internal state");
            _boardingCompleted = true;
        }

        if (_gsxCommunicator.RefuelingState == GsxServiceState.Completed && !_refuelingCompleted)
        {
            Logger.Debug("Detected refueling already completed - setting internal state");
            _refuelingCompleted = true;
        }

        if (_gsxCommunicator.CateringState == GsxServiceState.Completed && !_cateringCompleted)
        {
            Logger.Debug("Detected catering already completed - setting internal state");
            _cateringCompleted = true;
        }
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

        if (_deboardingCompleted && !_isInTurnaround)
        {
            if (!_refuelingBlockedLogged)
            {
                int delaySeconds = GetTurnaroundDelaySeconds();
                Logger.Debug($"Refueling blocked - waiting for turnaround delay ({delaySeconds}s after deboarding)");
                _refuelingBlockedLogged = true;
            }
            return;
        }

        string aircraftTitle = _simVariableMonitor?.AircraftState.AircraftTitle ?? "";
        if (string.IsNullOrEmpty(aircraftTitle)) return;

        var aircraftConfig = ConfigManager.GetAircraftConfig(aircraftTitle);
        if (!aircraftConfig.RefuelBeforeBoarding) return;

        if (_gsxCommunicator!.RefuelingState != GsxServiceState.Callable) return;
        if (!CanTriggerService(_lastRefuelingTrigger)) return;

        Logger.Debug($"Aircraft '{aircraftTitle}' configured for refueling before boarding");
        Logger.Debug("TRIGGER: Refueling conditions met!");
        _lastRefuelingTrigger = DateTime.Now;

        await Task.Delay(ServiceTriggerDelayMs);
        await _gsxCommunicator.CallRefueling();
    }

    static async void OnCateringConditions()
    {
        if (!IsSystemReady()) return;

        if (HasAircraftMoved()) return;

        if (_cateringCompleted) return;
        if (_boardingCompleted) return;

        string aircraftTitle = _simVariableMonitor?.AircraftState.AircraftTitle ?? "";
        if (string.IsNullOrEmpty(aircraftTitle)) return;

        var aircraftConfig = ConfigManager.GetAircraftConfig(aircraftTitle);

        if (!_deboardingCompleted && aircraftConfig.CateringOnNewFlight)
        {
            if (_gsxCommunicator!.CateringState != GsxServiceState.Callable) return;
            if (!CanTriggerService(_lastCateringTrigger)) return;

            Logger.Debug($"Aircraft '{aircraftTitle}' configured for catering on new flight");
            Logger.Debug("TRIGGER: Catering conditions met!");
            _lastCateringTrigger = DateTime.Now;

            await Task.Delay(ServiceTriggerDelayMs);
            await _gsxCommunicator.CallCatering();
            return;
        }

        if (_deboardingCompleted && !_isInTurnaround)
        {
            if (!_cateringBlockedLogged && aircraftConfig.CateringOnTurnaround)
            {
                int delaySeconds = aircraftConfig.TurnaroundDelaySeconds;
                Logger.Debug($"Catering blocked - waiting for turnaround delay ({delaySeconds}s after deboarding)");
                _cateringBlockedLogged = true;
            }
            return;
        }

        if (_deboardingCompleted && _isInTurnaround && aircraftConfig.CateringOnTurnaround)
        {
            if (_gsxCommunicator!.CateringState != GsxServiceState.Callable) return;
            if (!CanTriggerService(_lastCateringTrigger)) return;

            Logger.Debug($"Aircraft '{aircraftTitle}' configured for catering on turnaround");
            Logger.Debug("TRIGGER: Catering conditions met!");
            _lastCateringTrigger = DateTime.Now;

            await Task.Delay(ServiceTriggerDelayMs);
            await _gsxCommunicator.CallCatering();
        }
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

        if (_deboardingCompleted && !_isInTurnaround)
        {
            if (!_deboardingCompletedWarningLogged)
            {
                int delaySeconds = GetTurnaroundDelaySeconds();
                Logger.Debug($"Boarding blocked - waiting for turnaround delay ({delaySeconds}s after deboarding)");
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

        string aircraftTitle = _simVariableMonitor?.AircraftState.AircraftTitle ?? "";
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

            if (!_deboardingCompleted && aircraftConfig.CateringOnNewFlight)
            {
                if (!_cateringCompleted && _gsxCommunicator.CateringState == GsxServiceState.Unknown)
                {
                    return;
                }

                if (_gsxCommunicator.CateringState == GsxServiceState.Active ||
                    _gsxCommunicator.CateringState == GsxServiceState.Requested)
                {
                    return;
                }

                if (!_cateringCompleted && _gsxCommunicator.CateringState == GsxServiceState.Callable)
                {
                    return;
                }
            }

            if (_deboardingCompleted && aircraftConfig.CateringOnTurnaround)
            {
                if (!_cateringCompleted && _gsxCommunicator.CateringState == GsxServiceState.Unknown)
                {
                    return;
                }

                if (_gsxCommunicator.CateringState == GsxServiceState.Active ||
                    _gsxCommunicator.CateringState == GsxServiceState.Requested)
                {
                    return;
                }

                if (!_cateringCompleted && _gsxCommunicator.CateringState == GsxServiceState.Callable)
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

        string aircraftTitle = _simVariableMonitor?.AircraftState.AircraftTitle ?? "";
        var aircraftConfig = ConfigManager.GetAircraftConfig(aircraftTitle);

        if (_isPmdg737 && _pmdg737Controller != null && _systemActivated)
        {
            _ = Task.Run(async () =>
            {
                await Task.Delay(2000);

                if (aircraftConfig.AutoCloseDoors && _systemActivated)
                {
                    await _pmdg737Controller.CloseOpenDoors();
                    await Task.Delay(1000);
                    await _pmdg737Controller.RemoveGroundEquipment();
                }
            });
        }
        else if (_isPmdg777 && _pmdg777Controller != null && _systemActivated)
        {
            _ = Task.Run(async () =>
            {
                await Task.Delay(2000);

                if (aircraftConfig.AutoCloseDoors && _systemActivated)
                {
                    await _pmdg777Controller.CloseOpenDoors();
                    await Task.Delay(1000);
                    await _pmdg777Controller.RemoveGroundEquipment();
                }
            });
        }
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

        if (_simVariableMonitor != null)
        {
            bool enginesHaveRun = _simVariableMonitor.GetEnginesHaveRun();
            bool aircraftHasMoved = _simVariableMonitor.GetAircraftHasMoved();

            if (!enginesHaveRun && !aircraftHasMoved)
            {
                return;
            }
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
        _cateringCompleted = false;
        _boardingBlockedLogged = false;
        _pushbackBlockedLogged = false;
        _boardingCompletedWarningLogged = false;
        _deboardingCompletedWarningLogged = false;
        _refuelingBlockedLogged = false;
        _cateringBlockedLogged = false;
        _simVariableMonitor?.ResetEnginesHaveRun();
        _lastBoardingTrigger = DateTime.MinValue;
        _lastPushbackTrigger = DateTime.MinValue;
        _lastDeboardingTrigger = DateTime.MinValue;
        _lastRefuelingTrigger = DateTime.MinValue;
        _lastCateringTrigger = DateTime.MinValue;
        Logger.Success("Session Reset!");
    }

    static void OnDeboardingStateChanged(GsxServiceState state)
    {
        if (_gsxCommunicator == null || !_gsxCommunicator.IsGsxRunning())
        {
            return;
        }

        if (state == GsxServiceState.Active)
        {
            Logger.Success($"Deboarding ACTIVATED");
        }
        else if (state == GsxServiceState.Completed)
        {
            _deboardingCompleted = true;
            Logger.Debug("Deboarding Completed!");

            string aircraftTitle = _simVariableMonitor?.AircraftState.AircraftTitle ?? "";
            if (string.IsNullOrEmpty(aircraftTitle)) return;

            var aircraftConfig = ConfigManager.GetAircraftConfig(aircraftTitle);
            int delaySeconds = aircraftConfig.TurnaroundDelaySeconds;

            if (aircraftConfig.AutoCallTurnaroundServices)
            {
                Logger.Info($"Turnaround delay started - services will be called automatically in {delaySeconds} seconds");
            }
            else
            {
                Logger.Info($"Turnaround delay started - services will be available in {delaySeconds} seconds");
            }

            if (_systemActivated) StartTurnaround(aircraftConfig);
        }
        else if (state != GsxServiceState.Bypassed)
        {
            Logger.Debug($"Deboarding state: {state}");
        }
    }

    static void OnRefuelingStateChanged(GsxServiceState state)
    {
        if (_gsxCommunicator == null || !_gsxCommunicator.IsGsxRunning())
        {
            return;
        }

        if (state == GsxServiceState.Requested)
        {
            Logger.Success($"Refueling REQUESTED");
        }
        else if (state == GsxServiceState.Active)
        {
            Logger.Success($"Refueling ACTIVATED");
        }
        else if (state == GsxServiceState.Completed)
        {
            _refuelingCompleted = true;
            Logger.Success("Refueling COMPLETED");
        }
        else
        {
            Logger.Debug($"Refueling state: {state}");
        }
    }

    static void OnCateringStateChanged(GsxServiceState state)
    {
        if (_gsxCommunicator == null || !_gsxCommunicator.IsGsxRunning())
        {
            return;
        }

        if (state == GsxServiceState.Requested)
        {
            Logger.Success($"Catering REQUESTED");
        }
        else if (state == GsxServiceState.Active)
        {
            Logger.Success($"Catering ACTIVATED");
        }
        else if (state == GsxServiceState.Completed)
        {
            _cateringCompleted = true;
            Logger.Success("Catering COMPLETED");
        }
        else
        {
            Logger.Debug($"Catering state: {state}");
        }
    }

    private static void StartTurnaround(AircraftConfig aircraftConfig)
    {
        int delaySeconds = aircraftConfig.TurnaroundDelaySeconds;
        Logger.Info($"Starting turnaround delay timer ({delaySeconds}s) - services will be called automatically");

        _ = Task.Run(async () =>
        {
            await Task.Delay(delaySeconds * 1000);
            _isInTurnaround = true;

            _refuelingCompleted = false;
            _cateringCompleted = false;
            _boardingCompleted = false;
            _boardingBlockedLogged = false;
            _refuelingBlockedLogged = false;
            _cateringBlockedLogged = false;
            _boardingCompletedWarningLogged = false;

            if (aircraftConfig.AutoCallTurnaroundServices)
            {
                Logger.Success("Turnaround services starting now!");

                if (aircraftConfig.RefuelBeforeBoarding && !HasAircraftMoved() &&
                    _gsxCommunicator!.RefuelingState == GsxServiceState.Callable)
                {
                    _lastRefuelingTrigger = DateTime.Now;
                    await Task.Delay(ServiceTriggerDelayMs);
                    await _gsxCommunicator.CallRefueling();
                }

                if (aircraftConfig.CateringOnTurnaround &&
                    _gsxCommunicator!.CateringState == GsxServiceState.Callable)
                {
                    _lastCateringTrigger = DateTime.Now;
                    await Task.Delay(ServiceTriggerDelayMs);
                    await _gsxCommunicator.CallCatering();
                }
            }
            else
            {
                Logger.Success("Turnaround services now available!");
            }
        });
    }

    static void OnPushbackStateChanged(GsxServiceState state)
    {
        if (_gsxCommunicator == null || !_gsxCommunicator.IsGsxRunning())
        {
            return;
        }

        if (state == GsxServiceState.Requested)
        {
            Logger.Success($"Pushback REQUESTED");
        }
        else if (state == GsxServiceState.Active)
        {
            Logger.Success($"Pushback ACTIVATED");
        }
        else if (state == GsxServiceState.Completed)
        {
            _pushbackCompleted = true;
            _simVariableMonitor?.SetPushbackCompleted();
            Logger.Success("Pushback COMPLETED");
        }
        else
        {
            Logger.Debug($"Pushback state: {state}");
        }
    }

    static void OnBoardingStateChanged(GsxServiceState state)
    {
        OnBoardingStateChanged(state, false);
    }

    static void OnBoardingStateChanged(GsxServiceState state, bool forceActions)
    {
        if (_gsxCommunicator == null || !_gsxCommunicator.IsGsxRunning())
        {
            return;
        }

        if (state == GsxServiceState.Active)
        {
            Logger.Success($"Boarding ACTIVATED");
        }
        else if (state == GsxServiceState.Requested)
        {
            Logger.Success($"Boarding REQUESTED");
        }
        else if (state == GsxServiceState.Completed)
        {
            if (!_boardingCompleted)
            {
                _boardingCompleted = true;
                Logger.Success($"Boarding COMPLETED");
                RunBoardingCompletedActions();
            }
            else
            {
                if (forceActions && _systemActivated)
                {
                    RunBoardingCompletedActions();
                }
                else
                {
                    Logger.Debug("Boarding already completed - ignoring repeated completion event");
                }
            }
        }
        else if (state != GsxServiceState.Bypassed)
        {
            Logger.Debug($"Boarding state: {state}");
        }
    }

    static void RunBoardingCompletedActions()
    {
        if (!_systemActivated) return;

        _ = Task.Run(async () =>
        {
            await PerformBoardingCloseActions();
        });
    }

    static async Task PerformBoardingCloseActions()
    {
        await Task.Delay(2000);

        string aircraftTitle = _simVariableMonitor?.AircraftState.AircraftTitle ?? "";
        var aircraftConfig = ConfigManager.GetAircraftConfig(aircraftTitle);

        if (!aircraftConfig.AutoCloseDoors) return;

        if (_isPmdg737 && _pmdg737Controller != null)
        {
            await _pmdg737Controller.CloseOpenDoors();
        }
        else if (_isPmdg777 && _pmdg777Controller != null)
        {
            if (!_pmdg777Controller.IsConnected)
            {
                try { _pmdg777Controller.Connect(); } catch { }
            }

            await _pmdg777Controller.CloseOpenDoors();
        }
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
        Logger.Info($"Aircraft: {_simVariableMonitor.AircraftState.AircraftTitle}");
        Logger.Info($"Beacon: {(_simVariableMonitor.AircraftState.BeaconLight != 0 ? "ON" : "OFF")}, Parking Brake: {(_simVariableMonitor.AircraftState.ParkingBrake != 0 ? "SET" : "RELEASED")}, On Ground: {_simVariableMonitor.AircraftState.OnGround}");
        Logger.Info($"Speed: {_simVariableMonitor.AircraftState.GroundSpeed:F1} kts, Engine(s) Running: {_simVariableMonitor.AircraftState.EngineRunning != 0}, Has Moved: {_simVariableMonitor.GetAircraftHasMoved()}");
        Logger.Info($"GSX Running: {_gsxCommunicator.IsGsxRunning()}, Deboarding: {_gsxCommunicator.DeboardingState}, Boarding: {_gsxCommunicator.BoardingState}");
        Logger.Info($"Pushback: {_gsxCommunicator.PushbackState}, Refueling: {_gsxCommunicator.RefuelingState}, Catering: {_gsxCommunicator.CateringState}");
        Logger.Info($"System Active: {_systemActivated}, Pushback Done: {_pushbackCompleted}, Deboarding Done: {_deboardingCompleted}, Boarding Done: {_boardingCompleted}, Catering Done: {_cateringCompleted}");
        Logger.Info("====================");
    }

    public static void PrintMovementDebug()
    {
        if (_simVariableMonitor == null)
        {
            Logger.Warning("SimConnect not initialized");
            return;
        }

        Logger.Info($"Movement Debug - Speed: {_simVariableMonitor.AircraftState.GroundSpeed:F1} kts, EnginesRun: {_simVariableMonitor.GetEnginesHaveRun()}, HasMoved: {_simVariableMonitor.GetAircraftHasMoved()}");
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
