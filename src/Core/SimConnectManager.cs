using Microsoft.FlightSimulator.SimConnect;

namespace SimpleGsxIntegrator.Core;

/// <summary>
/// Owns the single SimConnect connection and message pump.
/// All other components receive the SimConnect instance via the <see cref="Connected"/> event
/// to register their definitions and requests. Data is dispatched back through the
/// <see cref="SimObjectDataReceived"/> and <see cref="SystemStateReceived"/> events so
/// every component only needs to hold this manager – not a raw SimConnect reference.
/// </summary>
public sealed class SimConnectManager : IDisposable
{
    private SimConnect? _sc;

    /// <summary>
    /// Raised once after a successful SimConnect connection.
    /// Subscribers should register their SimConnect definitions inside this handler.
    /// </summary>
    public event Action<SimConnect>? Connected;

    /// <summary>Raised when SimConnect becomes unavailable (simulator quit).</summary>
    public event Action? Disconnected;

    /// <summary>
    /// Raised for every <c>OnRecvSimobjectData</c> callback.
    /// Each subscriber filters by <c>dwRequestID</c> to claim its own data.
    /// </summary>
    public event Action<SIMCONNECT_RECV_SIMOBJECT_DATA>? SimObjectDataReceived;

    /// <summary>Raised for every <c>OnRecvSystemState</c> callback.</summary>
    public event Action<SIMCONNECT_RECV_SYSTEM_STATE>? SystemStateReceived;

    /// <summary>Raised when the simulator sends a quit message.</summary>
    public event Action? SimulatorQuit;
    public bool IsConnected => _sc != null;

    /// <summary>
    /// Establishes a SimConnect connection and notifies subscribers via <see cref="Connected"/>.
    /// Must be called from the window-message thread (or pass a valid hWnd).
    /// </summary>
    public void Connect(IntPtr windowHandle)
    {
        _sc = new SimConnect("SimpleGSXIntegrator", windowHandle, 0, null, 0);

        _sc.OnRecvSimobjectData += (_, d) => SimObjectDataReceived?.Invoke(d);
        _sc.OnRecvSystemState += (_, d) => SystemStateReceived?.Invoke(d);
        _sc.OnRecvQuit += (_, _) => OnQuit();

        Connected?.Invoke(_sc);
    }

    /// <summary>
    /// Pumps the SimConnect message queue. Call frequently from a background loop.
    /// </summary>
    public void PumpMessages() => _sc?.ReceiveMessage();

    public void Dispose()
    {
        _sc?.Dispose();
        _sc = null;
    }

    private void OnQuit()
    {
        Logger.Info("SimConnect: simulator quit");
        Disconnected?.Invoke();
        SimulatorQuit?.Invoke();
    }
}
