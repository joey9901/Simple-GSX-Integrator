namespace SimpleGsxIntegrator.Aircraft.Pmdg;

/// <summary>
/// PMDG 777 event codes and door-related constants.
/// Event codes are built from the THIRD_PARTY_EVENT_ID_MIN base (0x00011000)
/// plus the offsets defined in PMDG_777X_SDK.h.
/// </summary>
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
    public const uint EVT_DOOR_CARGO_BULK = BASE + 14023;   // Bulk cargo
    public const uint EVT_DOOR_CARGO_MAIN = BASE + 14024;   // Main deck cargo (freighter)
    public const uint EVT_DOOR_AVIONICS = BASE + 14025;   // Avionics / forward access
    public const uint EVT_DOOR_EE_HATCH = BASE + 14026;   // E&E hatch

    public const uint EVT_OH_ELEC_GRD_PWR_PRIM = BASE + 8;    // Primary ground power switch
    public const uint EVT_OH_ELEC_GRD_PWR_SEC = BASE + 7;    // Secondary ground power switch

    public const uint EVT_CDU_R_R1 = BASE + 407;
    public const uint EVT_CDU_R_R6 = BASE + 412;
    public const uint EVT_CDU_R_L2 = BASE + 402;
    public const uint EVT_CDU_R_MENU = BASE + 423;

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

    public static string GetDoorName(uint evtCode) => evtCode switch
    {
        EVT_DOOR_1L => "Cabin 1L",
        EVT_DOOR_1R => "Cabin 1R",
        EVT_DOOR_2L => "Cabin 2L",
        EVT_DOOR_2R => "Cabin 2R",
        EVT_DOOR_3L => "Cabin 3L",
        EVT_DOOR_3R => "Cabin 3R",
        EVT_DOOR_4L => "Cabin 4L",
        EVT_DOOR_4R => "Cabin 4R",
        EVT_DOOR_5L => "Cabin 5L",
        EVT_DOOR_5R => "Cabin 5R",
        EVT_DOOR_CARGO_FWD => "Fwd Cargo",
        EVT_DOOR_CARGO_AFT => "Aft Cargo",
        EVT_DOOR_CARGO_MAIN => "Main Cargo",
        EVT_DOOR_CARGO_BULK => "Bulk Cargo",
        EVT_DOOR_AVIONICS => "Avionics Access",
        EVT_DOOR_EE_HATCH => "E&E Hatch",
        _ => $"door_evt_{evtCode}",
    };


    /// <summary>All door event codes paired with their L:var names (for registration order).</summary>
    public static readonly IReadOnlyList<(uint EvtCode, string LVar)> AllDoors =
        new (uint, string)[]
        {
            (EVT_DOOR_1L,         LVAR_DOOR_1L),
            (EVT_DOOR_1R,         LVAR_DOOR_1R),
            (EVT_DOOR_2L,         LVAR_DOOR_2L),
            (EVT_DOOR_2R,         LVAR_DOOR_2R),
            (EVT_DOOR_3L,         LVAR_DOOR_3L),
            (EVT_DOOR_3R,         LVAR_DOOR_3R),
            (EVT_DOOR_4L,         LVAR_DOOR_4L),
            (EVT_DOOR_4R,         LVAR_DOOR_4R),
            (EVT_DOOR_5L,         LVAR_DOOR_5L),
            (EVT_DOOR_5R,         LVAR_DOOR_5R),
            (EVT_DOOR_CARGO_FWD,  LVAR_CARGO_FWD),
            (EVT_DOOR_CARGO_AFT,  LVAR_CARGO_AFT),
            (EVT_DOOR_CARGO_MAIN, LVAR_CARGO_MAIN),
            (EVT_DOOR_CARGO_BULK, LVAR_CARGO_BULK),
            (EVT_DOOR_AVIONICS,   LVAR_AVIONICS),
            (EVT_DOOR_EE_HATCH,   LVAR_EE_HATCH),
        };

    /// <summary>Flat list of door event codes, used with <see cref="DoorStateTracker"/>.</summary>
    public static readonly IReadOnlyList<uint> AllDoorIds = new uint[]
    {
        EVT_DOOR_1L,   EVT_DOOR_1R,   EVT_DOOR_2L,  EVT_DOOR_2R,
        EVT_DOOR_3L,   EVT_DOOR_3R,   EVT_DOOR_4L,  EVT_DOOR_4R,
        EVT_DOOR_5L,   EVT_DOOR_5R,
        EVT_DOOR_CARGO_FWD, EVT_DOOR_CARGO_AFT, EVT_DOOR_CARGO_MAIN, EVT_DOOR_CARGO_BULK,
        EVT_DOOR_AVIONICS,  EVT_DOOR_EE_HATCH,
    };

    public const string CLIENT_DATA_CONTROL_NAME = "PMDG_777X_Control";
}
