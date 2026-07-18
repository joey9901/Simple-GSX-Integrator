using System.Runtime.InteropServices;

namespace SimpleGsxIntegrator.Aircraft.iFly;

internal static class IFly737Constants
{
    public const string LVar_ParkingBrake = "L:VC_Parking_Brake_SW_VAL";
    public const string LVar_NoseChock = "L:iFly_NLG_Chock_Display_VAL";
    public const string LVar_LeftChock = "L:iFly_L_MLG_Chock_Display_VAL";
    public const string LVar_RightChock = "L:iFly_R_MLG_Chock_Display_VAL";
    public const string LVar_Gpu = "L:Animation_GND_ELEC_VEHICLE_Display_VAL";

    public const string LVar_Aft_Cargo = "L:Animation_AFT_Cargo_VAL";
    public const string LVar_Fwd_Cargo = "L:Animation_FWD_Cargo_VAL";
    public const string LVar_R_Mid_Exit = "L:Animation_R_MID_Exit_VAL";
    public const string LVar_L_Mid_Exit = "L:Animation_L_MID_Exit_VAL";
    public const string LVar_L_Fwd_OverWing_Exit = "L:Animation_L_FWD_OverWing_VAL";
    public const string LVar_R_Fwd_OverWing_Exit = "L:Animation_R_FWD_OverWing_VAL";
    public const string LVar_L_Aft_OverWing_Exit = "L:Animation_L_AFT_OverWing_VAL";
    public const string LVar_R_Aft_OverWing_Exit = "L:Animation_R_AFT_OverWing_VAL";
    public const string LVar_Fwd_Entry = "L:Animation_FWD_Entry_VAL";
    public const string LVar_Aft_Entry = "L:Animation_AFT_Entry_VAL";
    public const string LVar_Fwd_Service = "L:Animation_FWD_Service_VAL";
    public const string LVar_Aft_Service = "L:Animation_AFT_Service_VAL";


    public const string EfbUrl = "http://localhost:8084";
    public const string HomeButtonSelector = "#homeButton";

    public const string GroundServicesSelector = "div[data-module='groundservices']";
    public const string NoseWheelSelector = "#nose_wheel";
    public const string MainLeftWheelSelector = "#main_left_wheel";
    public const string MainRightWheelSelector = "#main_right_wheel";
    public const string GpuSelector = "#E1";

    public const string DoorsSelector = "div[data-module='doors']";
    public const string CloseAllDoorsSelector = "#close-all-doors-btn";
    public const string ArmAllDoorsSelector = "#arm-all-doors-btn";
}

[StructLayout(LayoutKind.Sequential, Pack = 1)]
internal struct IFly737VarsStruct
{
    public double NoseChock;
    public double LeftChock;
    public double RightChock;
    public double Gpu;
    public double AftCargoDoor;
    public double FwdCargoDoor;
    public double RMidExitDoor;
    public double LMidExitDoor;
    public double LFwdOverWingDoor;
    public double RFwdOverWingDoor;
    public double LAftOverWingDoor;
    public double RAftOverWingDoor;
    public double FwdEntryDoor;
    public double AftEntryDoor;
    public double FwdServiceDoor;
    public double AftServiceDoor;
}
