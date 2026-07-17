using Microsoft.FlightSimulator.SimConnect;
using SimpleGsxIntegrator.Core;
using SimpleGsxIntegrator.Efb;

namespace SimpleGsxIntegrator.Aircraft.iFly;

internal sealed class IFly737Adapter : AircraftAdapterBase, IGroundEquipment
{
    // GPU is a pure toggle in the EFB (not an absolute set), and the confirmed SimConnect read lags
    // several seconds behind a command actually finishing. A second reconcile trigger firing inside
    // that lag window would see stale "not yet matching" state and send another toggle, flipping GPU
    // right back. This cooldown suppresses a repeat request for the same value until that lag has
    // had time to clear, while still letting a genuine retry-after-failure through afterwards.
    private static readonly TimeSpan RequestCooldown = TimeSpan.FromSeconds(4);

    private readonly IEfbCommandRunner _efb;
    private readonly string _efbUrl;
    private bool? _chocksSet;
    private bool? _gpuConnected;
    private bool? _lastChocksRequest;
    private bool? _lastGpuRequest;
    private DateTime _lastChocksRequestTime;
    private DateTime _lastGpuRequestTime;

    public override string DisplayName => "iFly 737Max";
    public override string parkingBrakeVariable => IFly737Constants.LVar_ParkingBrake;

    public bool? ChocksSet => _chocksSet;
    public bool? GpuConnected => _gpuConnected;

    public IFly737Adapter(IEfbCommandRunner efb)
    {
        _efb = efb;
        _efbUrl = IFly737Constants.EfbUrl;
    }

    public override void OnSimConnectConnected(SimConnect sc)
    {
        base.OnSimConnectConnected(sc);

        sc.AddToDataDefinition(SimDef.IFly737GroundState, IFly737Constants.LVar_NoseChock, "Number", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
        sc.AddToDataDefinition(SimDef.IFly737GroundState, IFly737Constants.LVar_LeftChock, "Number", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
        sc.AddToDataDefinition(SimDef.IFly737GroundState, IFly737Constants.LVar_RightChock, "Number", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
        sc.AddToDataDefinition(SimDef.IFly737GroundState, IFly737Constants.LVar_Gpu, "Number", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
        sc.RegisterDataDefineStruct<IFly737GroundStateStruct>(SimDef.IFly737GroundState);
        sc.RequestDataOnSimObject(
            SimReq.IFly737GroundState, SimDef.IFly737GroundState,
            SimConnect.SIMCONNECT_OBJECT_ID_USER,
            SIMCONNECT_PERIOD.SECOND,
            SIMCONNECT_DATA_REQUEST_FLAG.CHANGED,
            0, 0, 0);

        // Pre-warm the browser now so the first real command doesn't pay the launch+load cost.
        _ = _efb.RunAsync(_efbUrl, Array.Empty<EfbCommand>());
    }

    public override void OnSimObjectData(SIMCONNECT_RECV_SIMOBJECT_DATA data)
    {
        if (data.dwRequestID != (uint)SimReq.IFly737GroundState) return;

        var s = (IFly737GroundStateStruct)data.dwData[0];
        var chocksSet = s.NoseChock > 0.5 || s.LeftChock > 0.5 || s.RightChock > 0.5;
        var gpuConnected = s.Gpu > 0.5;

        if (_chocksSet == chocksSet && _gpuConnected == gpuConnected) return;
        _chocksSet = chocksSet;
        _gpuConnected = gpuConnected;
        NotifyGroundEquipmentStateChanged();
    }

    public Task SetChocks(bool placed)
    {
        if (_chocksSet == placed) return Task.CompletedTask;
        if (_lastChocksRequest == placed && DateTime.UtcNow - _lastChocksRequestTime < RequestCooldown) return Task.CompletedTask;
        _lastChocksRequest = placed;
        _lastChocksRequestTime = DateTime.UtcNow;

        Logger.Debug($"IFly737Adapter: Attempting Chocks → {(placed ? "SET" : "REMOVED")}");
        return _efb.RunAsync(_efbUrl, new EfbCommand[]
        {
            new NavigateTo(IFly737Constants.GroundServicesSelector),
            new SetCheckbox(IFly737Constants.NoseWheelSelector, placed),
            new SetCheckbox(IFly737Constants.MainLeftWheelSelector, placed),
            new SetCheckbox(IFly737Constants.MainRightWheelSelector, placed),
            new NavigateTo(IFly737Constants.HomeButtonSelector),
        });
    }

    public Task SetGpu(bool connected)
    {
        if (_gpuConnected == connected) return Task.CompletedTask;
        if (_lastGpuRequest == connected && DateTime.UtcNow - _lastGpuRequestTime < RequestCooldown) return Task.CompletedTask;
        _lastGpuRequest = connected;
        _lastGpuRequestTime = DateTime.UtcNow;

        Logger.Debug($"IFly737Adapter: Attempting GPU → {(connected ? "ON" : "OFF")}");
        return _efb.RunAsync(_efbUrl, new EfbCommand[]
        {
            new NavigateTo(IFly737Constants.GroundServicesSelector),
            new DispatchClick(IFly737Constants.GpuSelector),
            new NavigateTo(IFly737Constants.HomeButtonSelector),
        });
    }

    public override void Dispose()
    {
        Logger.Debug("IFly737Adapter: disposed");
    }
}
