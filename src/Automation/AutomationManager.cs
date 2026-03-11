using System.Security.Cryptography;
using Microsoft.FlightSimulator.SimConnect;
using SimpleGsxIntegrator.Aircraft;
using SimpleGsxIntegrator.Config;
using SimpleGsxIntegrator.Core;
using SimpleGsxIntegrator.Gsx;

namespace SimpleGsxIntegrator.Automation;

public sealed class AutomationManager
{
    private readonly FlightStateTracker _flightState;
    private readonly GsxMonitor _gsxMonitor;
    private readonly GsxMenuController _gsxMenu;

    private IAircraftAdapter? _currentAdapter;

    private bool _activated;
    private string? _currentAircraftTitle;
    private bool _refuelingDone;
    private bool _cateringDone;
    private bool _boardingDone;
    private bool _pushbackDone;
    private bool _deboardingDone;

    /// Set once pushback has been attempted.
    /// This aims to stop boarding being called if user forgot to turn on APU
    /// and loses power, causing the beacon to turn OFF
    /// (can only occur if boarding was not performed)
    private bool _pushbackAttempted;

    private SimConnect? _sc;

    public event Action<bool>? ActivationChanged;
    public event Action<string>? AircraftChanged;

    public AutomationManager(
        FlightStateTracker flightState,
        GsxMonitor gsxMonitor,
        GsxMenuController gsxMenu)
    {
        _flightState = flightState;
        _gsxMonitor = gsxMonitor;
        _gsxMenu = gsxMenu;

        SetupEvents();
    }

    public bool IsActivated
    {
        get { return _activated; }
    }

    public void OnSimConnectConnected(SimConnect sc)
    {
        _sc = sc;
    }

    public IAircraftAdapter? CurrentAdapter
    {
        get { return _currentAdapter; }
    }

    public void SetCurrentAdapter(IAircraftAdapter? adapter)
    {
        _currentAdapter = adapter;
    }

    public void ToggleActivation()
    {
        _activated = !_activated;
        Logger.Debug(_activated
            ? "SYSTEM ACTIVATED - GSX automation enabled"
            : "SYSTEM DEACTIVATED - GSX automation disabled");

        ActivationChanged?.Invoke(_activated);

        if (_activated)
        {
            SyncInitialGsxStates();
            EvaluateServices();
        }
    }

    public void ResetSession(bool printLog = true)
    {
        _refuelingDone = false;
        _cateringDone = false;
        _boardingDone = false;
        _pushbackDone = false;
        _deboardingDone = false;
        _pushbackAttempted = false;

        _flightState.ResetSession();
        if (printLog)
            Logger.Success("Session reset - all service flags cleared");
    }

    private void SetupEvents()
    {
        _flightState.BeaconChanged += OnBeaconChanged;
        _flightState.ParkingBrakeChanged += OnParkingBrakeChanged;
        _flightState.AircraftChanged += OnAircraftChanged;
        _flightState.ActivationLvarTriggered += OnActivationLvarTriggered;
        _flightState.EngineChanged += OnEngineChanged;
        _flightState.SpawnedAtGate += OnSpawnedAtGate;
        _flightState.MenuStateChanged += OnMenuStateChanged;

        _gsxMonitor.GsxStarted += OnGsxStarted;
        _gsxMonitor.GsxStopped += OnGsxStopped;
        _gsxMonitor.BoardingStateChanged += OnBoardingStateChanged;
        _gsxMonitor.DeboardingStateChanged += OnDeboardingStateChanged;
        _gsxMonitor.PushbackStateChanged += OnPushbackStateChanged;
        _gsxMonitor.RefuelingStateChanged += OnRefuelingStateChanged;
        _gsxMonitor.CateringStateChanged += OnCateringStateChanged;
    }

    private void OnBeaconChanged(bool beaconOn)
    {
        if (!_activated || !_gsxMonitor.IsGsxRunning) return;
        EvaluateServices();
    }

    private void OnParkingBrakeChanged(bool brakeSet)
    {
        if (!_activated || !_gsxMonitor.IsGsxRunning) return;
        EvaluateServices();
    }

    private async void OnSpawnedAtGate()
    {
        if (_currentAdapter == null) return;
        var adapter = _currentAdapter;
        await Task.Delay(10_000);
        Logger.Debug($"AutomationManager: Spawned at Gate, Calling OnSpawned on '{adapter.GetType().Name}'");
        await adapter.OnSpawned();
    }

    private void OnMenuStateChanged()
    {
        if (_flightState.IsInMenu)
        {
            Logger.Debug("AutomationManager: Entered Menu - Resetting Session");
            ResetSession(printLog: false);
        }
    }

    private void OnAircraftChanged(string title)
    {
        Logger.Debug($"Aircraft changed: {title}");

        if (_currentAircraftTitle != null)
        {
            if (_activated) ToggleActivation();
            ResetSession(printLog: false);
        }
        _currentAircraftTitle = title;

        var cfg = ConfigManager.GetAircraftConfig(title);
        _gsxMenu.SetLiveryName(_flightState.LiveryName);
        AircraftChanged?.Invoke(title);

        if (_sc != null && !string.IsNullOrEmpty(cfg.ActivationLvar))
        {
            _flightState.SetActivationLvar(_sc, cfg.ActivationLvar);
            Logger.Debug($"AutomationManager: activation L:var set to '{cfg.ActivationLvar}' (trigger at {cfg.ActivationValue})");
        }
    }

    private void OnActivationLvarTriggered(double value)
    {
        var cfg = ConfigManager.GetAircraftConfig(_flightState.AircraftTitle);

        if (string.IsNullOrEmpty(cfg.ActivationLvar)) return;
        if (Math.Abs(value - cfg.ActivationValue) < 0.001)
        {
            Logger.Debug($"AutomationManager: activation L:var hit target value {value} - toggling system");
            ToggleActivation();
        }
    }

    private void OnEngineChanged(bool engineOn)
    {
        if (!_activated || !_gsxMonitor.IsGsxRunning) return;
        EvaluateServices();
    }

    private void OnGsxStarted()
    {
        Logger.Debug("GSX is running");
        SyncInitialGsxStates();
        if (_activated)
            EvaluateServices();
    }

    private void OnGsxStopped()
    {
        Logger.Debug("GSX stopped - automation paused until GSX restarts");
    }

    private void OnBoardingStateChanged(GsxServiceState state)
    {
        switch (state)
        {
            case GsxServiceState.Requested:
                Logger.Success("Boarding: Requested");
                if (_currentAdapter != null) _ = _currentAdapter.OnBoardingRequested();
                break;
            case GsxServiceState.Active:
                Logger.Success("Boarding: Active");
                break;
            case GsxServiceState.Completed when !_boardingDone:
                _boardingDone = true;
                Logger.Success("Boarding: Complete");
                if (_currentAdapter != null) _ = _currentAdapter.OnBoardingCompleted();
                break;
        }
    }

    private void OnDeboardingStateChanged(GsxServiceState state)
    {
        switch (state)
        {
            case GsxServiceState.Requested:
                Logger.Success("Deboarding: Requested");
                if (_currentAdapter != null) _ = _currentAdapter.OnDeboardingRequested();
                break;
            case GsxServiceState.Active:
                Logger.Success("Deboarding: Active");
                break;
            case GsxServiceState.Completed when !_deboardingDone:
                _deboardingDone = true;
                Logger.Success("Deboarding: Complete");
                if (_currentAdapter != null) _ = _currentAdapter.OnDeboardingCompleted();
                Logger.Debug("Deboarding Complete - Resetting Session and deactivating system");
                ResetSession();
                if (_activated) ToggleActivation();
                break;
        }
    }

    private void OnPushbackStateChanged(GsxServiceState state)
    {
        switch (state)
        {
            case GsxServiceState.Requested:
                Logger.Success("Pushback: Requested");
                if (_currentAdapter != null) _ = _currentAdapter.OnPushbackRequested();
                break;
            case GsxServiceState.Active:
                _pushbackDone = true; // GSX doesn't always set pushback state to completed so we set pushback done here
                Logger.Success("Pushback: Active");
                break;
            case GsxServiceState.Completed:
                _pushbackDone = true;
                Logger.Success("Pushback: Complete");
                if (_currentAdapter != null) _ = _currentAdapter.OnPushbackCompleted();
                break;
        }
    }

    private void OnRefuelingStateChanged(GsxServiceState state)
    {
        switch (state)
        {
            case GsxServiceState.Requested:
                Logger.Success("Refueling: Requested");
                if (_currentAdapter != null) _ = _currentAdapter.OnRefuelingRequested();
                break;
            case GsxServiceState.Active:
                Logger.Success("Refueling: Active");
                break;
            case GsxServiceState.Completed:
                _refuelingDone = true;
                Logger.Success("Refueling: Complete");
                if (_currentAdapter != null) _ = _currentAdapter.OnRefuelingCompleted();
                if (_activated) EvaluateBoarding();
                break;
        }
    }

    private void OnCateringStateChanged(GsxServiceState state)
    {
        switch (state)
        {
            case GsxServiceState.Requested:
                Logger.Success("Catering: Requested");
                if (_currentAdapter != null) _ = _currentAdapter.OnCateringRequested();
                break;
            case GsxServiceState.Active:
                Logger.Success("Catering: Active");
                break;
            case GsxServiceState.Completed:
                _cateringDone = true;
                Logger.Success("Catering: Complete");
                if (_currentAdapter != null) _ = _currentAdapter.OnCateringCompleted();
                if (_activated) EvaluateRefueling();
                if (_activated) EvaluateBoarding();
                break;
        }
    }

    private void EvaluateServices()
    {
        EvaluateDeboarding();
        EvaluatePushback();
        EvaluateRefueling();
        EvaluateCatering();
        EvaluateBoarding();
    }

    private void EvaluateRefueling()
    {
        if (!_activated || !_gsxMonitor.IsGsxRunning) return;
        if (_flightState.EngineOn || _flightState.HasEnginesEverRun) return;
        if (_flightState.HasMoved || _flightState.BeaconOn) return;
        if (_refuelingDone || _boardingDone) return;

        var cfg = ConfigManager.GetAircraftConfig(_flightState.AircraftTitle);
        if (!cfg.RefuelBeforeBoarding) return;
        if (cfg.CateringOnNewFlight && !_cateringDone) return;

        _ = CallServiceAsync("Refueling",
            GetRefuelingState,
            _gsxMenu.CallRefuelingAsync,
            EvaluateRefueling);
    }

    private void EvaluateCatering()
    {
        if (!_activated || !_gsxMonitor.IsGsxRunning) return;
        if (_flightState.EngineOn || _flightState.HasEnginesEverRun) return;
        if (_flightState.HasMoved || _flightState.BeaconOn) return;
        if (_cateringDone || _boardingDone) return;

        var cfg = ConfigManager.GetAircraftConfig(_flightState.AircraftTitle);
        if (!cfg.CateringOnNewFlight) return;

        _ = CallServiceAsync("Catering",
            GetCateringState,
            _gsxMenu.CallCateringAsync,
            EvaluateCatering);
    }

    private void EvaluateBoarding()
    {
        if (!_activated || !_gsxMonitor.IsGsxRunning) return;
        if (_flightState.EngineOn || _flightState.HasEnginesEverRun) return;
        if (_flightState.HasMoved || _flightState.BeaconOn) return;
        if (_boardingDone || _pushbackAttempted) return;

        if (_gsxMonitor.DeboardingState == GsxServiceState.Active ||
            _gsxMonitor.DeboardingState == GsxServiceState.Requested) return;

        var cfg = ConfigManager.GetAircraftConfig(_flightState.AircraftTitle);
        if (cfg.RefuelBeforeBoarding && !_refuelingDone) return;
        if (cfg.CateringOnNewFlight && !_cateringDone) return;

        _ = CallServiceAsync("Boarding",
            GetBoardingState,
            _gsxMenu.CallBoardingAsync,
            EvaluateBoarding);
    }

    private void EvaluatePushback()
    {
        if (!_activated || !_gsxMonitor.IsGsxRunning) return;
        if (_flightState.EngineOn || _flightState.HasEnginesEverRun) return;
        if (!_flightState.BeaconOn || _flightState.HasMoved) return;
        if (!_flightState.ParkingBrake) return;
        if (_pushbackDone) return;

        if (_gsxMonitor.BoardingState == GsxServiceState.Active ||
            _gsxMonitor.BoardingState == GsxServiceState.Requested ||
            _gsxMonitor.DeboardingState == GsxServiceState.Active ||
            _gsxMonitor.DeboardingState == GsxServiceState.Requested) return;

        if (_gsxMonitor.CateringState == GsxServiceState.Active ||
            _gsxMonitor.CateringState == GsxServiceState.Requested ||
            _gsxMonitor.RefuelingState == GsxServiceState.Active ||
            _gsxMonitor.RefuelingState == GsxServiceState.Requested) return;

        if (_gsxMonitor.PushbackState != GsxServiceState.Callable) return;

        _pushbackAttempted = true;

        _ = CallServiceAsync("Pushback",
            GetPushbackState,
            TriggerPushbackAsync,
            OnPushbackTimeout);
    }

    private async Task TriggerPushbackAsync()
    {
        if (_currentAdapter != null)
            await _currentAdapter.OnBeforePushbackAsync();
        else
            await Task.Delay(2_000);

        await _gsxMenu.CallPushbackAsync();
    }

    private void EvaluateDeboarding()
    {
        if (!_activated || !_gsxMonitor.IsGsxRunning) return;
        if (_deboardingDone) return;
        if (_flightState.EngineOn) return;
        if (!_flightState.HasMoved || !_flightState.HasEnginesEverRun) return;
        if (!_flightState.OnGround || _flightState.BeaconOn) return;
        if (_flightState.GroundSpeed > 0.5) return;

        _ = CallServiceAsync("Deboarding",
            GetDeboardingState,
            TriggerDeboardingAsync,
            EvaluateDeboarding);
    }

    private async Task TriggerDeboardingAsync()
    {
        if (_currentAdapter != null)
            await _currentAdapter.OnBeforeDeboardingAsync();

        await _gsxMenu.CallDeboardingAsync();
    }

    private async Task CallServiceAsync(
        string name,
        Func<GsxServiceState> getState,
        Func<Task> trigger,
        Action? onTimeout = null)
    {
        var state = getState();
        if (state != GsxServiceState.Callable)
        {
            Logger.Debug($"AutomationManager: {name} not callable (state={state})");
            return;
        }

        Logger.Debug($"AutomationManager: triggering {name}");

        try
        {
            await trigger();

            var ackDeadline = DateTime.UtcNow.AddSeconds(30);
            while (DateTime.UtcNow < ackDeadline)
            {
                var s = getState();
                if (s != GsxServiceState.Callable && s != GsxServiceState.Unknown)
                {
                    Logger.Debug($"AutomationManager: GSX acknowledged {name}");
                    return;
                }
                await Task.Delay(2_000);
            }

            Logger.Warning($"AutomationManager: GSX did not Acknowledge {name} within 30 s. Retrying...");
            onTimeout?.Invoke();
        }
        catch (Exception ex)
        {
            Logger.Error($"AutomationManager: {name} failed: {ex.Message}");
        }
    }

    private void SyncInitialGsxStates()
    {
        if (_gsxMonitor.RefuelingState == GsxServiceState.Completed && !_refuelingDone)
        {
            _refuelingDone = true;
            Logger.Debug("AutomationManager: sync - refueling already completed");
        }
        if (_gsxMonitor.CateringState == GsxServiceState.Completed && !_cateringDone)
        {
            _cateringDone = true;
            Logger.Debug("AutomationManager: sync - catering already completed");
        }
        if (_gsxMonitor.BoardingState == GsxServiceState.Completed && !_boardingDone)
        {
            _boardingDone = true;
            Logger.Debug("AutomationManager: sync - boarding already completed");
        }
        if (_gsxMonitor.PushbackState == GsxServiceState.Completed && !_pushbackDone)
        {
            _pushbackDone = true;
            Logger.Debug("AutomationManager: sync - pushback already completed");
        }
        if (_gsxMonitor.DeboardingState == GsxServiceState.Completed && !_deboardingDone)
        {
            _deboardingDone = true;
            Logger.Debug("AutomationManager: sync - deboarding already completed");
        }
    }

    private GsxServiceState GetRefuelingState()
    {
        return _gsxMonitor.RefuelingState;
    }

    private GsxServiceState GetCateringState()
    {
        return _gsxMonitor.CateringState;
    }

    private GsxServiceState GetBoardingState()
    {
        return _gsxMonitor.BoardingState;
    }

    private GsxServiceState GetPushbackState()
    {
        return _gsxMonitor.PushbackState;
    }

    private GsxServiceState GetDeboardingState()
    {
        return _gsxMonitor.DeboardingState;
    }

    private void OnPushbackTimeout()
    {
        _pushbackAttempted = false;
        EvaluatePushback();
    }

    public void PrintState()
    {
        Logger.Info("=== Automation State ===");
        Logger.Info($"  Activated:\t\t{_activated}");
        Logger.Info($"  Aircraft:\t\t{_flightState.AircraftTitle}");
        Logger.Info($"  Beacon:\t\t{_flightState.BeaconOn}");
        Logger.Info($"  Brake:\t\t{_flightState.ParkingBrake}");
        Logger.Info($"  On Ground:\t\t{_flightState.OnGround}");
        Logger.Info($"  Engine Running:\t{_flightState.EngineOn}");
        Logger.Info($"  Ground Speed:\t{_flightState.GroundSpeed:F1} kts");
        Logger.Info($"  Has Moved:\t\t{_flightState.HasMoved}");
        Logger.Info($"  Engines Ran:\t\t{_flightState.HasEnginesEverRun}");
        Logger.Info($"  Refueling:\t\tGSX={_gsxMonitor.RefuelingState}\t\tDone={_refuelingDone}");
        Logger.Info($"  Catering:\t\tGSX={_gsxMonitor.CateringState}\t\tDone={_cateringDone}");
        Logger.Info($"  Boarding:\t\tGSX={_gsxMonitor.BoardingState}\t\tDone={_boardingDone}");
        Logger.Info($"  Pushback:\t\tGSX={_gsxMonitor.PushbackState}\t\tDone={_pushbackDone}\tAttempted={_pushbackAttempted}");
        Logger.Info($"  Deboarding:\t\tGSX={_gsxMonitor.DeboardingState}\t\tDone={_deboardingDone}");
        Logger.Info("========================");
    }
}
