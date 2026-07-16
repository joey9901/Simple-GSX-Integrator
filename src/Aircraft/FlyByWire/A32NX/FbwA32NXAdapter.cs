namespace SimpleGsxIntegrator.Aircraft.FlyByWire;

internal sealed class FbwA32NXAdapter : AircraftAdapterBase
{
    public override string DisplayName => "FlyByWire A32NX";
    public override string parkingBrakeVariable => "L:A32NX_PARK_BRAKE_LEVER_POS";
    public override string beaconLightVariable => "L:LIGHTING_BEACON_0";
}
