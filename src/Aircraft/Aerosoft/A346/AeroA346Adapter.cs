namespace SimpleGsxIntegrator.Aircraft.Aerosoft;

internal sealed class AeroA346Adapter : AircraftAdapterBase
{
    public override string DisplayName => "Aerosoft/Toliss A346";
    public override string parkingBrakeVariable => "L:ParkingBrake_Position";
    public override string beaconLightVariable => "L:AB_VC_OVH_EXTLIGHT_BEACON_SW";
}
