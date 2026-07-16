namespace SimpleGsxIntegrator.Aircraft.iFly;

internal sealed class IFly737Adapter : AircraftAdapterBase
{
    public override string DisplayName => "iFly 737Max";
    public override string parkingBrakeVariable => "L:VC_Parking_Brake_SW_VAL";
}
