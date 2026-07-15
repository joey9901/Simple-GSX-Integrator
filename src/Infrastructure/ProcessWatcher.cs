namespace SimpleGsxIntegrator.Infrastructure;

public sealed class ProcessWatcher : IDisposable
{
    private static readonly string[] ProcessNames = { "FlightSimulator", "FlightSimulator2024", "Microsoft Flight Simulator", "Microsoft Flight Simulator 2024" };

    private const int PollIntervalMs = 5_000;

    private CancellationTokenSource? _cts;

    public event Action? MsfsExited;

    public void Run()
    {
        // Already watching - don't start a second loop.
        if (_cts != null) return;

        if (!IsMsfsRunning())
        {
            Logger.Debug("ProcessWatcher: MSFS not running - process watch deferred");
            return;
        }

        Logger.Debug("ProcessWatcher: MSFS detected - will exit when MSFS closes");
        Start();
    }

    private void Start()
    {
        _cts = new CancellationTokenSource();
        _ = Task.Run(MonitorLoopAsync, _cts.Token);
    }

    public void Dispose() => _cts?.Cancel();

    private async Task MonitorLoopAsync()
    {
        while (_cts?.IsCancellationRequested == false)
        {
            await Task.Delay(PollIntervalMs);

            if (!IsMsfsRunning())
            {
                Logger.Info("ProcessWatcher: MSFS has exited");
                _cts = null;
                MsfsExited?.Invoke();
                return;
            }
        }
        _cts = null;
    }

    public static bool IsMsfsRunning()
    {
        return System.Diagnostics.Process.GetProcesses()
            .Any(p => ProcessNames.Any(n =>
                p.ProcessName.Contains(n, StringComparison.OrdinalIgnoreCase)));
    }
}
