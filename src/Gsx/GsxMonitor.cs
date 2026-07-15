using System.Runtime.InteropServices;
using Microsoft.FlightSimulator.SimConnect;
using SimpleGsxIntegrator.Core;

namespace SimpleGsxIntegrator.Gsx;

public sealed class GsxMonitor
{
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    private struct RemoteControlStruct { public double RemoteControl; }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi, Pack = 1)]
    private struct GsxStateStruct
    {
        public double CouatlStarted;
        public double MenuOpen;
        public double MenuChoice;
        public double BoardingState;
        public double DeboardingState;
        public double PushbackState;
        public double RefuelingState;
        public double CateringState;
        public double RemoteControl;
        public double RemotePort;
    }

    private SimConnect? _sc;
    private bool _gsxRunning;
    private GsxServiceState _boardingState = GsxServiceState.Unknown;
    private GsxServiceState _deboardingState = GsxServiceState.Unknown;
    private GsxServiceState _pushbackState = GsxServiceState.Unknown;
    private GsxServiceState _refuelingState = GsxServiceState.Unknown;
    private GsxServiceState _cateringState = GsxServiceState.Unknown;

    public bool ShouldDisableRemoteControl { get; set; }

    private string _remotePort = "8744";
    public string RemotePort => _remotePort;

    public bool IsGsxRunning
    {
        get { return _gsxRunning; }
    }

    public GsxServiceState BoardingState
    {
        get { return _boardingState; }
    }

    public GsxServiceState DeboardingState
    {
        get { return _deboardingState; }
    }

    public GsxServiceState PushbackState
    {
        get { return _pushbackState; }
    }

    public GsxServiceState RefuelingState
    {
        get { return _refuelingState; }
    }

    public GsxServiceState CateringState
    {
        get { return _cateringState; }
    }

    public event Action? GsxStarted;
    public event Action? GsxStopped;
    public event Action<GsxServiceState>? BoardingStateChanged;
    public event Action<GsxServiceState>? DeboardingStateChanged;
    public event Action<GsxServiceState>? PushbackStateChanged;
    public event Action<GsxServiceState>? RefuelingStateChanged;
    public event Action<GsxServiceState>? CateringStateChanged;
    public event Action<string>? RemotePortChanged;

    public void OnSimConnectConnected(SimConnect sc)
    {
        _sc = sc;

        // Write-only SimDef for SetDataOnSimObject — must not also appear in GsxState read definition
        sc.AddToDataDefinition(SimDef.GsxRemoteControl, GsxConstants.RemoteControl, null,
            SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
        sc.RegisterDataDefineStruct<RemoteControlStruct>(SimDef.GsxRemoteControl);

        AddGsxVar(sc, GsxConstants.CouatlStarted);
        AddGsxVar(sc, GsxConstants.MenuOpen);
        AddGsxVar(sc, GsxConstants.MenuChoice);
        AddGsxVar(sc, GsxConstants.BoardingState);
        AddGsxVar(sc, GsxConstants.DeboardingState);
        AddGsxVar(sc, GsxConstants.PushbackState);
        AddGsxVar(sc, GsxConstants.RefuelingState);
        AddGsxVar(sc, GsxConstants.CateringState);
        AddGsxVar(sc, GsxConstants.RemoteControl);
        AddGsxVar(sc, GsxConstants.RemotePort);

        sc.RegisterDataDefineStruct<GsxStateStruct>(SimDef.GsxState);

        sc.RequestDataOnSimObject(
            SimReq.GsxState,
            SimDef.GsxState,
            SimConnect.SIMCONNECT_OBJECT_ID_USER,
            SIMCONNECT_PERIOD.SECOND,
            SIMCONNECT_DATA_REQUEST_FLAG.CHANGED,
            0, 0, 0);

        Logger.Debug("GsxMonitor: SimConnect vars registered");
    }

    public void SetRemoteControl(bool enabled)
    {
        if (_sc == null) return;
        try
        {
            _sc.SetDataOnSimObject(SimDef.GsxRemoteControl, SimConnect.SIMCONNECT_OBJECT_ID_USER,
                SIMCONNECT_DATA_SET_FLAG.DEFAULT, new RemoteControlStruct { RemoteControl = enabled ? 1.0 : 0.0 });
            Logger.Debug($"GsxMonitor: {GsxConstants.RemoteControl} = {(enabled ? 1.0 : 0.0)}");
        }
        catch (Exception ex)
        {
            Logger.Warning($"GsxMonitor: SetRemoteControl failed: {ex.Message}");
        }
    }

    private void AddGsxVar(SimConnect sc, string lvar)
    {
        sc.AddToDataDefinition(SimDef.GsxState, lvar, null,
            SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
    }

    public void OnSimObjectData(SIMCONNECT_RECV_SIMOBJECT_DATA data)
    {
        if (data.dwRequestID != (uint)SimReq.GsxState) return;

        var raw = (GsxStateStruct)data.dwData[0];
        ProcessGsxState(raw);
    }

    private void ProcessGsxState(GsxStateStruct raw)
    {
        bool nowRunning = raw.CouatlStarted > 0;
        if (nowRunning != _gsxRunning)
        {
            _gsxRunning = nowRunning;
            if (_gsxRunning)
                GsxStarted?.Invoke();
            else
                GsxStopped?.Invoke();
        }

        UpdateState(ref _boardingState, BoardingStateChanged, raw.BoardingState);
        UpdateState(ref _deboardingState, DeboardingStateChanged, raw.DeboardingState);
        UpdateState(ref _pushbackState, PushbackStateChanged, raw.PushbackState);
        UpdateState(ref _refuelingState, RefuelingStateChanged, raw.RefuelingState);
        UpdateState(ref _cateringState, CateringStateChanged, raw.CateringState);

        if (ShouldDisableRemoteControl && raw.RemoteControl == 1.0)
            SetRemoteControl(false);

        if (raw.RemotePort != 0)
        {
            var port = ((int)raw.RemotePort).ToString();
            if (port != _remotePort)
            {
                _remotePort = port;
                RemotePortChanged?.Invoke(_remotePort);
                Logger.Debug($"Remote Port changed: {_remotePort}");
            }
        }
    }

    private static void UpdateState(
        ref GsxServiceState field,
        Action<GsxServiceState>? evt,
        double rawValue)
    {
        var next = (GsxServiceState)(int)rawValue;
        if (next == field) return;

        field = next;
        evt?.Invoke(next);
    }
}
