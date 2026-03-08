namespace SimpleGsxIntegrator.Aircraft.Pmdg;

/// <summary>
/// PMDG 737 (NG3) event codes and L:var names.
/// Event offsets sourced from PMDG_NG3_SDK.h (THIRD_PARTY_EVENT_ID_MIN = 0x00011000).
/// </summary>
public static class Pmdg737Constants
{
    private const uint BASE = 0x00011000;

    public const uint EVT_DOOR_FWD_L = BASE + 14005;  // Forward Left Cabin  (1L)
    public const uint EVT_DOOR_FWD_R = BASE + 14006;  // Forward Right Cabin (1R)
    public const uint EVT_DOOR_AFT_L = BASE + 14007;  // Aft Left Cabin      (2L)
    public const uint EVT_DOOR_AFT_R = BASE + 14008;  // Aft Right Cabin     (2R)
    public const uint EVT_DOOR_OVERWING_EXIT_L = BASE + 14009;  // Overwing Fwd Left  Emergency Exit
    public const uint EVT_DOOR_OVERWING_EXIT_R = BASE + 14010;  // Overwing Fwd Right Emergency Exit
    // Aft overwing exits have no direct SDK event – closed via CDU sequence.
    public const uint EVT_DOOR_OVERWING_EXIT_L2 = BASE + 69696969; // Overwing Aft Left  (CDU only)
    public const uint EVT_DOOR_OVERWING_EXIT_R2 = BASE + 69696970; // Overwing Aft Right (CDU only)
    public const uint EVT_DOOR_CARGO_FWD = BASE + 14013;  // Forward Cargo
    public const uint EVT_DOOR_CARGO_AFT = BASE + 14014;  // Aft Cargo
    public const uint EVT_DOOR_CARGO_MAIN = BASE + 14015;  // Main Cargo
    public const uint EVT_DOOR_EQUIPMENT_HATCH = BASE + 14016;  // Equipment Hatch

    public const uint EVT_OH_ELEC_GRD_PWR_SWITCH = BASE + 17;

    public const uint EVT_CDU_R_L1 = BASE + 606;
    public const uint EVT_CDU_R_L2 = BASE + 607;
    public const uint EVT_CDU_R_L3 = BASE + 608;
    public const uint EVT_CDU_R_L4 = BASE + 609;
    public const uint EVT_CDU_R_L5 = BASE + 610;
    public const uint EVT_CDU_R_L6 = BASE + 611;
    public const uint EVT_CDU_R_R1 = BASE + 612;
    public const uint EVT_CDU_R_R2 = BASE + 613;
    public const uint EVT_CDU_R_R3 = BASE + 614;
    public const uint EVT_CDU_R_R4 = BASE + 615;
    public const uint EVT_CDU_R_R5 = BASE + 616;
    public const uint EVT_CDU_R_R6 = BASE + 617;
    public const uint EVT_CDU_R_MENU = BASE + 623;

    public const string LVAR_DOOR_FWD_L = "L:FwdLeftCabinDoor";
    public const string LVAR_DOOR_AFT_L = "L:AftLeftCabinDoor";
    public const string LVAR_DOOR_FWD_R = "L:FwdRightCabinDoor";
    public const string LVAR_DOOR_AFT_R = "L:AftRightCabinDoor";
    public const string LVAR_OVERWING_AFT_L = "L:OverwingAftLeftEmerExit";
    public const string LVAR_OVERWING_AFT_R = "L:OverwingAftRightEmerExit";
    public const string LVAR_OVERWING_FWD_L = "L:OverwingFwdLeftEmerExit";
    public const string LVAR_OVERWING_FWD_R = "L:OverwingFwdRightEmerExit";
    public const string LVAR_CARGO_FWD = "L:FwdLwrCargoDoor";
    public const string LVAR_CARGO_AFT = "L:AftLwrCargoDoor";
    public const string LVAR_CARGO_MAIN = "L:MainCargoDoor";
    public const string LVAR_EQUIPMENT_HATCH = "L:EEDoor";
    public const string LVAR_WHEEL_CHOCKS = "L:NGXWheelChocks";

    public const string CLIENT_DATA_CONTROL_NAME = "PMDG_NG3_Control";

    public static string GetDoorName(uint evtCode) => evtCode switch
    {
        EVT_DOOR_FWD_L => "Forward Left Cabin",
        EVT_DOOR_FWD_R => "Forward Right Cabin",
        EVT_DOOR_AFT_L => "Aft Left Cabin",
        EVT_DOOR_AFT_R => "Aft Right Cabin",
        EVT_DOOR_OVERWING_EXIT_L => "Overwing Fwd Left Emergency Exit",
        EVT_DOOR_OVERWING_EXIT_R => "Overwing Fwd Right Emergency Exit",
        EVT_DOOR_OVERWING_EXIT_L2 => "Overwing Aft Left Emergency Exit",
        EVT_DOOR_OVERWING_EXIT_R2 => "Overwing Aft Right Emergency Exit",
        EVT_DOOR_CARGO_FWD => "Forward Cargo",
        EVT_DOOR_CARGO_AFT => "Aft Cargo",
        EVT_DOOR_CARGO_MAIN => "Main Cargo",
        EVT_DOOR_EQUIPMENT_HATCH => "Equipment Hatch",
        _ => $"door_evt_{evtCode}",
    };

    public static readonly IReadOnlyList<uint> AllDoorIds =
    [
        EVT_DOOR_FWD_L,
        EVT_DOOR_AFT_L,
        EVT_DOOR_FWD_R,
        EVT_DOOR_AFT_R,
        EVT_DOOR_OVERWING_EXIT_L2,  // aft – CDU close
        EVT_DOOR_OVERWING_EXIT_R2,  // aft – CDU close
        EVT_DOOR_OVERWING_EXIT_L,
        EVT_DOOR_OVERWING_EXIT_R,
        EVT_DOOR_CARGO_FWD,
        EVT_DOOR_CARGO_AFT,
        EVT_DOOR_CARGO_MAIN,
        EVT_DOOR_EQUIPMENT_HATCH,
    ];
}
