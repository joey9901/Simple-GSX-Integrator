namespace SimpleGsxIntegrator.Aircraft.A330;

internal static class A330Constants
{
    public const string IE_DOOR_1L = "UNKNOWN_DOOR1L";
    public const string IE_DOOR_1R = "UNKNOWN_DOOR1R";
    public const string IE_DOOR_2L = "UNKNOWN_DOOR2L";
    public const string IE_DOOR_2R = "UNKNOWN_DOOR2R";
    public const string IE_DOOR_3L = "UNKNOWN_DOOR3L";   // Cabin 3L — exit 7 (confirmed)
    public const string IE_DOOR_3R = "UNKNOWN_DOOR3R";   // Cabin 3R — exit 8 (confirmed)

    /// <summary>Cargo hold doors. FWD = exit 6, AFT = exit 9 (both confirmed).</summary>
    public const string IE_CARGO_FWD = "UNKNOWN_CARGO_FWD";   // exit 6
    public const string IE_CARGO_AFT = "UNKNOWN_CARGO_AFT";   // exit 9

    public const string AVar_Chocks = "COVER ON:0";
    public const string LVar_Gpu = "L:INI_GPU_AVAIL";

    // ── Internal door ID constants (not event codes, just stable identifiers) ──

    public const uint DoorId1L = 1;
    public const uint DoorId1R = 2;
    public const uint DoorId2L = 3;
    public const uint DoorId2R = 4;
    public const uint DoorIdCargoFwd = 6;   // forward belly cargo hold (exit 6, confirmed)
    public const uint DoorId3L = 7;   // Cabin 3L (exit 7, confirmed)
    public const uint DoorId3R = 8;   // Cabin 3R (exit 8, confirmed)
    public const uint DoorIdCargoAft = 9;   // aft belly cargo hold (exit 9, confirmed)

    /// <summary>
    /// Maps each internal door ID to the corresponding Input Event name.
    /// Used by the adapter to route CloseDoor(uint) calls to the right hash.
    /// </summary>
    public static readonly IReadOnlyList<(uint Id, string Event)> DoorMap =
    [
        (DoorId1L,       IE_DOOR_1L),
        (DoorId1R,       IE_DOOR_1R),
        (DoorId2L,       IE_DOOR_2L),
        (DoorId2R,       IE_DOOR_2R),
        (DoorIdCargoFwd, IE_CARGO_FWD),
        (DoorId3L,       IE_DOOR_3L),
        (DoorId3R,       IE_DOOR_3R),
        (DoorIdCargoAft, IE_CARGO_AFT),
    ];

    public static readonly IReadOnlyList<uint> AllDoorIds =
        [DoorId1L, DoorId1R, DoorId2L, DoorId2R, DoorIdCargoFwd, DoorId3L, DoorId3R, DoorIdCargoAft];
}
