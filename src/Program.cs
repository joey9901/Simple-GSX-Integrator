using System.Runtime.InteropServices;
using Microsoft.FlightSimulator.SimConnect;
using SimpleGsxIntegrator.Aircraft;
using SimpleGsxIntegrator.Aircraft.A300;
using SimpleGsxIntegrator.Aircraft.A330;
using SimpleGsxIntegrator.Aircraft.Aerosoft;
using SimpleGsxIntegrator.Aircraft.Fenix;
using SimpleGsxIntegrator.Aircraft.FlyByWire;
using SimpleGsxIntegrator.Aircraft.FSLabs;
using SimpleGsxIntegrator.Aircraft.FSS;
using SimpleGsxIntegrator.Aircraft.iniBuilds;
using SimpleGsxIntegrator.Aircraft.JustFlight;
using SimpleGsxIntegrator.Aircraft.Pmdg;
using SimpleGsxIntegrator.Aircraft.TFDi;
using SimpleGsxIntegrator.Aircraft.iFly;
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

        _procWatcher.Run();

        _manager.Connected += OnSimConnectConnected;
        _manager.Disconnected += OnSimConnectDisconnected;
        _manager.SimObjectDataReceived += OnSimObjectData;
        _manager.SystemStateReceived += OnSystemStateReceived;
        _manager.SimulatorQuit += OnSimulatorQuit;

        _automationManager.ActivationChanged += OnActivationChanged;
        _automationManager.ServiceTimedOut += key => _MainWindow.Invoke(() => _MainWindow.SendMessage(new { type = "serviceStatus", service = key, status = "failed" }));

        _flightState.AircraftChanged += OnAircraftTitleChanged;

        _flightState.BeaconChanged += RefreshAircraftStateDetails;
        _flightState.ParkingBrakeChanged += RefreshAircraftStateDetails;
        _flightState.EngineChanged += RefreshAircraftStateDetails;
        _flightState.EnginesEverRunChanged += RefreshAircraftStateDetails;
        _flightState.HasMovedChanged += RefreshAircraftStateDetails;

        _gsxMonitor.GsxStarted += OnGsxStarted;
        _gsxMonitor.GsxStopped += OnGsxStopped;
        _gsxMonitor.RemotePortChanged += OnRemotePortChanged;
        _gsxMonitor.BoardingStateChanged += s => _MainWindow.Invoke(() => _MainWindow.SendMessage(new { type = "serviceStatus", service = "boarding", status = s.ToString().ToLower() }));
        _gsxMonitor.DeboardingStateChanged += s => _MainWindow.Invoke(() => _MainWindow.SendMessage(new { type = "serviceStatus", service = "deboard", status = s.ToString().ToLower() }));
        _gsxMonitor.PushbackStateChanged += s => _MainWindow.Invoke(() => _MainWindow.SendMessage(new { type = "serviceStatus", service = "pushback", status = s.ToString().ToLower() }));
        _gsxMonitor.RefuelingStateChanged += s => _MainWindow.Invoke(() => _MainWindow.SendMessage(new { type = "serviceStatus", service = "boarding", status = s.ToString().ToLower() }));
        _gsxMonitor.CateringStateChanged += s => _MainWindow.Invoke(() => _MainWindow.SendMessage(new { type = "serviceStatus", service = "boarding", status = s.ToString().ToLower() }));

        _hotkeys.ActivationPressed += OnActivationKeyPressed;
        _hotkeys.ResetPressed += OnResetKeyPressed;

        _procWatcher.MsfsExited += OnMsfsExited;

        _MainWindow.Show();

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

        _procWatcher.Run();

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
        _rawAircraftTitle = string.Empty;
        CurrentAircraftPath = string.Empty;
        CurrentAircraftTitle = string.Empty;
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

        var path = data.szString?.Trim() ?? string.Empty;
        if (string.IsNullOrEmpty(path) || path == CurrentAircraftPath) return;

        CurrentAircraftPath = path;
        Logger.Debug($"Aircraft loaded: {path}");

        ApplyResolvedAdapter(path, _rawAircraftTitle);
    }

    private static readonly (Func<string, string, bool> Matches, Func<AircraftAdapterBase> Create)[] AdapterCatalog =
    {
        ((path, title) => path.Contains("PMDG 777", StringComparison.OrdinalIgnoreCase), () => new Pmdg777Adapter()),
        ((path, title) => path.Contains("PMDG 737", StringComparison.OrdinalIgnoreCase), () => new Pmdg737Adapter()),
        ((path, title) => path.Contains("a346-pro", StringComparison.OrdinalIgnoreCase), () => new AeroA346Adapter()),
        ((path, title) => path.Contains("TFDi_Design_MD-11", StringComparison.OrdinalIgnoreCase), () => new TfdiMd11Adapter()),
        ((path, title) => path.Contains("FSLabs", StringComparison.OrdinalIgnoreCase), () => new FSLabsA320Adapter()),
        ((path, title) => path.Contains("FlyByWire_A380", StringComparison.OrdinalIgnoreCase), () => new FbwA380Adapter()),
        ((path, title) => path.Contains("FlyByWire_A320", StringComparison.OrdinalIgnoreCase), () => new FbwA32NXAdapter()),
        ((path, title) => path.Contains("inibuilds", StringComparison.OrdinalIgnoreCase) && path.Contains("a300", StringComparison.OrdinalIgnoreCase), () => new IniA300Adapter()),
        ((path, title) => path.Contains("inibuilds", StringComparison.OrdinalIgnoreCase) && path.Contains("a330", StringComparison.OrdinalIgnoreCase), () => new IniA330Adapter()),
        ((path, title) => path.Contains("inibuilds-a340", StringComparison.OrdinalIgnoreCase), () => new IniA340Adapter()),
        ((path, title) => path.Contains("inibuilds", StringComparison.OrdinalIgnoreCase) && path.Contains("A350", StringComparison.OrdinalIgnoreCase), () => new IniA350Adapter()),
        ((path, title) => path.Contains("FNX_320", StringComparison.OrdinalIgnoreCase) || path.Contains("FNX_321", StringComparison.OrdinalIgnoreCase) || path.Contains("FNX_319", StringComparison.OrdinalIgnoreCase), () => new FenixA320Adapter()),
        ((path, title) => path.Contains("Just Flight Fokker", StringComparison.OrdinalIgnoreCase), () => new JustFlightF100Adapter()),
        ((path, title) => path.Contains("iFly 737", StringComparison.OrdinalIgnoreCase), () => new IFly737Adapter()),

        ((path, title) => title.Contains("FSS Embraer", StringComparison.OrdinalIgnoreCase), () => new FSSEJetsAdapter()),
    };

    public static AircraftAdapterBase? ResolveAdapter(string aircraftPath, string aircraftTitle)
    {
        var path = aircraftPath ?? "";
        var title = aircraftTitle ?? "";

        foreach (var entry in AdapterCatalog)
            if (entry.Matches(path, title))
                return entry.Create();

        return null;
    }

    public static IReadOnlySet<string> GetKnownAdapterDisplayNames() =>
        AdapterCatalog.Select(entry => entry.Create().DisplayName).ToHashSet(StringComparer.OrdinalIgnoreCase);

    public static AircraftAdapterBase? CreateAdapterByDisplayName(string displayName)
    {
        foreach (var entry in AdapterCatalog)
        {
            var adapter = entry.Create();
            if (string.Equals(adapter.DisplayName, displayName, StringComparison.OrdinalIgnoreCase))
                return adapter;
        }
        return null;
    }

    private static void ApplyResolvedAdapter(string path, string title)
    {
        var adapter = ResolveAdapter(path, title);
        var displayName = adapter?.DisplayName ?? title;

        if (displayName != CurrentAircraftTitle)
        {
            CurrentAircraftTitle = displayName;
            _MainWindow.Invoke(() => _MainWindow.SendMessage(new { type = "aircraft", title = CurrentAircraftTitle }));
        }

        LoadAdapterForAircraft(adapter);
    }

    private static void OnActivationChanged(bool isActive)
    {
        Logger.Debug($"Activation changed: {isActive}");
        _MainWindow.Invoke(() => _MainWindow.SendMessage(new { type = "system", active = isActive }));
        _MainWindow.Invoke(() => _MainWindow.SendMessage(new { type = "aircraft", title = CurrentAircraftTitle }));
    }

    private static void OnAircraftTitleChanged(string title)
    {
        bool isFirstTitleSinceConnect = string.IsNullOrEmpty(_rawAircraftTitle);
        _rawAircraftTitle = title;

        if (isFirstTitleSinceConnect)
        {
            ApplyResolvedAdapter(CurrentAircraftPath, title);
        }
        else
        {
            CurrentAircraftTitle = "";
            _MainWindow.Invoke(() => _MainWindow.SendMessage(new { type = "aircraft", title = "" }));
        }

        _MainWindow.Invoke(() => RefreshAircraftStateDetails(false));

        // Request the correct path; OnSystemStateReceived will (re)resolve once it arrives.
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

    public static void OnRemotePortChanged(string port)
    {
        Logger.Debug($"Port changed, sending to JS: {port}");
        _MainWindow.Invoke(() => _MainWindow.SendMessage(new { type = "remotePort", remotePort = port }));
    }

    public static void RefreshRemotePort()
    {
        var port = _gsxMonitor?.RemotePort;
        if (!string.IsNullOrEmpty(port))
            _MainWindow.SendMessage(new { type = "remotePort", remotePort = port });
    }

    private static void OnGsxStarted()
    {
        _MainWindow.Invoke(() => _MainWindow.SendMessage(new { type = "gsx", running = true }));
        RefreshServiceStates();
        ApplyRemoteControlSetting();
    }

    private static void OnGroundStateChanged()
    {
        var adapter = _automationManager.CurrentAdapter;
        SendGroundEquipState(adapter);
    }

    private static void SendGroundEquipState(AircraftAdapterBase? adapter)
    {
        var ground = adapter as IGroundEquipment;
        var closableDoors = adapter as IClosableDoors;
        var cargoDoor = adapter as ICargoDoor;

        int? openDoors = closableDoors != null
            ? closableDoors.OpenDoorCount
            : cargoDoor != null
                ? cargoDoor.DoorOpen == true ? 1 : cargoDoor.DoorOpen == false ? 0 : null
                : null;

        _MainWindow.Invoke(() => _MainWindow.SendMessage(new
        {
            type = "groundEquip",
            canManageGroundEquipment = ground != null,
            showDoors = closableDoors != null || cargoDoor != null,
            chocks = ground?.ChocksSet,
            gpu = ground?.GpuConnected,
            openDoors,
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

    private static void LoadAdapterForAircraft(AircraftAdapterBase? adapter)
    {
        if (adapter?.DisplayName == _automationManager.CurrentAircraftDisplayName && _automationManager.CurrentAdapter != null)
        {
            Logger.Debug("LoadAdapterForAircraft: adapter already loaded, skipping.");
            return;
        }

        var prevAdapter = _automationManager.CurrentAdapter;
        if (prevAdapter != null) prevAdapter.GroundEquipmentStateChanged -= OnGroundStateChanged;
        _automationManager.SetCurrentAdapter(adapter);

        if (_sc != null)
            _flightState.OnSimConnectConnected(_sc, adapter);

        if (adapter == null)
        {
            Logger.Info("No profile found for this aircraft.");
            SendGroundEquipState(null);
            return;
        }

        adapter.GroundEquipmentStateChanged += OnGroundStateChanged;
        SendGroundEquipState(adapter);

        if (_sc != null)
            adapter.OnSimConnectConnected(_sc);

        var options = new List<string>();
        if (adapter is IGroundEquipment) options.Add("Ground Equipment");
        if (adapter is IEngineCovers) options.Add("Engine Covers");
        if (adapter is IClosableDoors) options.Add("Closable Doors");
        if (adapter is ICargoDoor) options.Add("Cargo Door");

        Logger.Success(options.Count > 0
            ? $"{adapter.DisplayName} detected. Options: {string.Join(", ", options)}"
            : $"{adapter.DisplayName} detected. No automation options available.");
    }

    public static void PrintCurrentState()
    {
        _automationManager?.PrintState();
    }

    public static void ApplyAdapterConfig(string configTitle)
    {
        if (_automationManager.CurrentAdapter == null || _automationManager.CurrentAircraftDisplayName != configTitle) return;
        ApplyRemoteControlSetting();
    }

    private static void ApplyRemoteControlSetting()
    {
        if (string.IsNullOrEmpty(CurrentAircraftTitle)) return;
        var cfg = ConfigManager.GetAircraftConfig(CurrentAircraftTitle);
        _gsxMonitor.ShouldDisableRemoteControl = cfg.DisableRemoteControl;
        if (cfg.DisableRemoteControl && _gsxMonitor.IsGsxRunning)
            _gsxMonitor.SetRemoteControl(false);
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

    public static void RefreshGroundEquipState() => SendGroundEquipState(_automationManager?.CurrentAdapter);

    public static void RefreshDisplayState() => RefreshAircraftStateDetails(false);

    public static void RefreshServiceStates()
    {
        if (_gsxMonitor == null) return;
        _MainWindow.Invoke(() =>
        {
            _MainWindow.SendMessage(new { type = "serviceStatus", service = "boarding", status = _gsxMonitor.BoardingState.ToString().ToLower() });
            _MainWindow.SendMessage(new { type = "serviceStatus", service = "pushback", status = _gsxMonitor.PushbackState.ToString().ToLower() });
            _MainWindow.SendMessage(new { type = "serviceStatus", service = "deboard", status = _gsxMonitor.DeboardingState.ToString().ToLower() });
        });
    }

    private static void RefreshAircraftStateDetails(bool _)
    {
        _MainWindow.Invoke(() => _MainWindow.SendMessage(new
        {
            type = "state",
            beaconOn = _flightState.BeaconOn,
            enginesOn = _flightState.EngineOn,
            parkingBrake = _flightState.ParkingBrake,
            enginesEverRan = _flightState.HasEnginesEverRun,
            hasMoved = _flightState.HasMoved
        }));
    }

}
