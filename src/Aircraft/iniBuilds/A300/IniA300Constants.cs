using System.Runtime.InteropServices;

namespace SimpleGsxIntegrator.Aircraft.A300;

internal static class A300Constants
{
    public const string LVar_Chocks = "L:INI_CHOCKS_ENABLED";
    public const string LVar_Covers = "L:INI_COVERS_ENABLED";
    public const string LVar_Gpu = "L:INI_gpu_avail";
    public const string LVar_CargoDoor = "L:INI_MAIN_CARGO_DOOR_TGT";

    public const double CargoDoorOpen = 100.0;
    public const double CargoDoorClosed = 0.0;
}

[StructLayout(LayoutKind.Sequential, Pack = 1)]
internal struct A300GroundStateStruct
{
    public double Chocks;
    public double Gpu;
    public double CargoDoor;
}
