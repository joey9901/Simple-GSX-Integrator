using Microsoft.FlightSimulator.SimConnect;

namespace SimpleGsxIntegrator.Core;

public sealed class SimConnectManager : IDisposable
{
    private SimConnect? _sc;

    public event Action<SimConnect>? Connected;
    public event Action? Disconnected;

    public event Action<SIMCONNECT_RECV_SIMOBJECT_DATA>? SimObjectDataReceived;
    public event Action<SIMCONNECT_RECV_SYSTEM_STATE>? SystemStateReceived;

    public event Action? SimulatorQuit;
    public bool IsConnected => _sc != null;

    public void Connect(IntPtr windowHandle)
    {
        _sc = new SimConnect("SimpleGSXIntegrator", windowHandle, 0, null, 0);

        _sc.OnRecvSimobjectData += OnRecvSimObjectData;
        _sc.OnRecvSystemState += OnRecvSystemState;
        _sc.OnRecvQuit += OnRecvQuit;

        Connected?.Invoke(_sc);
    }

    public void PumpMessages()
    {
        _sc?.ReceiveMessage();
    }

    public void Dispose()
    {
        _sc?.Dispose();
        _sc = null;
    }

    private void OnRecvSimObjectData(SimConnect _, SIMCONNECT_RECV_SIMOBJECT_DATA data)
    {
        SimObjectDataReceived?.Invoke(data);
    }

    private void OnRecvSystemState(SimConnect _, SIMCONNECT_RECV_SYSTEM_STATE data)
    {
        SystemStateReceived?.Invoke(data);
    }

    private void OnRecvQuit(SimConnect _, SIMCONNECT_RECV data)
    {
        OnQuit();
    }

    private void OnQuit()
    {
        Logger.Info("SimConnect: simulator quit");
        Disconnected?.Invoke();
        SimulatorQuit?.Invoke();
    }
}
