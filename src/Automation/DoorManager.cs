using SimpleGsxIntegrator.Aircraft;
using SimpleGsxIntegrator.Aircraft.Pmdg;
using SimpleGsxIntegrator.Gsx;

namespace SimpleGsxIntegrator.Automation;

/// <summary>
/// Reacts to GSX service-state changes to enforce door-close policies specific
/// to the current aircraft adapter (e.g. PMDG 777 doors reopened by GSX catering).
///
/// Door policies for PMDG 777:
///  - Cabin 1L (boarding door): close 15 s after boarding completes
///  - Cabin 1R, 2L–5R (service doors GSX may reopen): close when pushback is
///    requested and keep closed.
///  - Cargo Fwd/Aft: monitor during boarding, close if opened unexpectedly
///  - E&amp;E, avionics, bulk: always close before pushback
/// </summary>
public sealed class DoorManager
{
    // Doors that must be closed before pushback regardless of GSX interference.
    private static readonly IReadOnlySet<uint> ClosedBeforePushback = new HashSet<uint>
    {
        Pmdg777Constants.EVT_DOOR_1R,
        Pmdg777Constants.EVT_DOOR_2L, Pmdg777Constants.EVT_DOOR_2R,
        Pmdg777Constants.EVT_DOOR_3L, Pmdg777Constants.EVT_DOOR_3R,
        Pmdg777Constants.EVT_DOOR_4L, Pmdg777Constants.EVT_DOOR_4R,
        Pmdg777Constants.EVT_DOOR_5L, Pmdg777Constants.EVT_DOOR_5R,
        Pmdg777Constants.EVT_DOOR_CARGO_FWD,
        Pmdg777Constants.EVT_DOOR_CARGO_AFT,
        Pmdg777Constants.EVT_DOOR_CARGO_BULK,
        Pmdg777Constants.EVT_DOOR_AVIONICS,
        Pmdg777Constants.EVT_DOOR_EE_HATCH,
    };

    // Main boarding door (1L) is only closed after boarding completes + delay.
    private const uint MainBoardingDoor = Pmdg777Constants.EVT_DOOR_1L;

    private IAircraftAdapter? _adapter;
    private readonly GsxMonitor _gsx;

    /// <summary>The currently active aircraft adapter (read by AutomationManager for pre-pushback door close).</summary>
    public IAircraftAdapter? CurrentAdapter => _adapter;

    private CancellationTokenSource? _monitorCts;

    public DoorManager(GsxMonitor gsx)
    {
        _gsx = gsx;

        _gsx.BoardingStateChanged += OnBoardingStateChanged;
        _gsx.PushbackStateChanged += OnPushbackStateChanged;
    }

    /// <summary>
    /// Sets (or clears) the current aircraft adapter.
    /// Call this whenever the aircraft changes.
    /// </summary>
    public void SetAdapter(IAircraftAdapter? adapter)
    {
        StopMonitor();
        _adapter = adapter;
    }

    // -----------------------------------------------------------------
    //  Event handlers
    // -----------------------------------------------------------------

    private void OnBoardingStateChanged(GsxServiceState state)
    {
        if (_adapter == null) return;

        if (state == GsxServiceState.Completed)
        {
            _ = CloseDoorWithDelayAsync(MainBoardingDoor, delayMs: 15_000, reason: "boarding complete");
        }
    }

    private void OnPushbackStateChanged(GsxServiceState state)
    {
        if (_adapter == null) return;

        if (state == GsxServiceState.Requested || state == GsxServiceState.Active)
        {
            // All non-essential doors must be closed before the tug moves
            _ = EnforceAllDoorsClosedAsync(reason: "pushback requested");

            // Start a short monitor loop that re-closes any door GSX reopens
            StartMonitor(durationMs: 30_000);
        }
    }

    // -----------------------------------------------------------------
    //  Door-close helpers
    // -----------------------------------------------------------------

    private async Task CloseDoorWithDelayAsync(uint doorId, int delayMs, string reason)
    {
        await Task.Delay(delayMs);
        if (_adapter == null) return;

        Logger.Debug($"DoorManager: closing {Pmdg777Constants.GetDoorName(doorId)} ({reason})");
        _adapter.CloseDoor(doorId);
    }

    private async Task EnforceServiceDoorsClosedAsync(int delayMs, string reason)
    {
        await Task.Delay(delayMs);
        if (_adapter == null) return;

        var open = _adapter.GetOpenDoorIds();
        foreach (var id in ClosedBeforePushback)
        {
            if (open.Contains(id))
            {
                Logger.Info($"DoorManager: unexpected door open – closing {Pmdg777Constants.GetDoorName(id)} ({reason})");
                _adapter.CloseDoor(id);
                await Task.Delay(150);
            }
        }
    }

    private async Task EnforceAllDoorsClosedAsync(string reason)
    {
        if (_adapter == null) return;

        // Give a brief moment for any ongoing animation to settle
        await Task.Delay(2_000);

        var open = _adapter.GetOpenDoorIds();
        int closed = 0;

        foreach (var id in open)
        {
            Logger.Info($"DoorManager: closing {Pmdg777Constants.GetDoorName(id)} ({reason})");
            _adapter.CloseDoor(id);
            await Task.Delay(200);
            closed++;
        }

        if (closed > 0)
            Logger.Info($"DoorManager: closed {closed} door(s) before {reason}");
    }

    // -----------------------------------------------------------------
    //  Short re-close monitor (prevents GSX from re-opening doors)
    // -----------------------------------------------------------------

    private void StartMonitor(int durationMs)
    {
        StopMonitor();
        _monitorCts = new CancellationTokenSource();
        var token = _monitorCts.Token;

        _ = Task.Run(async () =>
        {
            var deadline = DateTime.UtcNow.AddMilliseconds(durationMs);

            while (!token.IsCancellationRequested && DateTime.UtcNow < deadline)
            {
                await Task.Delay(5_000, token).ContinueWith(_ => { }); // swallow cancellation

                if (_adapter == null || token.IsCancellationRequested) break;

                var open = _adapter.GetOpenDoorIds();
                foreach (var id in ClosedBeforePushback)
                {
                    if (open.Contains(id))
                    {
                        Logger.Debug($"DoorManager: re-closing {Pmdg777Constants.GetDoorName(id)} (monitor)");
                        _adapter.CloseDoor(id);
                    }
                }
            }
        }, token);
    }

    private void StopMonitor()
    {
        _monitorCts?.Cancel();
        _monitorCts = null;
    }
}
