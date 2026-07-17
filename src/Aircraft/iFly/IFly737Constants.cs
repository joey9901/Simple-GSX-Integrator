using System.Runtime.InteropServices;

namespace SimpleGsxIntegrator.Aircraft.iFly;

internal static class IFly737Constants
{
    public const string LVar_ParkingBrake = "L:VC_Parking_Brake_SW_VAL";
    public const string LVar_NoseChock = "L:iFly_NLG_Chock_Display_VAL";
    public const string LVar_LeftChock = "L:iFly_L_MLG_Chock_Display_VAL";
    public const string LVar_RightChock = "L:iFly_R_MLG_Chock_Display_VAL";
    public const string LVar_Gpu = "L:Animation_GND_ELEC_VEHICLE_Display_VAL";

    public const string EfbUrl = "http://localhost:8084";

    public const string GroundServicesSelector = "div[data-module='groundservices']";
    public const string HomeButtonSelector = "#homeButton";
    public const string NoseWheelSelector = "#nose_wheel";
    public const string MainLeftWheelSelector = "#main_left_wheel";
    public const string MainRightWheelSelector = "#main_right_wheel";
    public const string GpuSelector = "#E1";
}

[StructLayout(LayoutKind.Sequential, Pack = 1)]
internal struct IFly737GroundStateStruct
{
    public double NoseChock;
    public double LeftChock;
    public double RightChock;
    public double Gpu;
}
