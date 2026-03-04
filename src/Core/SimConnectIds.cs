namespace SimpleGsxIntegrator.Core;

/// <summary>
/// Central registry of all SimConnect definition and request IDs used across the application.
/// Each component owns a unique range to avoid collisions.
/// </summary>
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

/// <summary>PMDG 777 client-data area IDs (from PMDG_777X_SDK.h).</summary>
internal enum Pmdg777DataId : uint
{
    Data = 0x504D4447,   // PMDG_777X_DATA_ID
    Control = 0x504D4449,   // PMDG_777X_CONTROL_ID
}

/// <summary>PMDG 737 NG3 client-data area IDs (from PMDG_NG3_SDK.h).</summary>
internal enum Pmdg737DataId : uint
{
    Control = 0x4E473300,   // PMDG_NG3_CONTROL_ID
}
