using System.Runtime.InteropServices;

namespace SimpleGsxIntegrator.Aircraft.A330;

internal static class A330Constants
{
    public const string AVar_EngineCover = "COVER ON:1";
    public const string AVar_PitotCover = "COVER ON:2";
    public const string AVar_Chocks = "COVER ON:0";
    public const string LVar_Gpu = "L:INI_GPU_AVAIL";
}

[StructLayout(LayoutKind.Sequential, Pack = 1)]
internal struct A330GroundStateStruct
{
    public double Chocks;
    public double Gpu;
}
