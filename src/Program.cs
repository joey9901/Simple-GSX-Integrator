using System.Runtime.InteropServices;
using Microsoft.FlightSimulator.SimConnect;
using SimpleGsxIntegrator.Aircraft;
using SimpleGsxIntegrator.Automation;
using SimpleGsxIntegrator.Config;
using SimpleGsxIntegrator.Core;
using SimpleGsxIntegrator.Gsx;
using SimpleGsxIntegrator.Infrastructure;

namespace SimpleGsxIntegrator;

internal static class Program
{
    private static SimConnectManager _manager = null!;
    private static FlightStateTracker _flightState = null!;
    private static GsxMonitor _gsxMonitor = null!;
    private static GsxMenuController _gsxMenu = null!;
    private static AutomationManager _automationManager = null!;
    private static HotkeyListener _hotkeys = null!;
    private static ProcessWatcher _procWatcher = null!;
    private static MainWindow _MainWindow = null!;
    private static System.Windows.Forms.Timer _simConnectTimer = null!;

    private static SimConnect? _sc;

    public static string CurrentAircraftPath { get; private set; } = string.Empty;
    public static string CurrentAircraftTitle { get; private set; } = string.Empty;
    public static bool IsSimConnectConnected => _manager?.IsConnected ?? false;
    public static bool IsGsxRunning => _gsxMonitor?.IsGsxRunning ?? false;
    public static bool IsSystemActive => _automationManager?.IsActivated ?? false;
    private static string? _resolvedDisplayName;
    private static string _rawAircraftTitle = string.Empty;
    private static Mutex? _singleInstanceMutex;
    private static bool _closeWithSim;

    [STAThread]
    private static void Main(string[] args)
    {
        _closeWithSim = args.Contains("--close-with-sim", StringComparer.OrdinalIgnoreCase);
        _singleInstanceMutex = new Mutex(initiallyOwned: true, "SimpleGSXIntegrator_SingleInstance", out bool createdNew);
        if (!createdNew)
        {
            MessageBox.Show(
                "Simple GSX Integrator is already running.",
                "Already Running",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
            _singleInstanceMutex.Dispose();
            return;
        }

        ApplicationConfiguration.Initialize();

        var cfg = ConfigManager.GetConfig();

        _MainWindow = new MainWindow();

        if (_closeWithSim) Logger.Debug("Close-with-sim flag active — app will exit when MSFS closes.");

        _manager = new SimConnectManager();
        _flightState = new FlightStateTracker();
        _gsxMonitor = new GsxMonitor();
        _gsxMenu = new GsxMenuController();
        _automationManager = new AutomationManager(_flightState, _gsxMonitor, _gsxMenu);

        _hotkeys = new HotkeyListener(cfg.Hotkeys.ActivationKey, cfg.Hotkeys.ResetKey);
        _procWatcher = new ProcessWatcher();
        _hotkeys.Start();

        _procWatcher.StartIfMsfsRunning();

        _manager.Connected += OnSimConnectConnected;
        _manager.Disconnected += OnSimConnectDisconnected;
        _manager.SimObjectDataReceived += OnSimObjectData;
        _manager.SystemStateReceived += OnSystemStateReceived;
        _manager.SimulatorQuit += OnSimulatorQuit;

        _automationManager.ActivationChanged += OnActivationChanged;

        _flightState.AircraftChanged += OnAircraftTitleChanged;

        _flightState.BeaconChanged += OnBeaconChangedForDisplay;
        _flightState.ParkingBrakeChanged += OnParkingBrakeChangedForDisplay;
        _flightState.EngineChanged += OnEngineChangedForDisplay;
        _flightState.EnginesEverRunChanged += OnEnginesEverRunChangedForDisplay;

        _gsxMonitor.GsxStarted += OnGsxStarted;
        _gsxMonitor.GsxStopped += OnGsxStopped;
        _gsxMonitor.BoardingStateChanged += s => _MainWindow.Invoke(() => _MainWindow.SendMessage(new { type = "serviceStatus", service = "boarding", status = s.ToString().ToLower() }));
        _gsxMonitor.DeboardingStateChanged += s => _MainWindow.Invoke(() => _MainWindow.SendMessage(new { type = "serviceStatus", service = "deboard", status = s.ToString().ToLower() }));
        _gsxMonitor.PushbackStateChanged += s => _MainWindow.Invoke(() => _MainWindow.SendMessage(new { type = "serviceStatus", service = "pushback", status = s.ToString().ToLower() }));
        _gsxMonitor.RefuelingStateChanged += s => _MainWindow.Invoke(() => _MainWindow.SendMessage(new { type = "serviceStatus", service = "boarding", status = s.ToString().ToLower() }));
        _gsxMonitor.CateringStateChanged += s => _MainWindow.Invoke(() => _MainWindow.SendMessage(new { type = "serviceStatus", service = "boarding", status = s.ToString().ToLower() }));

        _hotkeys.ActivationPressed += OnActivationKeyPressed;
        _hotkeys.ResetPressed += OnResetKeyPressed;

        _procWatcher.MsfsExited += OnMsfsExited;

        _MainWindow.Show();
        Logger.Debug("UI window shown.");

        TryConnectSimConnect();

        _simConnectTimer = new System.Windows.Forms.Timer { Interval = 50, Enabled = true };
        _simConnectTimer.Tick += OnSimConnectTimerTick;

        Application.Run(_MainWindow);
    }

    private static CancellationTokenSource? _retryConnectCts;

    private static void TryConnectSimConnect()
    {
        _retryConnectCts?.Cancel();
        _retryConnectCts = new CancellationTokenSource();
        var token = _retryConnectCts.Token;

        Logger.Debug("Attempting SimConnect Connection…");
        try
        {
            _manager.Connect(_MainWindow.Handle);
            Logger.Debug("SimConnect connected.");
            _MainWindow.Invoke(() => _MainWindow.SendMessage(new { type = "simconnect", connected = true }));
        }
        catch (COMException ex)
        {
            Logger.Debug($"SimConnect not available ({ex.Message}). Will retry when MSFS is running.");
            Task.Run(async () =>
            {
                while (!token.IsCancellationRequested && !_manager.IsConnected)
                {
                    try { await Task.Delay(5000, token); } catch (TaskCanceledException) { return; }
                    if (token.IsCancellationRequested) return;
                    try
                    {
                        _manager.Connect(_MainWindow.Handle);
                        Logger.Debug("SimConnect reconnected.");
                        _MainWindow.Invoke(() => _MainWindow.SendMessage(new { type = "simconnect", connected = true }));
                    }
                    catch { }
                }
            }, token);
        }
    }

    private static void OnSimConnectConnected(SimConnect sc)
    {
        _sc = sc;
        _simConnectTimer?.Start(); // restart pump timer (may have been stopped on disconnect)

        _procWatcher.StartIfMsfsRunning();

        _automationManager.OnSimConnectConnected(sc);
        _automationManager.CurrentAdapter?.OnSimConnectConnected(sc);

        _flightState.OnSimConnectConnected(sc, _automationManager.CurrentAdapter);
        _gsxMonitor.OnSimConnectConnected(sc);
        _gsxMenu.OnSimConnectConnected(sc);

        try
        {
            sc.RequestSystemState((SimReq)900, "AircraftLoaded");
        }
        catch (Exception ex)
        {
            Logger.Warning($"Could not request aircraft state: {ex.Message}");
        }
    }

    private static void OnSimConnectDisconnected()
    {
        _sc = null;
        _simConnectTimer?.Stop();
        _resolvedDisplayName = null;
        _automationManager.SetCurrentAdapter(null);
        Logger.Debug("SimConnect disconnected.");
        _MainWindow.Invoke(() => _MainWindow.SendMessage(new { type = "simconnect", connected = false }));
        _MainWindow.Invoke(() => _MainWindow.SendMessage(new { type = "gsx", running = false }));
        _manager.Disconnect();
        TryConnectSimConnect();
    }

    private static void OnSimObjectData(SIMCONNECT_RECV_SIMOBJECT_DATA data)
    {
        _flightState.OnSimObjectData(data);
        _gsxMonitor.OnSimObjectData(data);
        _automationManager.CurrentAdapter?.OnSimObjectData(data);
    }

    private static void OnSystemStateReceived(SIMCONNECT_RECV_SYSTEM_STATE data)
    {
        if (data.dwRequestID != 900) return;

        string aircraftPath = data.szString?.Trim() ?? string.Empty;
        if (string.IsNullOrEmpty(aircraftPath)) return;
        if (aircraftPath == CurrentAircraftPath) return;

        CurrentAircraftPath = aircraftPath;
        Logger.Debug($"Aircraft loaded: {aircraftPath}");

        LoadAdapterForAircraft(aircraftPath);

        // Correct the display name in case AircraftChanged fired before the path resolved.
        // _resolvedDisplayName was just set by LoadAdapterForAircraft; recompute the title.
        if (!string.IsNullOrEmpty(_rawAircraftTitle))
        {
            var correct = _resolvedDisplayName ?? AircraftAdapterMatcher.TryGetFamilyForTitle(_rawAircraftTitle) ?? _rawAircraftTitle;
            if (correct != CurrentAircraftTitle)
            {
                CurrentAircraftTitle = correct;
                _MainWindow.Invoke(() => _MainWindow.SendMessage(new { type = "aircraft", title = CurrentAircraftTitle }));
            }
        }
    }

    private static void OnActivationChanged(bool isActive)
    {
        Logger.Debug($"Activation changed: {isActive}");
        _MainWindow.Invoke(() => _MainWindow.SendMessage(new { type = "system", active = isActive }));
        _MainWindow.Invoke(() => _MainWindow.SendMessage(new { type = "aircraft", title = CurrentAircraftTitle }));
    }

    private static void OnAircraftTitleChanged(string title)
    {
        _rawAircraftTitle = title;
        var displayTitle = _resolvedDisplayName ?? AircraftAdapterMatcher.TryGetFamilyForTitle(title) ?? title;
        CurrentAircraftTitle = displayTitle;

        var adapter = _automationManager.CurrentAdapter;
        // Create config entry for any recognized aircraft (displayTitle was normalized from path/family),
        // not just those with a custom adapter — ensures native integrations appear in the picker.
        if (adapter != null || displayTitle != title)
        {
            var cfg = ConfigManager.GetAircraftConfig(displayTitle);
            if (adapter != null)
            {
                adapter.removeCovers = cfg.RemoveCovers && adapter.canRemoveCovers;
                if (!cfg.ManageGroundEquipment) adapter.canRemoveAndPlaceGroundEquipment = false;
                if (!cfg.CloseDoors) adapter.canCloseDoors = false;
            }
        }

        _MainWindow.Invoke(() => _MainWindow.SendMessage(new { type = "aircraft", title = displayTitle }));
        _MainWindow.Invoke(() => RefreshAircraftStateDetails());
        try { _sc?.RequestSystemState((SimReq)900, "AircraftLoaded"); }
        catch { }
    }

    private static void OnActivationKeyPressed()
    {
        Logger.Debug("Hotkey: activation pressed");
        _automationManager.ToggleActivation();
    }

    private static void OnResetKeyPressed()
    {
        Logger.Debug("Hotkey: reset session");
        _automationManager.ResetSession();
    }

    private static void OnMsfsExited()
    {
        Logger.Warning("MSFS process no longer detected - exiting.");
        _retryConnectCts?.Cancel();
        Application.Exit();
    }

    private static void OnSimConnectTimerTick(object? sender, EventArgs e)
    {
        try { _manager.PumpMessages(); }
        catch (Exception ex)
        {
            Logger.Debug($"SimConnect pump error: {ex.Message}");
            _simConnectTimer.Stop();
            OnSimConnectDisconnected();
        }
    }

    private static void OnGsxStarted()
    {
        Logger.Debug("GSX started.");
        _MainWindow.Invoke(() => _MainWindow.SendMessage(new { type = "gsx", running = true }));
    }

    private static void OnGroundStateChanged()
    {
        var adapter = _automationManager.CurrentAdapter;
        SendGroundEquipState(adapter);
    }

    private static void SendGroundEquipState(AircraftAdapterBase? adapter)
    {
        // Use _resolvedDisplayName (just set by LoadAdapterForAircraft) rather than CurrentAircraftTitle,
        // which may still hold the previous aircraft's name during an aircraft switch.
        var caps = AircraftAdapterMatcher.GetCapabilitiesForTitle(_resolvedDisplayName ?? CurrentAircraftTitle);
        _MainWindow.Invoke(() => _MainWindow.SendMessage(new
        {
            type = "groundEquip",
            canManageGroundEquipment = caps.CanManageGroundEquipment,
            showDoors = caps.CanCloseDoors,
            chocks = adapter?.ChocksSet,
            gpu = adapter?.GpuConnected,
            openDoors = adapter?.OpenDoorCount,
        }));
    }

    private static void OnGsxStopped()
    {
        Logger.Debug("GSX stopped.");
        _MainWindow.Invoke(() => _MainWindow.SendMessage(new { type = "gsx", running = false }));
    }

    private static void OnSimulatorQuit()
    {
        if (_closeWithSim)
        {
            Logger.Debug("Simulator quit — closing with sim (--close-with-sim flag active).");
            Application.Exit();
        }
        else
        {
            Logger.Debug("Simulator quit.");
        }
    }

    private static void LoadAdapterForAircraft(string aircraftPath)
    {
        if (string.IsNullOrEmpty(aircraftPath)) return;

        var match = AircraftAdapterMatcher.Resolve(aircraftPath);

        Logger.Debug("Aircraft path: " + aircraftPath);

        _resolvedDisplayName = match.Kind != AircraftAdapterMatcher.MatchKind.Unknown ? match.DisplayName : null;

        if (match.Adapter?.GetType() == _automationManager.CurrentAdapter?.GetType() && _automationManager.CurrentAdapter != null)
        {
            Logger.Debug($"LoadAdapterForAircraft: adapter already loaded for '{aircraftPath}', skipping.");
            return;
        }

        var prevAdapter = _automationManager.CurrentAdapter;
        if (prevAdapter != null) prevAdapter.GroundStateChanged -= OnGroundStateChanged;
        _automationManager.SetCurrentAdapter(match.Adapter);
        if (match.Adapter != null) match.Adapter.GroundStateChanged += OnGroundStateChanged;

        // Reset ground equip section; adapters that poll (PMDG) will re-populate via event within a few seconds.
        // Adapters that write (A330) will populate on next place/remove operation.
        SendGroundEquipState(match.Adapter);

        if (_sc != null && match.Adapter != null)
        {
            _flightState.OnSimConnectConnected(_sc, match.Adapter);
        }

        switch (match.Kind)
        {
            case AircraftAdapterMatcher.MatchKind.Adapter:
                Logger.Success($"Custom Profile for {match.DisplayName} Found! Doors and Ground Equipment will be managed Automatically.");
                if (_sc != null)
                {
                    Logger.Debug($"Registering Adapter '{match.Adapter!.GetType().Name}' with Active SimConnect.");
                    match.Adapter.OnSimConnectConnected(_sc);
                }
                else
                {
                    Logger.Debug($"Adapter '{match.Adapter!.GetType().Name}' created but SimConnect not yet connected.");
                }
                break;

            case AircraftAdapterMatcher.MatchKind.NativeIntegration:
                Logger.Success($"{match.DisplayName} Detected. Aircraft has Native GSX Integration.\nGround Equipment & Door Closing is handled by its own Systems.");
                if (_sc != null && match.Adapter != null)
                {
                    Logger.Debug($"Registering NativeIntegration Adapter '{match.Adapter.GetType().Name}' with Active SimConnect.");
                    match.Adapter.OnSimConnectConnected(_sc);
                }
                break;

            case AircraftAdapterMatcher.MatchKind.NonFunctional:
                Logger.Warning($"{match.DisplayName} Detected. This Aircraft was Tested and found to be Non-Functional.");
                break;

            case AircraftAdapterMatcher.MatchKind.Unknown:
                Logger.Info("No Custom Profile found for this Aircraft.\nDoors and Ground Equipment will NOT be managed Automatically. Native GSX support is Unknown.");
                break;
        }
    }

    public static void PrintCurrentState()
    {
        _automationManager?.PrintState();
    }

    public static void RegisterActivationForCurrentAircraft()
    {
        if (_sc == null || string.IsNullOrEmpty(CurrentAircraftTitle)) return;

        var cfg = ConfigManager.GetAircraftConfig(CurrentAircraftTitle);
        if (!string.IsNullOrEmpty(cfg.ActivationLvar))
        {
            _flightState.SetActivationLvar(_sc, cfg.ActivationLvar);
            Logger.Debug($"Activation L:var registered: '{cfg.ActivationLvar}' (trigger={cfg.ActivationValue})");
        }
    }

    public static void ToggleMovementFlag()
    {
        bool current = _flightState.HasMoved;
        _flightState.ForceHasMoved(!current);
    }

    public static void ToggleEnginesEverRunFlag()
    {
        bool current = _flightState.HasEnginesEverRun;
        _flightState.ForceEnginesEverRun(!current);
    }

    public static void SetRebindingMode(bool isRebinding)
    {
        _hotkeys.SetRebinding(isRebinding);
    }

    public static void UpdateHotkey(string hotkeyType, string hotkeyString)
    {
        var cfg = ConfigManager.GetConfig();

        if (hotkeyType.Equals("activation", StringComparison.OrdinalIgnoreCase))
        {
            cfg.Hotkeys.ActivationKey = hotkeyString;
            _hotkeys.SetActivationKey(hotkeyString);
        }
        else if (hotkeyType.Equals("reset", StringComparison.OrdinalIgnoreCase))
        {
            cfg.Hotkeys.ResetKey = hotkeyString;
            _hotkeys.SetResetKey(hotkeyString);
        }

        ConfigManager.Save(cfg);
        Logger.Info($"Hotkey '{hotkeyType}' updated to '{hotkeyString}'.");
    }

    private static void OnBeaconChangedForDisplay(bool _) { RefreshAircraftStateDetails(); }
    private static void OnParkingBrakeChangedForDisplay(bool _) { RefreshAircraftStateDetails(); }
    private static void OnEngineChangedForDisplay(bool _) { RefreshAircraftStateDetails(); }
    private static void OnEnginesEverRunChangedForDisplay(bool _) { RefreshAircraftStateDetails(); }

    public static void RefreshDisplayState() => RefreshAircraftStateDetails();
    public static void RefreshGroundEquipState() => SendGroundEquipState(_automationManager?.CurrentAdapter);

    private static void RefreshAircraftStateDetails()
    {
        _MainWindow.Invoke(() => _MainWindow.SendMessage(new
        {
            type = "state",
            beaconOn = _flightState.BeaconOn,
            enginesOn = _flightState.EngineOn,
            parkingBrake = _flightState.ParkingBrake,
            enginesEverRan = _flightState.HasEnginesEverRun
        }));
    }

}
