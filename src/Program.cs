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
    private static DoorManager _doorManager = null!;
    private static AutomationManager _automationManager = null!;
    private static HotkeyListener _hotkeys = null!;
    private static ProcessWatcher _procWatcher = null!;
    private static MainForm _mainForm = null!;
    private static System.Windows.Forms.Timer _simConnectTimer = null!;

    private static IAircraftAdapter? _currentAdapter;

    private static SimConnect? _sc;

    /// <summary>
    /// Full filesystem path to the currently loaded aircraft
    /// </summary>
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
        _doorManager = new DoorManager(_gsxMonitor);
        _automationManager = new AutomationManager(_flightState, _gsxMonitor, _gsxMenu, _doorManager);

        _hotkeys = new HotkeyListener(cfg.Hotkeys.ActivationKey, cfg.Hotkeys.ResetKey);
        _procWatcher = new ProcessWatcher();
        _hotkeys.Start();

        _procWatcher.StartIfMsfsRunning();

        _manager.Connected += OnSimConnectConnected;
        _manager.Disconnected += OnSimConnectDisconnected;
        _manager.SimObjectDataReceived += OnSimObjectData;
        _manager.SystemStateReceived += OnSystemStateReceived;
        _manager.SimulatorQuit += OnSimulatorQuit;

        _automationManager.ActivationChanged += isActive =>
        {
            _mainForm.Invoke(() =>
            {
                _mainForm.SetSystemStatus(isActive);
                _mainForm.SetCurrentAircraft(_flightState.AircraftTitle, isActive);
            });
        };

        _flightState.AircraftChanged += title =>
        {
            _mainForm.Invoke(() => _mainForm.SetCurrentAircraft(title, _automationManager.IsActivated));
            RefreshAircraftStateDetails();
            // For mid-session aircraft switches, re-request the full AircraftLoaded path from
            // SimConnect (which has the .cfg path we need for adapter matching).
            // Do NOT call LoadAdapterForAircraft(title) here – the TITLE SimVar is just a short
            // display name (e.g. "777F") that won't match adapter patterns.
            try { _sc?.RequestSystemState((SimReq)900, "AircraftLoaded"); }
            catch { /* sim may be momentarily unavailable */ }
        };

        _flightState.BeaconChanged += _ => RefreshAircraftStateDetails();
        _flightState.ParkingBrakeChanged += _ => RefreshAircraftStateDetails();
        _flightState.EngineChanged += _ => RefreshAircraftStateDetails();
        _flightState.SpeedChanged += _ => RefreshAircraftStateDetails();

        _gsxMonitor.GsxStarted += () => _mainForm.Invoke(() => _mainForm.SetGsxStatus(true));
        _gsxMonitor.GsxStopped += () => _mainForm.Invoke(() => _mainForm.SetGsxStatus(false));

        _hotkeys.ActivationPressed += () =>
        {
            Logger.Debug("Hotkey: activation pressed");
            _automationManager.ToggleActivation();
        };
        _hotkeys.ResetPressed += () =>
        {
            Logger.Info("Hotkey: reset session");
            _automationManager.ResetSession();
        };

        _procWatcher.MsfsExited += () =>
        {
            Logger.Warning("MSFS process no longer detected – exiting.");
            _mainForm.Invoke(() => Application.Exit());
        };

        _mainForm.Show();
        _mainForm.Invoke(() => _mainForm.SetSimConnectStatus(false));
        _mainForm.Invoke(() => _mainForm.SetSystemStatus(false));
        _mainForm.Invoke(() => SyncHotkeyLabels());

        TryConnectSimConnect();

        _simConnectTimer = new System.Windows.Forms.Timer { Interval = 50, Enabled = true };
        _simConnectTimer.Tick += (_, _) =>
        {
            try { _manager.PumpMessages(); }
            catch (Exception ex)
            {
                Logger.Warning($"SimConnect pump error: {ex.Message}");
                _simConnectTimer.Stop();
                OnSimConnectDisconnected();
            }
        };

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
                    catch { /* still not available */ }
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

        // If an adapter was already selected (e.g. hub reconnect after aircraft already known),
        // give it the new SimConnect instance so it can register its definitions.
        _currentAdapter?.OnSimConnectConnected(sc);

        // Request the currently loaded aircraft path so we can choose the right adapter.
        try
        {
            sc.RequestSystemState((SimReq)900 /*AircraftLoaded*/, "AircraftLoaded");
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
        _currentAdapter = null;
        _doorManager.SetAdapter(null);
        Logger.Warning("SimConnect disconnected.");
    }

    private static void OnSimObjectData(SIMCONNECT_RECV_SIMOBJECT_DATA data)
    {
        _flightState.OnSimObjectData(data);
        _gsxMonitor.OnSimObjectData(data);
        _currentAdapter?.OnSimObjectData(data);
    }

    private static void OnSystemStateReceived(SIMCONNECT_RECV_SYSTEM_STATE data)
    {
        if (data.dwRequestID != 900) return;  // SimReq.AircraftLoaded

        string aircraftPath = data.szString?.Trim() ?? string.Empty;
        if (string.IsNullOrEmpty(aircraftPath)) return;
        if (aircraftPath == CurrentAircraftPath) return;

        CurrentAircraftPath = aircraftPath;
        Logger.Debug($"Aircraft loaded: {aircraftPath}");

        LoadAdapterForAircraft(aircraftPath);
    }

    private static void OnSimulatorQuit()
    {
        _mainForm.Invoke(() =>
        {
            _mainForm.SetSimConnectStatus(false);
            _mainForm.SetGsxStatus(false);
        });
    }

    private static void LoadAdapterForAircraft(string aircraftPathOrTitle)
    {
        if (string.IsNullOrEmpty(aircraftPathOrTitle)) return;

        var newAdapter = AircraftAdapterMatcher.Create(aircraftPathOrTitle);

        // Skip if we already have the same adapter type running to avoid double-registration.
        // (Both the SystemState path and the TITLE SimVar change can fire for the same aircraft.)
        if (newAdapter?.GetType() == _currentAdapter?.GetType() && _currentAdapter != null)
        {
            Logger.Debug($"LoadAdapterForAircraft: adapter already loaded for '{aircraftPathOrTitle}', skipping.");
            return;
        }

        _currentAdapter = newAdapter;
        _doorManager.SetAdapter(newAdapter);

        if (newAdapter != null)
        {
            Logger.Success("Custom Aircraft Profile Found! -- Doors and Ground Equipment will be managed Automatically");
            if (_sc != null)
            {
                Logger.Debug($"Registering Adapter '{newAdapter.GetType().Name}' with Active SimConnect.");
                newAdapter.OnSimConnectConnected(_sc);
            }
            else
            {
                Logger.Debug($"Adapter '{newAdapter.GetType().Name}' created but SimConnect not yet connected; will register on next connect.");
            }
        }
        else
        {
            Logger.Info("No adapter registered for this aircraft; running in basic mode.");
        }
    }

    public static void PrintCurrentState()
    {
        _automationManager?.PrintState();
    }

    /// <summary>
    /// Re-registers the activation L:var for the currently loaded aircraft.
    /// Call this after the aircraft config is saved to apply changes immediately.
    /// </summary>
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

    /// <summary>
    /// Persists a hotkey change and updates the live listener.
    /// <paramref name="hotkeyType"/> is "activation" or "reset";
    /// <paramref name="hotkeyString"/> is the display string (e.g. "Ctrl+F1").
    /// </summary>
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
