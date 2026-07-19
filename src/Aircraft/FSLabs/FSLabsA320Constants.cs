using System.Runtime.InteropServices;

namespace SimpleGsxIntegrator.Aircraft.FSLabs;

internal static class FSLabsA320Constants
{
    public const string LVar_ParkingBrake = "L:FSLA320_ParkBrake";
    public const string LVar_Chocks = "L:FSLA320_Wheel_Chocks";
    public const string LVar_Gpu = "L:GPU_Panel_pwr";

    public const string EfbUrl = "http://localhost:23032";
    public const string HomeButtonSelector = """button[onclick="EFB_ShowPage('MENU')"]""";

    public const string ZeroKey = """button[onclick="addFilledDot()"]""";

    public const string GroundServicesSelector = """button[onclick="EFB_ShowPage('CONNECTIONS')"]""";
    public const string ChocksSelector = "#CHOCKS";
    public const string GpuSelector = "#GROUND-BUTTON-GPU";
}

[StructLayout(LayoutKind.Sequential, Pack = 1)]
internal struct FSLabsA320VarsStruct
{
    public double Chocks;
    public double Gpu;
}
