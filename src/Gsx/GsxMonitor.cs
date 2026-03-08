using System.Runtime.InteropServices;
using Microsoft.FlightSimulator.SimConnect;
using SimpleGsxIntegrator.Core;

namespace SimpleGsxIntegrator.Gsx;

public sealed class GsxMonitor
{
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
    }

    private bool _gsxRunning;
    private GsxServiceState _boardingState = GsxServiceState.Unknown;
    private GsxServiceState _deboardingState = GsxServiceState.Unknown;
    private GsxServiceState _pushbackState = GsxServiceState.Unknown;
    private GsxServiceState _refuelingState = GsxServiceState.Unknown;
    private GsxServiceState _cateringState = GsxServiceState.Unknown;

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

    public void OnSimConnectConnected(SimConnect sc)
    {
        AddGsxVar(sc, GsxConstants.CouatlStarted);
        AddGsxVar(sc, GsxConstants.MenuOpen);
        AddGsxVar(sc, GsxConstants.MenuChoice);
        AddGsxVar(sc, GsxConstants.BoardingState);
        AddGsxVar(sc, GsxConstants.DeboardingState);
        AddGsxVar(sc, GsxConstants.PushbackState);
        AddGsxVar(sc, GsxConstants.RefuelingState);
        AddGsxVar(sc, GsxConstants.CateringState);

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
