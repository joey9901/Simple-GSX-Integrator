using Microsoft.FlightSimulator.SimConnect;
using SimpleGsxIntegrator.Aircraft;
using SimpleGsxIntegrator.Config;
using SimpleGsxIntegrator.Core;
using SimpleGsxIntegrator.Gsx;

namespace SimpleGsxIntegrator.Automation;

/// <summary>
/// Central automation state machine. Subscribes to events from
/// <see cref="FlightStateTracker"/> and <see cref="GsxMonitor"/> and
/// determines when to trigger GSX ground services.
///
/// Service completion flags are maintained internally so a GSX restart does not
/// cause previously-completed services to fire again.
/// </summary>
public sealed class AutomationManager
{
    private readonly FlightStateTracker _flightState;
    private readonly GsxMonitor _gsxMonitor;
    private readonly GsxMenuController _gsxMenu;
    private readonly DoorManager _doorManager;

    private bool _activated;
    private string? _currentAircraftTitle;
    private bool _refuelingDone;
    private bool _cateringDone;
    private bool _boardingDone;
    private bool _pushbackDone;
    private bool _deboardingDone;

    /// <summary>
    /// Set once pushback has been attempted.
    /// This aims to stop boarding being called if user forgot to turn on APU
    /// and loses power, causing the beacon to turn OFF
    /// (can only occur if boarding was not performed)
    /// </summary>
    private bool _pushbackAttempted;

    private SimConnect? _sc;

    // Cooldown guards: prevent a service from being called twice within a short window
    private DateTime _lastRefuelingCall = DateTime.MinValue;
    private DateTime _lastCateringCall = DateTime.MinValue;
    private DateTime _lastBoardingCall = DateTime.MinValue;
    private DateTime _lastPushbackCall = DateTime.MinValue;
    private DateTime _lastDeboardingCall = DateTime.MinValue;
    private static readonly TimeSpan ServiceCallCooldown = TimeSpan.FromSeconds(10);

    public event Action<bool>? ActivationChanged;
    public event Action<string>? AircraftChanged;

    public AutomationManager(
        FlightStateTracker flightState,
        GsxMonitor gsxMonitor,
        GsxMenuController gsxMenu,
        DoorManager doorManager)
    {
        _flightState = flightState;
        _gsxMonitor = gsxMonitor;
        _gsxMenu = gsxMenu;
        _doorManager = doorManager;

        SetupEvents();
    }

    public bool IsActivated => _activated;

    /// <summary>
    /// Stores the SimConnect instance for passing to adapters / activation L:var registration.
    /// </summary>
    public void OnSimConnectConnected(SimConnect sc) => _sc = sc;

    /// <summary>
    /// Toggles system activation on/off. Fires <see cref="ActivationChanged"/> event.
    /// </summary>
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

    /// <summary>
    /// Resets all session flags (for turnaround and debugging / testing).
    /// </summary>
    public void ResetSession(bool printLog = true)
    {
        _refuelingDone = false;
        _cateringDone = false;
        _boardingDone = false;
        _pushbackDone = false;
        _deboardingDone = false;
        _pushbackAttempted = false;

        _lastRefuelingCall = DateTime.MinValue;
        _lastCateringCall = DateTime.MinValue;
        _lastBoardingCall = DateTime.MinValue;
        _lastPushbackCall = DateTime.MinValue;
        _lastDeboardingCall = DateTime.MinValue;

        _flightState.ResetSession();
        if (printLog)
            Logger.Success("Session reset - all service flags cleared");
    }

    private void SetupEvents()
    {
        _flightState.BeaconChanged += OnBeaconChanged;
        _flightState.AircraftChanged += OnAircraftChanged;
        _flightState.ActivationLvarTriggered += OnActivationLvarTriggered;
        _flightState.EngineChanged += OnEngineChanged;

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

    private void OnAircraftChanged(string title)
    {
        Logger.Debug($"Aircraft changed: {title}");

        // Only reset when switching from a previously-loaded aircraft.
        // On initial connection _currentAircraftTitle is null, so there is nothing to reset.
        if (_currentAircraftTitle != null)
        {
            if (_activated) ToggleActivation();
            ResetSession(printLog: false);
        }
        _currentAircraftTitle = title;

        var cfg = ConfigManager.GetAircraftConfig(title);
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
            case GsxServiceState.Requested: Logger.Success("Boarding: Requested"); break;

            case GsxServiceState.Active: Logger.Success("Boarding: Active"); break;

            case GsxServiceState.Completed when !_boardingDone:
                _boardingDone = true;
                Logger.Success("Boarding: Complete");
                // Beacon check is handled by the beacon-ON event; if beacon is already
                // on (e.g. loaded mid-session) evaluate pushback immediately.
                if (_flightState.BeaconOn && _activated)
                    EvaluatePushback();
                break;
        }
    }

    private void OnDeboardingStateChanged(GsxServiceState state)
    {
        switch (state)
        {
            case GsxServiceState.Requested: Logger.Success("Deboarding: Requested"); break;
            case GsxServiceState.Active: Logger.Success("Deboarding: Active"); break;

            case GsxServiceState.Completed when !_deboardingDone:
                _deboardingDone = true;
                Logger.Success("Deboarding: Complete");
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
            case GsxServiceState.Requested: Logger.Success("Pushback: Requested"); break;
            case GsxServiceState.Active:
                _pushbackDone = true; // GSX doesn't always set pushback state to completed so we set pushhback done here
                Logger.Success("Pushback: Active");
                break;

            case GsxServiceState.Completed:
                _pushbackDone = true;
                Logger.Success("Pushback: Complete");
                break;
        }
    }

    private void OnRefuelingStateChanged(GsxServiceState state)
    {
        switch (state)
        {
            case GsxServiceState.Requested: Logger.Success("Refueling: Requested"); break;
            case GsxServiceState.Active: Logger.Success("Refueling: Active"); break;

            case GsxServiceState.Completed:
                _refuelingDone = true;
                Logger.Success("Refueling: Complete");
                if (_activated) EvaluateBoarding();
                break;
        }
    }

    private void OnCateringStateChanged(GsxServiceState state)
    {
        switch (state)
        {
            case GsxServiceState.Requested: Logger.Success("Catering: Requested"); break;
            case GsxServiceState.Active: Logger.Success("Catering: Active"); break;

            case GsxServiceState.Completed:
                _cateringDone = true;
                Logger.Success("Catering: Complete");
                if (_activated) EvaluateRefueling();
                if (_activated) EvaluateBoarding();
                break;
        }
    }

    /// <summary>
    /// Evaluates all services in priority order.
    /// Each evaluate method is fully self-guarded and returns immediately if
    /// its preconditions are not met
    /// </summary>
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
        if (_flightState.HasMoved || _flightState.BeaconOn) return;
        if (_refuelingDone || _boardingDone) return;

        var cfg = ConfigManager.GetAircraftConfig(_flightState.AircraftTitle);
        if (!cfg.RefuelBeforeBoarding) return;
        if (cfg.CateringOnNewFlight && !_cateringDone) return;

        _ = TryCallServiceAsync("Refueling", _gsxMonitor.Refueling,
            () => _lastRefuelingCall, t => _lastRefuelingCall = t,
            () => _gsxMenu.CallRefuelingAsync(),
            () => _gsxMonitor.Refueling,
            () => EvaluateRefueling());
    }

    private void EvaluateCatering()
    {
        if (!_activated || !_gsxMonitor.IsGsxRunning) return;
        if (_flightState.HasMoved || _flightState.BeaconOn) return;
        if (_cateringDone || _boardingDone) return;

        var cfg = ConfigManager.GetAircraftConfig(_flightState.AircraftTitle);
        if (!cfg.CateringOnNewFlight) return;

        _ = TryCallServiceAsync("Catering", _gsxMonitor.Catering,
            () => _lastCateringCall, t => _lastCateringCall = t,
            () => _gsxMenu.CallCateringAsync(),
            () => _gsxMonitor.Catering,
            () => EvaluateCatering());
    }

    private void EvaluateBoarding()
    {
        if (!_activated || !_gsxMonitor.IsGsxRunning) return;
        if (_flightState.HasMoved || _flightState.BeaconOn) return;
        if (_boardingDone || _pushbackAttempted) return;

        if (_gsxMonitor.Deboarding == GsxServiceState.Active ||
            _gsxMonitor.Deboarding == GsxServiceState.Requested) return;

        var cfg = ConfigManager.GetAircraftConfig(_flightState.AircraftTitle);
        if (cfg.RefuelBeforeBoarding && !_refuelingDone) return;
        if (cfg.CateringOnNewFlight && !_cateringDone) return;

        _ = TryCallServiceAsync("Boarding", _gsxMonitor.Boarding,
            () => _lastBoardingCall, t => _lastBoardingCall = t,
            () => _gsxMenu.CallBoardingAsync(),
            () => _gsxMonitor.Boarding,
            () => EvaluateBoarding());
    }

    private void EvaluatePushback()
    {
        if (!_activated || !_gsxMonitor.IsGsxRunning) return;
        if (!_flightState.BeaconOn || _flightState.HasMoved) return;
        if (_pushbackDone || _pushbackAttempted) return;

        if (_gsxMonitor.Boarding == GsxServiceState.Active ||
            _gsxMonitor.Boarding == GsxServiceState.Requested ||
            _gsxMonitor.Deboarding == GsxServiceState.Active ||
            _gsxMonitor.Deboarding == GsxServiceState.Requested) return;

        if (_gsxMonitor.Pushback != GsxServiceState.Callable) return;

        _pushbackAttempted = true;

        _ = TryCallServiceAsync("Pushback", _gsxMonitor.Pushback,
            () => _lastPushbackCall, t => _lastPushbackCall = t,
            async () => { await PrepareForPushbackAsync(); await _gsxMenu.CallPushbackAsync(); },
            () => _gsxMonitor.Pushback,
            () => { _pushbackAttempted = false; EvaluatePushback(); });
    }

    /// <summary>
    /// Closes all open doors and removes ground equipment before pushback.
    /// </summary>
    private async Task PrepareForPushbackAsync()
    {
        var adapter = _doorManager.CurrentAdapter;
        if (adapter == null)
        {
            Logger.Debug("AutomationManager: no adapter - skipping door close before pushback");
            await Task.Delay(2_000);
            return;
        }

        Logger.Debug("AutomationManager: closing all open doors before pushback");
        await adapter.CloseAllOpenDoorsAsync();

        var deadline = DateTime.UtcNow.AddSeconds(60);
        while (adapter.AreAnyDoorsOpen() && DateTime.UtcNow < deadline)
        {
            Logger.Debug("AutomationManager: waiting for doors to close…");
            await Task.Delay(2_000);
        }

        if (adapter.AreAnyDoorsOpen())
            Logger.Warning("AutomationManager: doors still open after 60 s - proceeding with pushback anyway");
        else
            Logger.Info("AutomationManager: All Doors Confirmed Closed");

        Logger.Info("AutomationManager: Removing Ground Equipment");
        adapter.RemoveGroundEquipment();
        await Task.Delay(2_000);
    }

    /// <summary>
    /// Evaluates whether deboarding should be called after landing.
    /// Requires: engines have run (we flew) + aircraft now stationary + engines off + beacon off.
    /// </summary>
    public void EvaluateDeboarding()
    {
        if (!_activated || !_gsxMonitor.IsGsxRunning) return;
        if (_deboardingDone) return;
        if (!_flightState.HasMoved || !_flightState.HasEnginesEverRun) return;
        if (!_flightState.OnGround || _flightState.BeaconOn) return;
        if (_flightState.GroundSpeed > 0.5) return;

        _ = TryCallServiceAsync("Deboarding", _gsxMonitor.Deboarding,
            () => _lastDeboardingCall, t => _lastDeboardingCall = t,
            async () => { await PrepareForDeboardingAsync(); await _gsxMenu.CallDeboardingAsync(); },
            () => _gsxMonitor.Deboarding,
            () => EvaluateDeboarding());
    }

    /// <summary>
    /// Places ground equipment and chocks before deboarding, ensuring the CDU sequence
    /// completes before GSX opens its own menu.
    /// </summary>
    private async Task PrepareForDeboardingAsync()
    {
        var adapter = _doorManager.CurrentAdapter;
        if (adapter == null) return;

        await adapter.PlaceGroundEquipmentAndChocks();
    }

    /// <summary>
    /// Tries to call a GSX service, checking that:
    ///   • Its GSX state is <see cref="GsxServiceState.Callable"/>
    ///   • The per-service cooldown has elapsed
    /// After firing the trigger, polls up to 30 s for GSX acknowledgement.
    /// If GSX does not respond, resets the cooldown and invokes <paramref name="onTimeout"/>.
    /// </summary>
    private async Task TryCallServiceAsync(
        string name,
        GsxServiceState currentState,
        Func<DateTime> getLastCall,
        Action<DateTime> setLastCall,
        Func<Task> trigger,
        Func<GsxServiceState> getState,
        Action? onTimeout = null)
    {
        if (currentState != GsxServiceState.Callable)
        {
            Logger.Debug($"AutomationManager: {name} not callable (state={currentState})");
            return;
        }

        if ((DateTime.Now - getLastCall()) < ServiceCallCooldown)
        {
            Logger.Debug($"AutomationManager: {name} call throttled (cooldown)");
            return;
        }

        setLastCall(DateTime.Now);
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

            Logger.Warning($"AutomationManager: GSX did not acknowledge {name} within 30 s \u2013 will retry");
            setLastCall(DateTime.MinValue);
            onTimeout?.Invoke();
        }
        catch (Exception ex)
        {
            Logger.Error($"AutomationManager: {name} failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Called once when GSX becomes available (or system activates) to sync
    /// internal flags with the current GSX state so we don't re-trigger
    /// already-completed services after a reconnect.
    /// </summary>
    private void SyncInitialGsxStates()
    {
        if (_gsxMonitor.Refueling == GsxServiceState.Completed && !_refuelingDone)
        {
            _refuelingDone = true;
            Logger.Debug("AutomationManager: sync - refueling already completed");
        }
        if (_gsxMonitor.Catering == GsxServiceState.Completed && !_cateringDone)
        {
            _cateringDone = true;
            Logger.Debug("AutomationManager: sync - catering already completed");
        }
        if (_gsxMonitor.Boarding == GsxServiceState.Completed && !_boardingDone)
        {
            _boardingDone = true;
            Logger.Debug("AutomationManager: sync - boarding already completed");
        }
        if (_gsxMonitor.Pushback == GsxServiceState.Completed && !_pushbackDone)
        {
            _pushbackDone = true;
            Logger.Debug("AutomationManager: sync - pushback already completed");
        }
        if (_gsxMonitor.Deboarding == GsxServiceState.Completed && !_deboardingDone)
        {
            _deboardingDone = true;
            Logger.Debug("AutomationManager: sync - deboarding already completed");
        }
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
        Logger.Info($"  Refueling:\t\tGSX={_gsxMonitor.Refueling}\t\tDone={_refuelingDone}");
        Logger.Info($"  Catering:\t\tGSX={_gsxMonitor.Catering}\t\tDone={_cateringDone}");
        Logger.Info($"  Boarding:\t\tGSX={_gsxMonitor.Boarding}\t\tDone={_boardingDone}");
        Logger.Info($"  Pushback:\t\tGSX={_gsxMonitor.Pushback}\t\tDone={_pushbackDone}\tAttempted={_pushbackAttempted}");
        Logger.Info($"  Deboarding:\t\tGSX={_gsxMonitor.Deboarding}\t\tDone={_deboardingDone}");
        Logger.Info("========================");
    }
}
