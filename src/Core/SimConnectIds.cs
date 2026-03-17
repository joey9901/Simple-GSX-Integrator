namespace SimpleGsxIntegrator.Core;

internal enum SimDef : uint
{
    FlightState = 100,
    ActivationLvar = 101,

    GsxState = 200,
    GsxMenuOpen = 201,
    GsxMenuChoice = 202,

    Pmdg777Vars = 300,
    Pmdg777Control = 301,

    Pmdg737Vars = 400,
    Pmdg737Control = 401,

    A330Chocks = 500,       // A:COVER ON:0 — wheel chocks
    A330Gpu = 501,          // L:INI_GPU_AVAIL — GPU
    A330EngineCover = 502,  // A:COVER ON:1 — engine covers
    A330PitotCover = 503,   // A:COVER ON:2 — pitot covers

    A300Chocks = 510,    // L:INI_CHOCKS_ENABLED — wheel chocks
    A300Gpu = 511,    // L:INI_gpu_avail — GPU
    A300Covers = 512,    // L:INI_COVERS_ENABLED — engine + pitot covers
    A300CargoDoor = 513,    // L:INI_MAIN_CARGO_DOOR_TGT — 100 = open, 0 = closed
}

internal enum SimReq : uint
{
    FlightState = 100,
    ActivationLvar = 101,

    GsxState = 200,

    AircraftLoaded = 900,

    Pmdg777Vars = 300,

    Pmdg737Vars = 400,

}

internal enum Pmdg777DataId : uint
{
    Data = 0x504D4447,   // PMDG_777X_DATA_ID
    Control = 0x504D4449,   // PMDG_777X_CONTROL_ID
}

internal enum Pmdg737DataId : uint
{
    Control = 0x4E473300,   // PMDG_NG3_CONTROL_ID
}
