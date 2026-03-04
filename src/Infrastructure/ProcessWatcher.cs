namespace SimpleGsxIntegrator.Infrastructure;

/// <summary>
/// Watches for the MSFS process to exit and optionally closes our application when it does.
/// Only activates if MSFS was already running at startup (prevents false exits when
/// the user manually launches the integrator before MSFS).
/// </summary>
public sealed class ProcessWatcher : IDisposable
{
    private static readonly string[] ProcessNames = { "FlightSimulator", "MSFS" };

    private const int PollIntervalMs = 5_000;

    private CancellationTokenSource? _cts;

    /// <summary>Fires when MSFS is detected to have exited.</summary>
    public event Action? MsfsExited;

    public void StartIfMsfsRunning()
    {
        if (!IsMsfsRunning())
        {
            Logger.Debug("ProcessWatcher: MSFS not running at startup – process watch skipped");
            return;
        }

        Logger.Debug("ProcessWatcher: MSFS detected at startup – will exit when MSFS closes");
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
                MsfsExited?.Invoke();
                return;
            }
        }
    }

    public static bool IsMsfsRunning()
        => System.Diagnostics.Process.GetProcesses()
            .Any(p => ProcessNames.Any(n =>
                p.ProcessName.Contains(n, StringComparison.OrdinalIgnoreCase)));
}
