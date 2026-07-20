using System.Diagnostics;
using Microsoft.FlightSimulator.SimConnect;
using SimpleGsxIntegrator.Core;
using SimpleGsxIntegrator.Efb;
using SimpleGsxIntegrator.Gsx;

namespace SimpleGsxIntegrator.Aircraft.FSLabs;

internal sealed class FSLabsA320Adapter : AircraftAdapterBase, IGroundEquipment, IEfbRunner
{
    public override string DisplayName => "FSLabs A32NX";
    public override string parkingBrakeVariable => FSLabsA320Constants.LVar_ParkingBrake;

    private readonly IEfbCommandRunner _efb;
    private readonly GsxMenuController _gsxMenu;
    private readonly string _efbUrl;
    private bool _efbLoaded = false;

    private static readonly TimeSpan RequestCooldown = TimeSpan.FromSeconds(4);
    private bool? _lastChocksRequest;
    private bool? _lastGpuRequest;
    private DateTime _lastChocksRequestTime;
    private DateTime _lastGpuRequestTime;
    private bool _chocksSet = false;
    private bool _gpuConnected = false;

    public bool? ChocksSet => _chocksSet;
    public bool? GpuConnected => _gpuConnected;

    public FSLabsA320Adapter(IEfbCommandRunner efb, GsxMenuController gsxMenu)
    {
        _efb = efb;
        _gsxMenu = gsxMenu;
        _efbUrl = FSLabsA320Constants.EfbUrl;
    }

    public override void OnSimConnectConnected(SimConnect sc)
    {
        base.OnSimConnectConnected(sc);

        sc.AddToDataDefinition(SimDef.FSLabsA320Vars, FSLabsA320Constants.LVar_Chocks, "Number", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
        sc.AddToDataDefinition(SimDef.FSLabsA320Vars, FSLabsA320Constants.LVar_Gpu, "Number", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);

        sc.RegisterDataDefineStruct<FSLabsA320VarsStruct>(SimDef.FSLabsA320Vars);
        sc.RequestDataOnSimObject(
            SimReq.FSLabsA320Vars, SimDef.FSLabsA320Vars,
            SimConnect.SIMCONNECT_OBJECT_ID_USER,
            SIMCONNECT_PERIOD.SECOND,
            SIMCONNECT_DATA_REQUEST_FLAG.CHANGED,
            0, 0, 0);
    }

    public override void OnSimObjectData(SIMCONNECT_RECV_SIMOBJECT_DATA data)
    {
        if (data.dwRequestID != (uint)SimReq.FSLabsA320Vars) return;

        var values = (FSLabsA320VarsStruct)data.dwData[0];

        _gpuConnected = values.Gpu > 0;
        _chocksSet = values.Chocks > 0;

        NotifyGroundEquipmentStateChanged();
    }

    public async Task PreloadEfb()
    {
        _efbLoaded = true;

        await _efb.RunAsync(_efbUrl, new EfbCommand[]
        {
            new ClickElement(FSLabsA320Constants.ZeroKey),
            new ClickElement(FSLabsA320Constants.ZeroKey),
            new ClickElement(FSLabsA320Constants.ZeroKey),
            new ClickElement(FSLabsA320Constants.ZeroKey),
        });
    }

    public async Task DisposeEfb()
    {
        _efbLoaded = false;
        if (_efb is IAsyncDisposable disposable)
            await disposable.DisposeAsync();
    }

    public async Task SetChocks(bool placed)
    {
        if (!_efbLoaded) _ = PreloadEfb();
        if (_chocksSet == placed) return;
        if (_lastChocksRequest == placed && DateTime.UtcNow - _lastChocksRequestTime < RequestCooldown) return;
        _lastChocksRequest = placed;
        _lastChocksRequestTime = DateTime.UtcNow;

        Logger.Debug($"FSLabsA320Adapter: Attempting Chocks → {(placed ? "SET" : "REMOVED")}");

        if (!placed)
        {
            await _efb.RunAsync(_efbUrl, new EfbCommand[]
            {
                new NavigateTo(FSLabsA320Constants.GroundServicesSelector),
                new ClickElement(FSLabsA320Constants.ChocksSelector),
                new ClickElement(FSLabsA320Constants.GpuSelector),
                new NavigateTo(FSLabsA320Constants.HomeButtonSelector),
            });
        }
        else
        {
            await _efb.RunAsync(_efbUrl, new EfbCommand[]
            {
                new NavigateTo(FSLabsA320Constants.GroundServicesSelector),
                new ClickElement(FSLabsA320Constants.ChocksSelector),
                new NavigateTo(FSLabsA320Constants.HomeButtonSelector),
            });
        }

        // The chocks button toggles rather than sets explicitly - wait for the L:var to confirm
        // this click before allowing another, or a re-click before confirmation would undo it.
        var deadline = DateTime.UtcNow.AddSeconds(8);
        while (DateTime.UtcNow < deadline && _chocksSet != placed)
            await Task.Delay(500);
    }

    public async Task SetGpu(bool connected)
    {
        if (!_efbLoaded) _ = PreloadEfb();
        if (_gpuConnected == connected) return;
        if (_lastGpuRequest == connected && DateTime.UtcNow - _lastGpuRequestTime < RequestCooldown) return;
        _lastGpuRequest = connected;
        _lastGpuRequestTime = DateTime.UtcNow;

        Logger.Debug($"FSLabsA320Adapter: Attempting GPU → {(connected ? "ON" : "OFF")}");

        if (!connected)
        {
            var efbCommands = new EfbCommand[]
                {
                new NavigateTo(FSLabsA320Constants.GroundServicesSelector),
                new ClickElement(FSLabsA320Constants.GpuSelector),
                new NavigateTo(FSLabsA320Constants.HomeButtonSelector),
                };

            await _efb.RunAsync(_efbUrl, efbCommands);
        }
        else
        {
            _ = _gsxMenu.FlashMenuAsync();
        }
    }
}
