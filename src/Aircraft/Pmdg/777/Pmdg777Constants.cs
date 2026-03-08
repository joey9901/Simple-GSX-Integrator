namespace SimpleGsxIntegrator.Aircraft.Pmdg;

public static class Pmdg777Constants
{
    private const uint BASE = 0x00011000;

    public const uint EVT_DOOR_1L = BASE + 14011;   // Cabin door 1 Left (main boarding)
    public const uint EVT_DOOR_1R = BASE + 14012;   // Cabin door 1 Right
    public const uint EVT_DOOR_2L = BASE + 14013;
    public const uint EVT_DOOR_2R = BASE + 14014;
    public const uint EVT_DOOR_3L = BASE + 14015;
    public const uint EVT_DOOR_3R = BASE + 14016;
    public const uint EVT_DOOR_4L = BASE + 14017;
    public const uint EVT_DOOR_4R = BASE + 14018;
    public const uint EVT_DOOR_5L = BASE + 14019;
    public const uint EVT_DOOR_5R = BASE + 14020;
    public const uint EVT_DOOR_CARGO_FWD = BASE + 14021;   // Forward lower cargo
    public const uint EVT_DOOR_CARGO_AFT = BASE + 14022;   // Aft lower cargo
    public const uint EVT_DOOR_CARGO_MAIN = BASE + 14023;   // Main deck cargo (freighter)
    public const uint EVT_DOOR_CARGO_BULK = BASE + 14024;   // Bulk cargo
    public const uint EVT_DOOR_AVIONICS = BASE + 14025;   // Avionics / forward access
    public const uint EVT_DOOR_EE_HATCH = BASE + 14026;   // E&E hatch

    public const uint EVT_OH_ELEC_GRD_PWR_PRIM = BASE + 8;    // Primary ground power switch
    public const uint EVT_OH_ELEC_GRD_PWR_SEC = BASE + 7;    // Secondary ground power switch

    // Right CDU (kept for reference)
    public const uint EVT_CDU_R_R1 = BASE + 407;
    public const uint EVT_CDU_R_R6 = BASE + 412;
    public const uint EVT_CDU_R_L2 = BASE + 402;
    public const uint EVT_CDU_R_MENU = BASE + 423;

    // Center (3rd / observer) CDU  –  offset = 653 - 328 = 325 over left CDU base
    public const uint EVT_CDU_C_R1 = BASE + 659;   // CDU_EVT_OFFSET_C + EVT_CDU_L_R1 (325+334)
    public const uint EVT_CDU_C_R6 = BASE + 664;   // CDU_EVT_OFFSET_C + EVT_CDU_L_R6 (325+339)
    public const uint EVT_CDU_C_L2 = BASE + 654;   // CDU_EVT_OFFSET_C + EVT_CDU_L_L2 (325+329)
    public const uint EVT_CDU_C_MENU = BASE + 675;   // CDU_EVT_OFFSET_C + EVT_CDU_L_MENU (325+350)

    public const string LVAR_DOOR_1L = "L:7X7XCabinDoor1L";
    public const string LVAR_DOOR_1R = "L:7X7XCabinDoor1R";
    public const string LVAR_DOOR_2L = "L:7X7XCabinDoor2L";
    public const string LVAR_DOOR_2R = "L:7X7XCabinDoor2R";
    public const string LVAR_DOOR_3L = "L:7X7XCabinDoor3L";
    public const string LVAR_DOOR_3R = "L:7X7XCabinDoor3R";
    public const string LVAR_DOOR_4L = "L:7X7XCabinDoor4L";
    public const string LVAR_DOOR_4R = "L:7X7XCabinDoor4R";
    public const string LVAR_DOOR_5L = "L:7X7XCabinDoor5L";
    public const string LVAR_DOOR_5R = "L:7X7XCabinDoor5R";
    public const string LVAR_CARGO_FWD = "L:7X7XforwardcargoDoor";
    public const string LVAR_CARGO_AFT = "L:7X7XaftcargoDoor";
    public const string LVAR_CARGO_MAIN = "L:7X7XmaincargoDoor";
    public const string LVAR_CARGO_BULK = "L:7X7XbulkcargoDoor";
    public const string LVAR_AVIONICS = "L:7X7XavionicsDoor";
    public const string LVAR_EE_HATCH = "L:7X7XEEDoor";

    public const string LVAR_WHEEL_CHOCKS = "L:7X7X_WheelChocks";
    public const string LVAR_EXT_PWR_SEC = "L:switch_07_b";    // secondary GPU
    public const string LVAR_EXT_PWR_PRIM = "L:switch_08_b";    // primary GPU

    public static string GetDoorName(uint evtCode)
    {
        switch (evtCode)
        {
            case EVT_DOOR_1L: return "Cabin 1L";
            case EVT_DOOR_1R: return "Cabin 1R";
            case EVT_DOOR_2L: return "Cabin 2L";
            case EVT_DOOR_2R: return "Cabin 2R";
            case EVT_DOOR_3L: return "Cabin 3L";
            case EVT_DOOR_3R: return "Cabin 3R";
            case EVT_DOOR_4L: return "Cabin 4L";
            case EVT_DOOR_4R: return "Cabin 4R";
            case EVT_DOOR_5L: return "Cabin 5L";
            case EVT_DOOR_5R: return "Cabin 5R";
            case EVT_DOOR_CARGO_FWD: return "Fwd Cargo";
            case EVT_DOOR_CARGO_AFT: return "Aft Cargo";
            case EVT_DOOR_CARGO_MAIN: return "Main Cargo";
            case EVT_DOOR_CARGO_BULK: return "Bulk Cargo";
            case EVT_DOOR_AVIONICS: return "Avionics Access";
            case EVT_DOOR_EE_HATCH: return "E&E Hatch";
            default: return $"door_evt_{evtCode}";
        }
    }


    public static readonly IReadOnlyList<uint> AllDoorIds =
    [
        EVT_DOOR_1L,   EVT_DOOR_1R,   EVT_DOOR_2L,  EVT_DOOR_2R,
        EVT_DOOR_3L,   EVT_DOOR_3R,   EVT_DOOR_4L,  EVT_DOOR_4R,
        EVT_DOOR_5L,   EVT_DOOR_5R,
        EVT_DOOR_CARGO_FWD, EVT_DOOR_CARGO_AFT, EVT_DOOR_CARGO_MAIN, EVT_DOOR_CARGO_BULK,
        EVT_DOOR_AVIONICS,  EVT_DOOR_EE_HATCH,
    ];

    public const string CLIENT_DATA_CONTROL_NAME = "PMDG_777X_Control";
}
