using System.Runtime.InteropServices;
using Microsoft.FlightSimulator.SimConnect;
using SimpleGsxIntegrator.Core;

namespace SimpleGsxIntegrator.Gsx;

/// <summary>
/// Monitors all GSX service-state L:vars via SimConnect.
/// Exposes current state as properties and raises strongly-typed events on state changes.
///
/// Registration wires into <see cref="SimConnectHub.Connected"/>; data arrives via
/// <see cref="SimConnectHub.SimObjectDataReceived"/>.
/// </summary>
public sealed class GsxMonitor
{
    // -----------------------------------------------------------------
    //  SimConnect struct
    // -----------------------------------------------------------------

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi, Pack = 1)]
    private struct GsxStateStruct
    {
        public double CouatlStarted;
        public double MenuOpen;
        public double MenuChoice;
        public double BoardingState;
        public double DeboardingState;
        public double DepartureState;   // pushback
        public double PushbackStatus;
        public double RefuelingState;
        public double CateringState;
    }

    // -----------------------------------------------------------------
    //  State
    // -----------------------------------------------------------------

    private bool _gsxRunning;
    private GsxServiceState _boarding = GsxServiceState.Unknown;
    private GsxServiceState _deboarding = GsxServiceState.Unknown;
    private GsxServiceState _pushback = GsxServiceState.Unknown;
    private GsxServiceState _refueling = GsxServiceState.Unknown;
    private GsxServiceState _catering = GsxServiceState.Unknown;
    private int _pushbackProgress;

    // -----------------------------------------------------------------
    //  Public state  (snapshot properties)
    // -----------------------------------------------------------------

    public bool IsGsxRunning => _gsxRunning;
    public GsxServiceState Boarding => _boarding;
    public GsxServiceState Deboarding => _deboarding;
    public GsxServiceState Pushback => _pushback;
    public GsxServiceState Refueling => _refueling;
    public GsxServiceState Catering => _catering;

    /// <summary>
    /// GSX pushback progress status (0 = idle, 1–4 = in progress, 5 = complete).
    /// </summary>
    public int PushbackProgress => _pushbackProgress;

    // -----------------------------------------------------------------
    //  Events
    // -----------------------------------------------------------------

    /// <summary>Fires when the GSX Couatl engine starts (GSX becomes active).</summary>
    public event Action? GsxStarted;

    /// <summary>Fires when the GSX Couatl engine stops.</summary>
    public event Action? GsxStopped;

    public event Action<GsxServiceState>? BoardingStateChanged;
    public event Action<GsxServiceState>? DeboardingStateChanged;
    public event Action<GsxServiceState>? PushbackStateChanged;
    public event Action<GsxServiceState>? RefuelingStateChanged;
    public event Action<GsxServiceState>? CateringStateChanged;

    // -----------------------------------------------------------------
    //  SimConnect wiring
    // -----------------------------------------------------------------

    /// <summary>Wire this to <see cref="SimConnectHub.Connected"/>.</summary>
    public void OnSimConnectConnected(SimConnect sc)
    {
        void Add(string lvar)
            => sc.AddToDataDefinition(SimDef.GsxState, lvar, null,
                SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);

        Add(GsxConstants.CouatlStarted);
        Add(GsxConstants.MenuOpen);
        Add(GsxConstants.MenuChoice);
        Add(GsxConstants.BoardingState);
        Add(GsxConstants.DeboardingState);
        Add(GsxConstants.DepartureState);
        Add(GsxConstants.PushbackStatus);
        Add(GsxConstants.RefuelingState);
        Add(GsxConstants.CateringState);

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

    // -----------------------------------------------------------------
    //  Data handling
    // -----------------------------------------------------------------

    /// <summary>Wire this to <see cref="SimConnectHub.SimObjectDataReceived"/>.</summary>
    public void OnSimObjectData(SIMCONNECT_RECV_SIMOBJECT_DATA data)
    {
        if (data.dwRequestID != (uint)SimReq.GsxState) return;

        var raw = (GsxStateStruct)data.dwData[0];
        ProcessGsxState(raw);
    }

    private void ProcessGsxState(GsxStateStruct raw)
    {
        // ---- GSX running state ----
        bool nowRunning = raw.CouatlStarted > 0;
        if (nowRunning != _gsxRunning)
        {
            _gsxRunning = nowRunning;
            if (_gsxRunning)
                GsxStarted?.Invoke();
            else
                GsxStopped?.Invoke();
        }

        // ---- Service states ----
        UpdateState(ref _boarding, ref BoardingStateChanged, raw.BoardingState);
        UpdateState(ref _deboarding, ref DeboardingStateChanged, raw.DeboardingState);
        UpdateState(ref _pushback, ref PushbackStateChanged, raw.DepartureState);
        UpdateState(ref _refueling, ref RefuelingStateChanged, raw.RefuelingState);
        UpdateState(ref _catering, ref CateringStateChanged, raw.CateringState);

        _pushbackProgress = (int)raw.PushbackStatus;
    }

    private static void UpdateState(
        ref GsxServiceState field,
        ref Action<GsxServiceState>? evt,
        double rawValue)
    {
        var next = (GsxServiceState)(int)rawValue;
        if (next == field) return;

        field = next;
        evt?.Invoke(next);
    }
}
