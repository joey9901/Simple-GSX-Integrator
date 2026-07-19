using System.Threading.Tasks;
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

    private AircraftAdapterBase? _currentAdapter;

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
    private bool _pushbackAttempted;

    private SimConnect? _sc;

    public event Action<bool>? ActivationChanged;
    public event Action<string>? AircraftChanged;
    public event Action<string>? ServiceTimedOut;

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

    public AircraftAdapterBase? CurrentAdapter
    {
        get { return _currentAdapter; }
    }

    public string? CurrentAircraftDisplayName => _currentAdapter?.DisplayName;
    private string ConfigAircraftTitle => _currentAdapter?.DisplayName ?? _flightState.AircraftTitle;

    public void SetCurrentAdapter(AircraftAdapterBase? adapter)
    {
        if (_currentAdapter != null) _currentAdapter.GroundEquipmentStateChanged -= OnGroundEquipmentStateChanged;
        _currentAdapter = adapter;
        _initialDoorsCheckDone = false;
        _initialStateReadDone = false;
        if (_currentAdapter != null) _currentAdapter.GroundEquipmentStateChanged += OnGroundEquipmentStateChanged;
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

    private void AutoDeactivate()
    {
        if (!_activated) return;
        var cfg = ConfigManager.GetAircraftConfig(ConfigAircraftTitle);
        if (!cfg.RealisticCrewComms) return;
        Logger.Info("AutomationManager: Realistic crew comms enabled, deactivating. Re-activate to call next service.");
        ToggleActivation();
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

    private bool _initialDoorsCheckDone;
    private bool _initialStateReadDone;

    private void OnBeaconChanged(bool beaconOn)
    {
        if (_flightState.IsInMenu) return;

        if (_gsxMonitor.BoardingState == GsxServiceState.Active ||
            _gsxMonitor.BoardingState == GsxServiceState.Requested ||
            _gsxMonitor.PushbackState == GsxServiceState.Active ||
            _gsxMonitor.PushbackState == GsxServiceState.Requested ||
            _gsxMonitor.DeboardingState == GsxServiceState.Active ||
            _gsxMonitor.DeboardingState == GsxServiceState.Requested) return;

        StartGroundEquipmentSync();
        CloseAndArmDoors();

        if (!_activated || !_gsxMonitor.IsGsxRunning) return;
        EvaluateServices();
    }

    private void OnGroundEquipmentStateChanged()
    {
        StartGroundEquipmentSync();

        if (_initialDoorsCheckDone) return;
        _initialDoorsCheckDone = true;
        CloseAndArmDoors();
    }

    private void CloseAndArmDoors()
    {
        if (_flightState.IsInMenu) return;

        CloseAllDoors();
        ArmAllDoors();
    }

    private void CloseAllDoors()
    {
        if (!GetClosableDoorsOption(out var doors)) return;
        doors.CloseOpenDoors();
    }

    private void ArmAllDoors()
    {
        if (!GetArmableDoorsOption(out var doors)) return;
        doors.ArmAllDoors();
    }

    private bool _groundEquipmentSyncRunning;

    private void StartGroundEquipmentSync()
    {
        // Skip the IsInMenu check on the very first state read (before FlightStateTracker has read initial menu state)
        if (_initialStateReadDone && _flightState.IsInMenu) return;
        if (_groundEquipmentSyncRunning) return;

        var hasOption = GetGroundEquipmentOption(out var equipment);
        if (!hasOption) return;

        _initialStateReadDone = true; // After first equipment read, enforce IsInMenu going forward

        var inDesiredState = IsGroundEquipmentInDesiredState(equipment);

        if (inDesiredState) return;

        _groundEquipmentSyncRunning = true;
        _ = RunGroundEquipmentSyncLoopAsync();
    }

    private async Task RunGroundEquipmentSyncLoopAsync()
    {
        try
        {
            var deadline = DateTime.UtcNow.AddSeconds(30);
            while (DateTime.UtcNow < deadline)
            {
                if (!GetGroundEquipmentOption(out var equipment)) return;
                if (IsGroundEquipmentInDesiredState(equipment)) return;

                await ApplyDesiredGroundEquipmentStateAsync(equipment);
                await Task.Delay(1_000);
            }
            Logger.Warning("AutomationManager: Ground equipment still mismatched with beacon after 30s");
        }
        finally
        {
            _groundEquipmentSyncRunning = false;
        }
    }

    private bool IsGroundEquipmentInDesiredState(IGroundEquipment equipment) =>
        equipment.GpuConnected == ShouldGroundEquipmentBePresent && equipment.ChocksSet == ShouldGroundEquipmentBePresent;

    private bool ShouldGroundEquipmentBePresent => !_flightState.BeaconOn;

    private async Task ApplyDesiredGroundEquipmentStateAsync(IGroundEquipment equipment)
    {
        if (_flightState.BeaconOn) // beacon on means GPU disconnects before chocks
        {
            if (equipment.GpuConnected != ShouldGroundEquipmentBePresent) await equipment.SetGpu(ShouldGroundEquipmentBePresent);
            if (equipment.ChocksSet != ShouldGroundEquipmentBePresent) await equipment.SetChocks(ShouldGroundEquipmentBePresent);
        }
        else // beacon off chocks are set first
        {
            if (equipment.ChocksSet != ShouldGroundEquipmentBePresent) await equipment.SetChocks(ShouldGroundEquipmentBePresent);
            if (equipment.GpuConnected != ShouldGroundEquipmentBePresent) await equipment.SetGpu(ShouldGroundEquipmentBePresent);
        }
    }

    private void OnParkingBrakeChanged(bool brakeSet)
    {
        if (!_activated || !_gsxMonitor.IsGsxRunning) return;
        EvaluateServices();
    }

    private async void OnSpawnedAtGate()
    {
        await Task.Delay(10_000);
        if (GetEngineCoversOption(out var covers))
            await covers.RemoveCovers();

        if (GetEfbPreloadableOption(out var efb))
            await efb.PreloadEfb();
    }

    private async void OnMenuStateChanged()
    {
        if (_flightState.IsInMenu)
        {
            Logger.Debug("AutomationManager: Entered Menu - Resetting Session");

            if (GetEfbPreloadableOption(out var efb))
                await efb.DisposeEfb();

            ResetSession(printLog: false);
        }

        if (!_gsxMonitor.IsGsxRunning) await _gsxMenu.FlashMenuAsync(); // tries to start GSX (it gets stuck sometimes)
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
        var cfg = ConfigManager.GetAircraftConfig(ConfigAircraftTitle);

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
                AutoDeactivate();
                break;
            case GsxServiceState.Active:
                Logger.Success("Boarding: Active");
                if (_currentAdapter is ICargoDoor cargoDoorActive) _ = cargoDoorActive.OpenCargoDoor();
                AutoDeactivate();
                break;
            case GsxServiceState.Completed when !_boardingDone:
                _boardingDone = true;
                Logger.Success("Boarding: Complete");
                if (_currentAdapter is ICargoDoor cargoDoorDone) _ = cargoDoorDone.CloseCargoDoor();
                if (GetClosableDoorsOption(out var doors)) _ = CloseDoorsAfterDelay(doors, TimeSpan.FromSeconds(15));
                break;
        }
    }

    private void OnDeboardingStateChanged(GsxServiceState state)
    {
        switch (state)
        {
            case GsxServiceState.Requested:
                Logger.Success("Deboarding: Requested");
                AutoDeactivate();
                break;
            case GsxServiceState.Active:
                Logger.Success("Deboarding: Active");
                if (_currentAdapter is ICargoDoor cargoDoorActive) _ = cargoDoorActive.OpenCargoDoor();
                AutoDeactivate();
                break;
            case GsxServiceState.Completed when !_deboardingDone:
                _deboardingDone = true;
                Logger.Success("Deboarding: Complete");
                if (_currentAdapter is ICargoDoor cargoDoorDone) _ = cargoDoorDone.CloseCargoDoor();
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
                break;
            case GsxServiceState.Active:
                _pushbackDone = true; // GSX doesn't always set pushback state to completed so we set pushback done here
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
            case GsxServiceState.Requested:
                Logger.Success("Refueling: Requested");
                break;
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
            case GsxServiceState.Requested:
                Logger.Success("Catering: Requested");
                break;
            case GsxServiceState.Active:
                Logger.Success("Catering: Active");
                break;
            case GsxServiceState.Completed:
                _cateringDone = true;
                Logger.Success("Catering: Complete");
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

        var cfg = ConfigManager.GetAircraftConfig(ConfigAircraftTitle);
        if (!cfg.RefuelBeforeBoarding) return;
        if (cfg.CateringOnNewFlight && !_cateringDone) return;

        _ = CallServiceAsync("Refueling",
            GetRefuelingState,
            _gsxMenu.CallRefuelingAsync,
            EvaluateRefueling,
            "boarding");
    }

    private void EvaluateCatering()
    {
        if (!_activated || !_gsxMonitor.IsGsxRunning) return;
        if (_flightState.EngineOn || _flightState.HasEnginesEverRun) return;
        if (_flightState.HasMoved || _flightState.BeaconOn) return;
        if (_cateringDone || _boardingDone) return;

        var cfg = ConfigManager.GetAircraftConfig(ConfigAircraftTitle);
        if (!cfg.CateringOnNewFlight) return;

        _ = CallServiceAsync("Catering",
            GetCateringState,
            _gsxMenu.CallCateringAsync,
            EvaluateCatering,
            "boarding");
    }

    private void EvaluateBoarding()
    {
        if (!_activated || !_gsxMonitor.IsGsxRunning) return;
        if (_flightState.EngineOn || _flightState.HasEnginesEverRun) return;
        if (_flightState.HasMoved || _flightState.BeaconOn) return;
        if (!_flightState.ParkingBrake) return;
        if (_boardingDone || _pushbackAttempted) return;

        if (_gsxMonitor.DeboardingState == GsxServiceState.Active ||
            _gsxMonitor.DeboardingState == GsxServiceState.Requested) return;

        var cfg = ConfigManager.GetAircraftConfig(ConfigAircraftTitle);
        if (cfg.RefuelBeforeBoarding && !_refuelingDone) return;
        if (cfg.CateringOnNewFlight && !_cateringDone) return;

        _ = CallServiceAsync("Boarding",
            GetBoardingState,
            _gsxMenu.CallBoardingAsync,
            EvaluateBoarding,
            "boarding");
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
            OnPushbackTimeout,
            "pushback");
    }

    private void EvaluateDeboarding()
    {
        if (!_activated || !_gsxMonitor.IsGsxRunning) return;
        if (_deboardingDone) return;
        if (_flightState.EngineOn) return;
        if (!_flightState.HasMoved || !_flightState.HasEnginesEverRun) return;
        if (!_flightState.OnGround || _flightState.BeaconOn) return;
        if (_flightState.GroundSpeed > 0.5) return;
        if (!_flightState.ParkingBrake) return;

        _ = CallServiceAsync("Deboarding",
            GetDeboardingState,
            _gsxMenu.CallDeboardingAsync,
            EvaluateDeboarding,
            "deboard");
    }

    private async Task TriggerPushbackAsync()
    {
        if (GetClosableDoorsOption(out var doors))
            await CloseDoorsWithRetry(doors);
        else
            await Task.Delay(2_000);

        await _gsxMenu.CallPushbackAsync();
    }

    private async Task CloseDoorsWithRetry(IClosableDoors doors)
    {
        await doors.CloseOpenDoors();

        var deadline = DateTime.UtcNow.AddSeconds(60);
        while (doors.AnyDoorOpen && DateTime.UtcNow < deadline)
        {
            await Task.Delay(5_000);
            await doors.CloseOpenDoors();
        }

        Logger.Info(doors.AnyDoorOpen
            ? "AutomationManager: Doors still open after 60s - Proceeding with Pushback"
            : "AutomationManager: All Doors Confirmed Closed");
    }

    private async Task CloseDoorsAfterDelay(IClosableDoors doors, TimeSpan delay)
    {
        await Task.Delay(delay);
        await doors.CloseOpenDoors();
    }

    private async Task CallServiceAsync(
        string name,
        Func<GsxServiceState> getState,
        Func<Task> trigger,
        Action? onTimeout = null,
        string? serviceKey = null)
    {
        if (!_activated || !_gsxMonitor.IsGsxRunning) return;

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
            if (serviceKey != null) ServiceTimedOut?.Invoke(serviceKey);
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

    private bool GetGroundEquipmentOption(out IGroundEquipment equipment)
    {
        equipment = null!;
        if (_currentAdapter is not IGroundEquipment e) return false;
        if (!ConfigManager.GetAircraftConfig(ConfigAircraftTitle).ManageGroundEquipment) return false;
        equipment = e;
        return true;
    }

    private bool GetEngineCoversOption(out IEngineCovers covers)
    {
        covers = null!;
        if (_currentAdapter is not IEngineCovers c) return false;
        if (!ConfigManager.GetAircraftConfig(ConfigAircraftTitle).RemoveCovers) return false;
        covers = c;
        return true;
    }

    private bool GetClosableDoorsOption(out IClosableDoors doors)
    {
        doors = null!;
        if (_currentAdapter is not IClosableDoors d) return false;
        if (!ConfigManager.GetAircraftConfig(ConfigAircraftTitle).ManageDoors) return false;
        doors = d;
        return true;
    }

    private bool GetArmableDoorsOption(out IArmableDoors doors)
    {
        doors = null!;
        if (_currentAdapter is not IArmableDoors d) return false;
        if (!ConfigManager.GetAircraftConfig(ConfigAircraftTitle).ManageDoors) return false;
        doors = d;
        return true;
    }

    private bool GetEfbPreloadableOption(out IEfbRunner efb)
    {
        efb = null!;
        if (_currentAdapter is not IEfbRunner e) return false;
        efb = e;
        return true;
    }
}
