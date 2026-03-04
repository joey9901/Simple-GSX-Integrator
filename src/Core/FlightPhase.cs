namespace SimpleGsxIntegrator.Core;

/// <summary>
/// Represents the logical phase of the current flight session.
/// The <see cref="FlightStateTracker"/> transitions between these phases based on
/// observed SimConnect state variables and GSX service completion events.
/// </summary>
public enum FlightPhase
{
    /// <summary>Phase not yet determined (startup).</summary>
    Unknown,

    /// <summary>On the ground, parked, engines off, beacon off – ready for ground services.</summary>
    AtGate,

    /// <summary>Ground services in progress (refueling / catering / boarding).</summary>
    GroundServicing,

    /// <summary>Boarding complete, beacon turned ON – close doors, remove equipment, call pushback.</summary>
    PrePushback,

    /// <summary>Pushback in progress or requested.</summary>
    Pushback,

    /// <summary>Engines running, aircraft taxiing to runway.</summary>
    TaxiOut,

    /// <summary>Aircraft is airborne.</summary>
    Airborne,

    /// <summary>Aircraft back on ground, taxiing to gate after a flight.</summary>
    TaxiIn,

    /// <summary>Parked at gate after flight, engines shutting down – ready for deboarding.</summary>
    Arrived,

    /// <summary>Deboarding in progress.</summary>
    Deboarding,

    /// <summary>Deboarding complete; waiting for the configured turnaround delay.</summary>
    TurnaroundDelay,

    /// <summary>Turnaround: ready for next departure ground services.</summary>
    Turnaround,
}
