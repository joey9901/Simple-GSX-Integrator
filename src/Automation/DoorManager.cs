using SimpleGsxIntegrator.Aircraft;
using SimpleGsxIntegrator.Gsx;

namespace SimpleGsxIntegrator.Automation;

/// <summary>
/// Reacts to GSX service-state changes to enforce door-close policies
/// (e.g. close boarding door after boarding completes, close all doors when pushback starts).
/// </summary>
public sealed class DoorManager
{
    private IAircraftAdapter? _adapter;
    private readonly GsxMonitor _gsx;
    public IAircraftAdapter? CurrentAdapter => _adapter;
    private CancellationTokenSource? _monitorCts;
    public DoorManager(GsxMonitor gsx)
    {
        _gsx = gsx;

        _gsx.BoardingStateChanged += OnBoardingStateChanged;
        _gsx.PushbackStateChanged += OnPushbackStateChanged;
    }

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
