using Microsoft.FlightSimulator.SimConnect;
using SimpleGsxIntegrator.Core;
using SimpleGsxIntegrator.Efb;

namespace SimpleGsxIntegrator.Aircraft.iFly;

internal sealed class IFly737Adapter : AircraftAdapterBase, IGroundEquipment
{
    private readonly IEfbCommandRunner _efb;
    private readonly string _efbUrl;
    private bool? _chocksSet;
    private bool? _gpuConnected;

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
