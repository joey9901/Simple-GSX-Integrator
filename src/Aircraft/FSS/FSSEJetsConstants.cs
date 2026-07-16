using System.Runtime.InteropServices;

namespace SimpleGsxIntegrator.Aircraft.FSS;

internal static class FSSEJetsConstants
{
    public const string LVar_ToggleGpu = "L:FSS_EXX_TOGGLE_CGPU";
    public const string LVar_GpuState = "L:FSS_EXX_EXT_GPU_STATE";
    public const string LVar_ChockF = "L:FSS_EXX_GNDOBJ_WHEEL_CHOKE_F";
    public const string LVar_ChockL = "L:FSS_EXX_GNDOBJ_WHEEL_CHOKE_L";
    public const string LVar_ChockR = "L:FSS_EXX_GNDOBJ_WHEEL_CHOKE_R";
}

internal static class FSSGpuState
{
    public const double ObjectHidden = -1;
    public const double Inactive = 0;
    public const double Requested = 1;
    public const double Available = 2;
    public const double Startup = 3;
    public const double Stopping = 4;
    public const double Running = 5;

    public static bool IsOn(double s) => s >= Available;
    public static bool IsBusy(double s) => s is Requested or Startup or Stopping;
}

[StructLayout(LayoutKind.Sequential, Pack = 1)]
internal struct FSSGroundStateStruct
{
    public double GpuState;
    public double ChockF;
    public double ChockL;
    public double ChockR;
}
