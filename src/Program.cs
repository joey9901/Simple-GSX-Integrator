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
    private static MainForm _mainForm = null!;
    private static System.Windows.Forms.Timer _simConnectTimer = null!;

    private static SimConnect? _sc;

    public static string CurrentAircraftPath { get; private set; } = string.Empty;

    private static Mutex? _singleInstanceMutex;

    [STAThread]
    private static void Main()
    {
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

        _mainForm = new MainForm();
        Logger.MainForm = _mainForm;

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
        _flightState.SpeedChanged += OnSpeedChangedForDisplay;

        _gsxMonitor.GsxStarted += OnGsxStarted;
        _gsxMonitor.GsxStopped += OnGsxStopped;

        _hotkeys.ActivationPressed += OnActivationKeyPressed;
        _hotkeys.ResetPressed += OnResetKeyPressed;

        _procWatcher.MsfsExited += OnMsfsExited;

        _mainForm.Show();
        _mainForm.Invoke(() => _mainForm.SetSimConnectStatus(false));
        _mainForm.Invoke(() => _mainForm.SetSystemStatus(false));
        _mainForm.Invoke(() => SyncHotkeyLabels());

        TryConnectSimConnect();

        _simConnectTimer = new System.Windows.Forms.Timer { Interval = 50, Enabled = true };
        _simConnectTimer.Tick += OnSimConnectTimerTick;

        Application.Run(_mainForm);
    }

    private static void TryConnectSimConnect()
    {
        Logger.Debug("Attempting SimConnect connection …");
        try
        {
            _manager.Connect(_mainForm.Handle);
            _mainForm.Invoke(() => _mainForm.SetSimConnectStatus(true));
            Logger.Debug("SimConnect connected.");
        }
        catch (COMException ex)
        {
            Logger.Debug($"SimConnect not available ({ex.Message}). Will retry when MSFS is running.");
            _mainForm.Invoke(() => _mainForm.SetSimConnectStatus(false));
            Task.Run(async () =>
            {
                while (!_manager.IsConnected)
                {
                    await Task.Delay(5000);
                    try
                    {
                        _manager.Connect(_mainForm.Handle);
                        _mainForm.Invoke(() => _mainForm.SetSimConnectStatus(true));
                        Logger.Debug("SimConnect reconnected.");
                    }
                    catch { }
                }
            });
        }
    }

    private static void OnSimConnectConnected(SimConnect sc)
    {
        _sc = sc;

        _procWatcher.StartIfMsfsRunning();

        _flightState.OnSimConnectConnected(sc);
        _gsxMonitor.OnSimConnectConnected(sc);
        _gsxMenu.OnSimConnectConnected(sc);
        _automationManager.OnSimConnectConnected(sc);
        _automationManager.CurrentAdapter?.OnSimConnectConnected(sc);

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
        _mainForm.Invoke(() => _mainForm.SetSimConnectStatus(false));
        _mainForm.Invoke(() => _mainForm.SetGsxStatus(false));
        _automationManager.SetCurrentAdapter(null);
        Logger.Warning("SimConnect disconnected.");
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
    }

    private static void OnActivationChanged(bool isActive)
    {
        _mainForm.Invoke(() =>
        {
            _mainForm.SetSystemStatus(isActive);
            _mainForm.SetCurrentAircraft(_flightState.AircraftTitle, isActive);
        });
    }

    private static void OnAircraftTitleChanged(string title)
    {
        _mainForm.Invoke(() => _mainForm.SetCurrentAircraft(title, _automationManager.IsActivated));
        RefreshAircraftStateDetails();
        // Re-request the full aircraft .cfg path so we can match the correct adapter.
        // The title alone (e.g. "777F") is not enough for adapter matching.
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
        Logger.Info("Hotkey: reset session");
        _automationManager.ResetSession();
    }

    private static void OnMsfsExited()
    {
        Logger.Warning("MSFS process no longer detected - exiting.");
        _mainForm.Invoke(() => Application.Exit());
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
        _mainForm.Invoke(() => _mainForm.SetGsxStatus(true));
    }

    private static void OnGsxStopped()
    {
        _mainForm.Invoke(() => _mainForm.SetGsxStatus(false));
    }

    private static void OnSimulatorQuit()
    {
        _mainForm.Invoke(() =>
        {
            _mainForm.SetSimConnectStatus(false);
            _mainForm.SetGsxStatus(false);
        });
    }

    private static void LoadAdapterForAircraft(string aircraftPath)
    {
        if (string.IsNullOrEmpty(aircraftPath)) return;

        var match = AircraftAdapterMatcher.Resolve(aircraftPath);

        Logger.Debug("Aircraft path: " + aircraftPath);

        // Skip if we already have the same adapter type running to avoid double-registration.
        if (match.Adapter?.GetType() == _automationManager.CurrentAdapter?.GetType() && _automationManager.CurrentAdapter != null)
        {
            Logger.Debug($"LoadAdapterForAircraft: adapter already loaded for '{aircraftPath}', skipping.");
            return;
        }

        _automationManager.SetCurrentAdapter(match.Adapter);

        if (_sc != null)
        {
            var overrides = match.Adapter?.GetSimVarOverrides() ?? new Dictionary<SimVarOverride, string>();
            _flightState.SetSimVarOverrides(_sc, overrides);
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
        if (_sc == null || string.IsNullOrEmpty(_flightState.AircraftTitle)) return;

        var cfg = ConfigManager.GetAircraftConfig(_flightState.AircraftTitle);
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
        Logger.Info($"hasMoved forced → {!current}");
    }

    public static void ToggleEnginesEverRunFlag()
    {
        bool current = _flightState.HasEnginesEverRun;
        _flightState.ForceEnginesEverRun(!current);
        Logger.Info($"hasEnginesEverRun forced \u2192 {!current}");
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
        SyncHotkeyLabels();
        Logger.Info($"Hotkey '{hotkeyType}' updated to '{hotkeyString}'.");
    }

    private static void OnBeaconChangedForDisplay(bool _) { RefreshAircraftStateDetails(); }
    private static void OnParkingBrakeChangedForDisplay(bool _) { RefreshAircraftStateDetails(); }
    private static void OnEngineChangedForDisplay(bool _) { RefreshAircraftStateDetails(); }
    private static void OnSpeedChangedForDisplay(double _) { RefreshAircraftStateDetails(); }

    private static void RefreshAircraftStateDetails()
    {
        _mainForm.Invoke(() => _mainForm.SetAircraftStateDetails(
            _flightState.AircraftTitle,
            _flightState.BeaconOn,
            _flightState.ParkingBrake,
            _flightState.EngineOn,
            _flightState.HasMoved,
            _flightState.GroundSpeed));
    }

    private static void SyncHotkeyLabels()
    {
        var cfg = ConfigManager.GetConfig();
        _mainForm.SetHotkeys(cfg.Hotkeys.ActivationKey, cfg.Hotkeys.ResetKey);
    }
}
