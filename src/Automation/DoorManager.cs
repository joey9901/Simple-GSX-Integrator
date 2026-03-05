using SimpleGsxIntegrator.Aircraft;
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
    // The main boarding door ID is resolved from the active adapter (aircraft-specific).

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


    private void OnBoardingStateChanged(GsxServiceState state)
    {
        if (_adapter == null) return;

        if (state == GsxServiceState.Completed)
        {
            _ = CloseDoorWithDelayAsync(_adapter.MainBoardingDoorId, delayMs: 15_000, reason: "boarding complete");
        }
    }

    private void OnPushbackStateChanged(GsxServiceState state)
    {
        if (_adapter == null) return;

        if (state == GsxServiceState.Requested || state == GsxServiceState.Active)
        {
            _ = CloseAllDoorsAsync(reason: "pushback requested");
        }
    }

    private async Task CloseDoorWithDelayAsync(uint doorId, int delayMs, string reason)
    {
        await Task.Delay(delayMs);
        if (_adapter == null) return;

        Logger.Debug($"DoorManager: closing door 0x{doorId:X} ({reason})");
        _adapter.CloseDoor(doorId);
    }

    private async Task CloseAllDoorsAsync(string reason)
    {
        if (_adapter == null) return;

        await Task.Delay(2_000);

        var open = _adapter.GetOpenDoorIds();
        int closed = 0;

        foreach (var id in open)
        {
            Logger.Debug($"DoorManager: closing door 0x{id:X} ({reason})");
            _adapter.CloseDoor(id);
            await Task.Delay(200);
            closed++;
        }

        if (closed > 0)
            Logger.Debug($"DoorManager: closed {closed} door(s) before {reason}");
    }

    private void StopMonitor()
    {
        _monitorCts?.Cancel();
        _monitorCts = null;
    }
}
