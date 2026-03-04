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
/// Design principles:
///   • All external triggers (beacon, engine, GSX state changes) are edge-detected
///     events, not polled conditions. This avoids the race conditions and repeated
///     trigger spam in the previous implementation.
///   • Service completion flags are maintained internally so a GSX restart does not
///     cause previously-completed services to fire again.
///   • Guard conditions are evaluated before every service call using the latest
///     state snapshot reported by <see cref="FlightStateTracker"/> and <see cref="GsxMonitor"/>.
///   • Thread safety: all state mutations happen inside Task.Run callbacks that
///     are naturally serialised by the SimConnect event dispatch order;
///     a lightweight lock protects multi-step sequences.
/// </summary>
public sealed class AutomationManager
{
    private readonly FlightStateTracker _flightState;
    private readonly GsxMonitor _gsxMonitor;
    private readonly GsxMenuController _gsxMenu;
    private readonly DoorManager _doorManager;

    private bool _activated;
    private bool _refuelingDone;
    private bool _cateringDone;
    private bool _boardingDone;
    private bool _pushbackDone;
    private bool _deboardingDone;

    /// <summary>
    /// Set once boarding-triggered-pushback has been attempted to prevent the
    /// beacon-ON event from triggering pushback multiple times.
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

    private readonly object _sequenceLock = new();

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
    /// Wire this to <see cref="SimConnectHub.Connected"/>.
    /// </summary>
    public void OnSimConnectConnected(SimConnect sc) => _sc = sc;

    /// <summary>
    /// Toggles system activation on/off. Fires <see cref="ActivationChanged"/> event.
    /// When activating, immediately evaluates GSX initial states to catch up.
    /// </summary>
    public void ToggleActivation()
    {
        _activated = !_activated;
        Logger.Debug(_activated
            ? "SYSTEM ACTIVATED – GSX automation enabled"
            : "SYSTEM DEACTIVATED – GSX automation disabled");

        ActivationChanged?.Invoke(_activated);

        if (_activated)
        {
            SyncInitialGsxStates();
            EvaluateServices();
        }
    }

    /// <summary>
    /// Resets all session flags (useful for turnaround or debugging).
    /// </summary>
    public void ResetSession()
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
        Logger.Success("Session reset – all service flags cleared");
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
            Logger.Debug($"AutomationManager: activation L:var hit target value {value} – toggling system");
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
        Logger.Debug("GSX stopped – automation paused until GSX restarts");
    }

    private void OnBoardingStateChanged(GsxServiceState state)
    {
        switch (state)
        {
            case GsxServiceState.Requested:
                Logger.Success("Boarding: Requested");
                break;

            case GsxServiceState.Active:
                Logger.Success("Boarding: Active");
                break;

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
            case GsxServiceState.Active:
                Logger.Success("Deboarding: Active");
                break;

            case GsxServiceState.Completed when !_deboardingDone:
                _deboardingDone = true;
                Logger.Success("Deboarding: Complete");
                Logger.Debug("Deboarding Complete – Resetting Session and deactivating system");
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
            case GsxServiceState.Active: Logger.Success("Pushback: Active"); break;

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
            case GsxServiceState.Active:
                Logger.Success("Refueling: Active");
                break;

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
            case GsxServiceState.Active:
                Logger.Success("Catering: Active");
                break;

            case GsxServiceState.Completed:
                _cateringDone = true;
                Logger.Success("Catering: Complete");
                if (_activated) EvaluateBoarding();
                break;
        }
    }

    /// <summary>
    /// Central dispatch: evaluates all services in priority order.
    /// Each evaluate method is fully self-guarded and returns immediately if
    /// its preconditions are not met, so calling all of them is always safe.
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

        TryCallService("Refueling",
            _gsxMonitor.Refueling,
            ref _lastRefuelingCall,
            () => _ = _gsxMenu.CallRefuelingAsync());
    }

    private void EvaluateCatering()
    {
        if (!_activated || !_gsxMonitor.IsGsxRunning) return;
        if (_flightState.HasMoved || _flightState.BeaconOn) return;
        if (_cateringDone || _boardingDone) return;

        var cfg = ConfigManager.GetAircraftConfig(_flightState.AircraftTitle);
        if (!cfg.CateringOnNewFlight) return;

        TryCallService("Catering",
            _gsxMonitor.Catering,
            ref _lastCateringCall,
            () => _ = _gsxMenu.CallCateringAsync());
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

        TryCallService("Boarding",
            _gsxMonitor.Boarding,
            ref _lastBoardingCall,
            () => _ = _gsxMenu.CallBoardingAsync());
    }

    /// <summary>
    /// Evaluates whether to initiate pushback (beacon ON + boarding done + doors closeable).
    /// </summary>
    private void EvaluatePushback()
    {
        if (!_activated || !_gsxMonitor.IsGsxRunning) return;
        if (!_flightState.BeaconOn) return;
        if (_flightState.HasMoved) return;
        if (_pushbackDone || _pushbackAttempted) return;

        // Don't push back if boarding/deboarding is still actively running.
        // Beacon must be ON (checked above) – that signals the crew is ready to depart,
        // regardless of whether this session tracked boarding completing.
        if (_gsxMonitor.Boarding == GsxServiceState.Active ||
            _gsxMonitor.Boarding == GsxServiceState.Requested ||
            _gsxMonitor.Deboarding == GsxServiceState.Active ||
            _gsxMonitor.Deboarding == GsxServiceState.Requested) return;

        if ((DateTime.Now - _lastPushbackCall) < ServiceCallCooldown) return;
        _pushbackAttempted = true;

        Logger.Debug("AutomationManager: pre-pushback sequence initiated");

        _ = Task.Run(async () =>
        {
            try
            {
                // 1. Close all open doors via the adapter before asking GSX to move the plane.
                //    Capture current adapter reference so it can't be swapped out mid-sequence.
                var adapter = _doorManager.CurrentAdapter;
                if (adapter != null)
                {
                    Logger.Debug("AutomationManager: closing all open doors before pushback");
                    await adapter.CloseAllOpenDoorsAsync();

                    // Poll until L:vars confirm all doors are closed (max 60 s).
                    // This prevents GSX from re-opening a door if we
                    // call pushback while a door is still swinging shut.
                    var deadline = DateTime.UtcNow.AddSeconds(60);
                    while (adapter.AreAnyDoorsOpen() && DateTime.UtcNow < deadline)
                    {
                        Logger.Debug("AutomationManager: waiting for doors to close…");
                        await Task.Delay(2_000);
                    }

                    if (adapter.AreAnyDoorsOpen())
                        Logger.Warning("AutomationManager: doors still open after 60 s – proceeding with pushback anyway");
                    else
                        Logger.Info("AutomationManager: All Doors Confirmed Closed");

                    Logger.Info("AutomationManager: Removing Ground Equipment");
                    adapter.RemoveGroundEquipment();
                    await Task.Delay(2_000);   // allow GPU/chock removal animations
                }
                else
                {
                    Logger.Debug("AutomationManager: no adapter – skipping door close before pushback");
                    await Task.Delay(2_000);
                }

                _lastPushbackCall = DateTime.Now;
                await _gsxMenu.CallPushbackAsync();

                // Wait up to 30 s for GSX to acknowledge the pushback request.
                // If the state never leaves Callable/Unknown the call was likely dropped
                // (e.g. wrong menu position, GSX not ready). Clear the attempt flag and
                // re-evaluate so we can try again on the next trigger.
                var ackDeadline = DateTime.UtcNow.AddSeconds(30);
                while (DateTime.UtcNow < ackDeadline)
                {
                    var s = _gsxMonitor.Pushback;
                    if (s == GsxServiceState.Requested || s == GsxServiceState.Active || s == GsxServiceState.Completed)
                    {
                        Logger.Debug("AutomationManager: GSX acknowledged pushback request");
                        break;
                    }
                    await Task.Delay(2_000);
                }

                if (_gsxMonitor.Pushback == GsxServiceState.Callable || _gsxMonitor.Pushback == GsxServiceState.Unknown)
                {
                    Logger.Warning("AutomationManager: GSX did not acknowledge pushback within 30 s – will retry");
                    _pushbackAttempted = false;
                    _lastPushbackCall = DateTime.MinValue;
                    EvaluatePushback();
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"AutomationManager: pushback sequence failed: {ex.Message}");
            }
        });
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

        TryCallService("Deboarding",
            _gsxMonitor.Deboarding,
            ref _lastDeboardingCall,
            () => _ = _gsxMenu.CallDeboardingAsync());
    }

    /// <summary>
    /// Tries to call a service, checking that:
    ///   • Its GSX state is <see cref="GsxServiceState.Callable"/>
    ///   • The per-service cooldown has elapsed
    /// </summary>
    private void TryCallService(
        string name,
        GsxServiceState currentState,
        ref DateTime lastCall,
        Action trigger)
    {
        if (currentState != GsxServiceState.Callable)
        {
            Logger.Debug($"AutomationManager: {name} not callable (state={currentState})");
            return;
        }

        if ((DateTime.Now - lastCall) < ServiceCallCooldown)
        {
            Logger.Debug($"AutomationManager: {name} call throttled (cooldown)");
            return;
        }

        lastCall = DateTime.Now;
        Logger.Debug($"AutomationManager: triggering {name}");
        trigger();
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
            Logger.Debug("AutomationManager: sync – refueling already completed");
        }
        if (_gsxMonitor.Catering == GsxServiceState.Completed && !_cateringDone)
        {
            _cateringDone = true;
            Logger.Debug("AutomationManager: sync – catering already completed");
        }
        if (_gsxMonitor.Boarding == GsxServiceState.Completed && !_boardingDone)
        {
            _boardingDone = true;
            Logger.Debug("AutomationManager: sync – boarding already completed");
        }
        if (_gsxMonitor.Pushback == GsxServiceState.Completed && !_pushbackDone)
        {
            _pushbackDone = true;
            Logger.Debug("AutomationManager: sync – pushback already completed");
        }
        if (_gsxMonitor.Deboarding == GsxServiceState.Completed && !_deboardingDone)
        {
            _deboardingDone = true;
            Logger.Debug("AutomationManager: sync – deboarding already completed");
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
