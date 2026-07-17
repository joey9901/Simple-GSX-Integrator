namespace SimpleGsxIntegrator.Core;

internal enum SimDef : uint
{
    FlightState = 100,
    ActivationLvar = 101,

    GsxState = 200,
    GsxMenuOpen = 201,
    GsxMenuChoice = 202,
    GsxRemoteControl = 203,  // L:FSDT_GSX_SET_REMOTECONTROL (write)

    Pmdg777Vars = 300,
    Pmdg777Control = 301,

    Pmdg737Vars = 400,
    Pmdg737Control = 401,

    A330Chocks = 500,       // A:COVER ON:0 — wheel chocks (write)
    A330Gpu = 501,          // L:INI_GPU_AVAIL — GPU (write)
    A330EngineCover = 502,  // A:COVER ON:1 — engine covers (write)
    A330PitotCover = 503,   // A:COVER ON:2 — pitot covers (write)
    A330GroundState = 504,  // combined read: chocks + gpu

    A300Chocks = 510,       // L:INI_CHOCKS_ENABLED (write)
    A300Gpu = 511,          // L:INI_gpu_avail (write)
    A300Covers = 512,       // L:INI_COVERS_ENABLED (write)
    A300CargoDoor = 513,    // L:INI_MAIN_CARGO_DOOR_TGT (write)
    A300GroundState = 514,  // combined read: chocks + gpu + cargo door

    Md11Chocks = 520,       // L:MD11_EXT_CHOCKS (write)
    Md11Gpu = 521,       // L:MD11_EXT_GPU (write)
    Md11GroundState = 522,  // combined read: chocks + gpu

    FSSEJetsToggleGpu = 600,  // L:FSS_EXX_TOGGLE_CGPU (write, rising-edge)
    FSSEJetsChockF = 601,  // L:FSS_EXX_GNDOBJ_WHEEL_CHOKE_F (write)
    FSSEJetsChockL = 602,  // L:FSS_EXX_GNDOBJ_WHEEL_CHOKE_L (write)
    FSSEJetsChockR = 603,  // L:FSS_EXX_GNDOBJ_WHEEL_CHOKE_R (write)
    FSSEJetsGroundState = 604,  // combined read: GPU + 3 chocks

    IFly737GroundState = 700,  // combined read: 3 chocks + GPU (write happens via EFB automation, not L:vars)
}

internal enum SimReq : uint
{
    FlightState = 100,
    ActivationLvar = 101,

    GsxState = 200,

    AircraftLoaded = 900,

    Pmdg777Vars = 300,

    Pmdg737Vars = 400,

    A330GroundState = 500,
    A300GroundState = 510,
    Md11GroundState = 520,

    FSSEJetsGroundState = 600,

    IFly737GroundState = 700,
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
