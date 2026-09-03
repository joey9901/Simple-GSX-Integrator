using System.Runtime.InteropServices;

namespace SimpleGsxIntegrator.Aircraft.Synaptic;

internal static class A220Constants
{
    public const string LVar_Chocks = "L:INI_CHOCKS_ENABLED";
    public const string LVar_Gpu = "L:INI_GPU_AVAIL";
}

[StructLayout(LayoutKind.Sequential, Pack = 1)]
internal struct A220GroundStateStruct
{
    public double Chocks;
    public double Gpu;
}