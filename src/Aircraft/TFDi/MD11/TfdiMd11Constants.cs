using System.Runtime.InteropServices;

namespace SimpleGsxIntegrator.Aircraft.TFDi;

internal static class TfdiMd11Constants
{
    public const string LVar_ParkingBrake = "L:MD11_THR_PARK_LT";
    public const string LVar_Chocks = "L:MD11_EXT_CHOCKS";
    public const string LVar_Gpu = "L:MD11_EXT_GPU";
}

[StructLayout(LayoutKind.Sequential, Pack = 1)]
internal struct Md11GroundStateStruct
{
    public double Chocks;
    public double Gpu;
}
