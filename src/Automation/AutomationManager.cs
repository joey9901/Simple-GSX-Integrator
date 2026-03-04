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
    // -----------------------------------------------------------------
    //  Dependencies
    // -----------------------------------------------------------------

    private readonly FlightStateTracker _flightState;
    private readonly GsxMonitor _gsxMonitor;
    private readonly GsxMenuController _gsxMenu;
    private readonly DoorManager _doorManager;

    // -----------------------------------------------------------------
    //  Session state
    // -----------------------------------------------------------------

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

    // -----------------------------------------------------------------
    //  Events for the UI
    // -----------------------------------------------------------------

    /// <summary>The system was activated or deactivated.</summary>
    public event Action<bool>? ActivationChanged;

    /// <summary>The current aircraft name changed.</summary>
    public event Action<string>? AircraftChanged;

    // -----------------------------------------------------------------
    //  Constructor
    // -----------------------------------------------------------------

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

    // -----------------------------------------------------------------
    //  Public interface
    // -----------------------------------------------------------------

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
            if (_flightState.BeaconOn)
                EvaluatePushback();   // Beacon already on – skip ground services, go straight to pushback
            else
                EvaluateGroundServicesNow();
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

    // -----------------------------------------------------------------
    //  Event subscriptions
    // -----------------------------------------------------------------

    private void SetupEvents()
    {
        // FlightState
        _flightState.BeaconChanged += OnBeaconChanged;
        _flightState.AircraftChanged += OnAircraftChanged;
        _flightState.ActivationLvarTriggered += OnActivationLvarTriggered;

        // GSX service states
        _gsxMonitor.GsxStarted += OnGsxStarted;
        _gsxMonitor.GsxStopped += OnGsxStopped;
        _gsxMonitor.BoardingStateChanged += OnBoardingStateChanged;
        _gsxMonitor.DeboardingStateChanged += OnDeboardingStateChanged;
        _gsxMonitor.PushbackStateChanged += OnPushbackStateChanged;
        _gsxMonitor.RefuelingStateChanged += OnRefuelingStateChanged;
        _gsxMonitor.CateringStateChanged += OnCateringStateChanged;
    }

    // -----------------------------------------------------------------
    //  FlightState event handlers
    // -----------------------------------------------------------------

    private void OnBeaconChanged(bool beaconOn)
    {
        if (!_activated || !_gsxMonitor.IsGsxRunning) return;

        if (beaconOn)
            EvaluatePushback();
        else
            EvaluateGroundServicesNow();
    }

    private void OnAircraftChanged(string title)
    {
        Logger.Debug($"Aircraft changed: {title}");

        var cfg = ConfigManager.GetAircraftConfig(title);
        AircraftChanged?.Invoke(title);

        // Register activation L:var if configured for this aircraft
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

    // -----------------------------------------------------------------
    //  GSX event handlers
    // -----------------------------------------------------------------

    private void OnGsxStarted()
    {
        Logger.Debug("GSX is running");
        SyncInitialGsxStates();
        if (_activated)
        {
            if (_flightState.BeaconOn)
                EvaluatePushback();
            else
                EvaluateGroundServicesNow();
        }
    }

    private void OnGsxStopped()
    {
        Logger.Debug("GSX stopped – automation paused until GSX restarts");
        _gsxMenu.ResetOperatorSelection();
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
                // Try to call boarding now that refueling is done
                if (_activated) EvaluateBoardingReady();
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
                // Try to call boarding now that catering is done
                if (_activated) EvaluateBoardingReady();
                break;
        }
    }

    // -----------------------------------------------------------------
    //  Service evaluation logic
    // -----------------------------------------------------------------

    /// <summary>
    /// Evaluates all ground services (refueling, catering, boarding).
    /// Called when the system activates, beacon turns off, or GSX starts.
    /// </summary>
    private void EvaluateGroundServicesNow()
    {
        if (!_activated || !_gsxMonitor.IsGsxRunning) return;
        if (_flightState.HasMoved) return;
        if (_flightState.BeaconOn) return;

        var cfg = ConfigManager.GetAircraftConfig(_flightState.AircraftTitle);

        // ---- Refueling ----
        if (cfg.RefuelBeforeBoarding && !_refuelingDone && !_boardingDone)
        {
            TryCallService("Refueling",
                _gsxMonitor.Refueling,
                ref _lastRefuelingCall,
                () => _ = _gsxMenu.CallRefuelingAsync());
        }

        // ---- Catering ----
        if (cfg.CateringOnNewFlight && !_cateringDone && !_boardingDone)
        {
            TryCallService("Catering",
                _gsxMonitor.Catering,
                ref _lastCateringCall,
                () => _ = _gsxMenu.CallCateringAsync());
        }

        // ---- Boarding ----
        EvaluateBoardingReady();
    }

    /// <summary>
    /// Evaluates whether boarding should be called, respecting sequencing guards.
    /// </summary>
    private void EvaluateBoardingReady()
    {
        if (!_activated || !_gsxMonitor.IsGsxRunning) return;
        if (_flightState.HasMoved || _flightState.BeaconOn) return;
        if (_boardingDone || _pushbackAttempted) return;

        // Deboarding must not be running
        if (_gsxMonitor.Deboarding == GsxServiceState.Active ||
            _gsxMonitor.Deboarding == GsxServiceState.Requested) return;

        var cfg = ConfigManager.GetAircraftConfig(_flightState.AircraftTitle);

        // Wait for refueling if configured
        if (cfg.RefuelBeforeBoarding && !_refuelingDone) return;

        // Wait for catering if enabled and not done
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
        if (_flightState.HasMoved) return;
        if (_pushbackDone || _pushbackAttempted) return;

        // Don't push back if boarding/deboarding is still actively running.
        // We no longer require _boardingDone: if the beacon is ON the crew is ready,
        // regardless of whether this session tracked boarding completing.
        if (_gsxMonitor.Boarding == GsxServiceState.Active ||
            _gsxMonitor.Boarding == GsxServiceState.Requested ||
            _gsxMonitor.Deboarding == GsxServiceState.Active ||
            _gsxMonitor.Deboarding == GsxServiceState.Requested) return;

        // Pushback cooldown
        if ((DateTime.Now - _lastPushbackCall) < ServiceCallCooldown) return;
        _pushbackAttempted = true;

        Logger.Info("AutomationManager: pre-pushback sequence initiated");

        _ = Task.Run(async () =>
        {
            try
            {
                // 1. Close all open doors via the adapter before asking GSX to move the plane.
                //    Capture current adapter reference so it can't be swapped out mid-sequence.
                var adapter = _doorManager.CurrentAdapter;
                if (adapter != null)
                {
                    Logger.Info("AutomationManager: closing all open doors before pushback");
                    adapter.CloseAllOpenDoors();
                    await Task.Delay(3_000);   // allow door animations to complete

                    Logger.Info("AutomationManager: removing ground equipment");
                    adapter.RemoveGroundEquipment();
                    await Task.Delay(2_000);   // allow GPU/chock removal animations
                }
                else
                {
                    Logger.Debug("AutomationManager: no adapter – skipping door close before pushback");
                    await Task.Delay(2_000);
                }

                // 2. Request pushback via GSX menu.
                _lastPushbackCall = DateTime.Now;
                await _gsxMenu.CallPushbackAsync();
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
        if (!_activated && !_flightState.HasEnginesEverRun) return;
        if (!_gsxMonitor.IsGsxRunning) return;
        if (_deboardingDone) return;
        if (_flightState.HasMoved && !_flightState.HasEnginesEverRun) return;

        // Must be parked
        if (!_flightState.OnGround || _flightState.BeaconOn) return;
        if (_flightState.GroundSpeed > 0.5) return;

        TryCallService("Deboarding",
            _gsxMonitor.Deboarding,
            ref _lastDeboardingCall,
            () => _ = _gsxMenu.CallDeboardingAsync());
    }

    // -----------------------------------------------------------------
    //  Helpers
    // -----------------------------------------------------------------

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

    // -----------------------------------------------------------------
    //  Debug helpers
    // -----------------------------------------------------------------

    public void PrintState()
    {
        Logger.Info("=== Automation State ===");
        Logger.Info($"  Activated:\t\t{_activated}");
        Logger.Info($"  Aircraft:\t\t{_flightState.AircraftTitle}");
        Logger.Info($"  Beacon:\t\t{_flightState.BeaconOn}");
        Logger.Info($"  Brake:\t\t{_flightState.ParkingBrake}");
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
