using Microsoft.FlightSimulator.SimConnect;
using SimpleGsxIntegrator.Core;
using System.Runtime.InteropServices;

namespace SimpleGsxIntegrator.Aircraft.TFDi;

internal sealed class Md11Adapter : AircraftAdapterBase
{
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    private struct ScalarStruct { public double Value; }

    private SimConnect? _sc;

    public override string parkingBrakeVariable => Md11Constants.LVar_ParkingBrake;
    public override bool canRemoveAndPlaceGroundEquipment => true;

    public override void OnSimConnectConnected(SimConnect sc)
    {
        _sc = sc;

        sc.AddToDataDefinition(SimDef.Md11Chocks,
            Md11Constants.LVar_Chocks, "Number", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
        sc.RegisterDataDefineStruct<ScalarStruct>(SimDef.Md11Chocks);

        sc.AddToDataDefinition(SimDef.Md11Gpu,
            Md11Constants.LVar_Gpu, "Number", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
        sc.RegisterDataDefineStruct<ScalarStruct>(SimDef.Md11Gpu);
    }

    public override Task OnBeforeDeboarding()
    {
        Logger.Info("Md11Adapter: Placing chocks and GPU");
        WriteSimVar(SimDef.Md11Chocks, 1.0);
        WriteSimVar(SimDef.Md11Gpu, 1.0);
        return Task.CompletedTask;
    }

    public override Task OnBeforePushback()
    {
        Logger.Info("Md11Adapter: Removing chocks and GPU");
        WriteSimVar(SimDef.Md11Gpu, 0.0);
        WriteSimVar(SimDef.Md11Chocks, 0.0);
        return Task.CompletedTask;
    }

    public override void Dispose()
    {
        _sc = null;
        Logger.Debug("Md11Adapter: disposed");
    }

    private void WriteSimVar(SimDef def, double value)
    {
        if (_sc == null) return;
        try
        {
            _sc.SetDataOnSimObject(def, SimConnect.SIMCONNECT_OBJECT_ID_USER,
                SIMCONNECT_DATA_SET_FLAG.DEFAULT, new ScalarStruct { Value = value });
        }
        catch (Exception ex)
        {
            Logger.Warning($"Md11Adapter: WriteSimVar({def}) = {value} failed: {ex.Message}");
        }
    }
}
