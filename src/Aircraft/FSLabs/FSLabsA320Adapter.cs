namespace SimpleGsxIntegrator.Aircraft.FSLabs;

internal sealed class FSLabsA320Adapter : AircraftAdapterBase
{
    public override string DisplayName => "FSLabs A32NX";
    public override string parkingBrakeVariable => "L:FSLA320_ParkBrake";
}
