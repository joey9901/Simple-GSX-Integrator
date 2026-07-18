using Microsoft.FlightSimulator.SimConnect;
using SimpleGsxIntegrator.Core;
using SimpleGsxIntegrator.Efb;

namespace SimpleGsxIntegrator.Aircraft.iFly;

internal sealed class IFly737Adapter : AircraftAdapterBase, IGroundEquipment, IClosableDoors, IArmableDoors
{
    private static readonly TimeSpan RequestCooldown = TimeSpan.FromSeconds(4);

    private readonly IEfbCommandRunner _efb;
    private readonly string _efbUrl;
    private bool? _chocksSet;
    private bool? _gpuConnected;
    private bool? _lastChocksRequest;
    private bool? _lastGpuRequest;
    private DateTime _lastChocksRequestTime;
    private DateTime _lastGpuRequestTime;

    private bool _anyOpenDoors = false;
    private int _openDoorCount = 0;

    public bool AnyDoorOpen => _anyOpenDoors;
    public int OpenDoorCount => _openDoorCount;

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

        sc.AddToDataDefinition(SimDef.IFly737Vars, IFly737Constants.LVar_NoseChock, "Number", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
        sc.AddToDataDefinition(SimDef.IFly737Vars, IFly737Constants.LVar_LeftChock, "Number", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
        sc.AddToDataDefinition(SimDef.IFly737Vars, IFly737Constants.LVar_RightChock, "Number", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
        sc.AddToDataDefinition(SimDef.IFly737Vars, IFly737Constants.LVar_Gpu, "Number", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
        sc.AddToDataDefinition(SimDef.IFly737Vars, IFly737Constants.LVar_Aft_Cargo, "Number", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
        sc.AddToDataDefinition(SimDef.IFly737Vars, IFly737Constants.LVar_Fwd_Cargo, "Number", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
        sc.AddToDataDefinition(SimDef.IFly737Vars, IFly737Constants.LVar_R_Mid_Exit, "Number", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
        sc.AddToDataDefinition(SimDef.IFly737Vars, IFly737Constants.LVar_L_Mid_Exit, "Number", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
        sc.AddToDataDefinition(SimDef.IFly737Vars, IFly737Constants.LVar_L_Fwd_OverWing_Exit, "Number", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
        sc.AddToDataDefinition(SimDef.IFly737Vars, IFly737Constants.LVar_R_Fwd_OverWing_Exit, "Number", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
        sc.AddToDataDefinition(SimDef.IFly737Vars, IFly737Constants.LVar_L_Aft_OverWing_Exit, "Number", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
        sc.AddToDataDefinition(SimDef.IFly737Vars, IFly737Constants.LVar_R_Aft_OverWing_Exit, "Number", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
        sc.AddToDataDefinition(SimDef.IFly737Vars, IFly737Constants.LVar_Fwd_Entry, "Number", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
        sc.AddToDataDefinition(SimDef.IFly737Vars, IFly737Constants.LVar_Aft_Entry, "Number", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
        sc.AddToDataDefinition(SimDef.IFly737Vars, IFly737Constants.LVar_Fwd_Service, "Number", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
        sc.AddToDataDefinition(SimDef.IFly737Vars, IFly737Constants.LVar_Aft_Service, "Number", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
        sc.RegisterDataDefineStruct<IFly737VarsStruct>(SimDef.IFly737Vars);
        sc.RequestDataOnSimObject(
            SimReq.IFly737Vars, SimDef.IFly737Vars,
            SimConnect.SIMCONNECT_OBJECT_ID_USER,
            SIMCONNECT_PERIOD.SECOND,
            SIMCONNECT_DATA_REQUEST_FLAG.CHANGED,
            0, 0, 0);

        // Pre-load the browser now so the first real command doesn't have to wait for load time
        _ = _efb.RunAsync(_efbUrl, Array.Empty<EfbCommand>());
    }

    public override void OnSimObjectData(SIMCONNECT_RECV_SIMOBJECT_DATA data)
    {
        if (data.dwRequestID != (uint)SimReq.IFly737Vars) return;

        var values = (IFly737VarsStruct)data.dwData[0];

        UpdateGroundEquipment(values);
        UpdateDoors(values);

        NotifyGroundEquipmentStateChanged();
    }

    private void UpdateGroundEquipment(IFly737VarsStruct values)
    {
        var chocksSet = values.NoseChock > 0.5 || values.LeftChock > 0.5 || values.RightChock > 0.5;
        var gpuConnected = values.Gpu > 0.5;

        if (_chocksSet == chocksSet && _gpuConnected == gpuConnected) return;
        _chocksSet = chocksSet;
        _gpuConnected = gpuConnected;
    }

    private void UpdateDoors(IFly737VarsStruct values)
    {
        var numberOfOpenDoors = 0;

        numberOfOpenDoors += values.AftCargoDoor > 0 ? 1 : 0;
        numberOfOpenDoors += values.FwdCargoDoor > 0 ? 1 : 0;
        numberOfOpenDoors += values.RMidExitDoor > 0 ? 1 : 0;
        numberOfOpenDoors += values.LMidExitDoor > 0 ? 1 : 0;
        numberOfOpenDoors += values.LFwdOverWingDoor > 0 ? 1 : 0;
        numberOfOpenDoors += values.RFwdOverWingDoor > 0 ? 1 : 0;
        numberOfOpenDoors += values.LAftOverWingDoor > 0 ? 1 : 0;
        numberOfOpenDoors += values.RAftOverWingDoor > 0 ? 1 : 0;
        numberOfOpenDoors += values.FwdEntryDoor > 0 ? 1 : 0;
        numberOfOpenDoors += values.AftEntryDoor > 0 ? 1 : 0;
        numberOfOpenDoors += values.FwdServiceDoor > 0 ? 1 : 0;
        numberOfOpenDoors += values.AftServiceDoor > 0 ? 1 : 0;

        _openDoorCount = numberOfOpenDoors;
        _anyOpenDoors = numberOfOpenDoors > 0;
    }

    public Task CloseOpenDoors()
    {
        Logger.Debug($"IFly737Adapter: Attempting to Close All Doors.");
        return _efb.RunAsync(_efbUrl, new EfbCommand[]
        {
            new NavigateTo(IFly737Constants.DoorsSelector),
            new ClickElement(IFly737Constants.CloseAllDoorsSelector),
            new NavigateTo(IFly737Constants.HomeButtonSelector),
        });
    }

    public async Task ArmAllDoors()
    {
        var deadline = DateTime.UtcNow.AddSeconds(15);
        while (DateTime.UtcNow < deadline && AnyDoorOpen)
        {
            await Task.Delay(1_000);
        }

        if (AnyDoorOpen)
        {
            Logger.Warning("IFly737Adapter: Failed to Arm All Doors - doors still open after 15s wait");
            return;
        }

        Logger.Debug("IFly737Adapter: Attempting to Arm All Doors.");
        await _efb.RunAsync(_efbUrl, new EfbCommand[]
        {
            new NavigateTo(IFly737Constants.DoorsSelector),
            new ClickElement(IFly737Constants.ArmAllDoorsSelector),
            new NavigateTo(IFly737Constants.HomeButtonSelector),
        });
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
