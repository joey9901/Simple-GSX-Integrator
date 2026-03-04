namespace SimpleGsxIntegrator.Gsx;

/// <summary>All GSX-related SimConnect L:var and event names in one place.</summary>
public static class GsxConstants
{
    // -----------------------------------------------------------------
    //  GSX engine detection
    // -----------------------------------------------------------------
    public const string CouatlStarted = "L:FSDT_GSX_COUATL_STARTED";

    // -----------------------------------------------------------------
    //  Menu control
    // -----------------------------------------------------------------
    public const string MenuOpen = "L:FSDT_GSX_MENU_OPEN";
    public const string MenuChoice = "L:FSDT_GSX_MENU_CHOICE";

    // -----------------------------------------------------------------
    //  Service state L:vars  (cast value to GsxServiceState)
    // -----------------------------------------------------------------
    public const string BoardingState = "L:FSDT_GSX_BOARDING_STATE";
    public const string DeboardingState = "L:FSDT_GSX_DEBOARDING_STATE";
    public const string DepartureState = "L:FSDT_GSX_DEPARTURE_STATE";   // pushback prep/done
    public const string RefuelingState = "L:FSDT_GSX_REFUELING_STATE";
    public const string CateringState = "L:FSDT_GSX_CATERING_STATE";

    // -----------------------------------------------------------------
    //  Pushback progress (separate from state)
    //  0 = idle, 1-4 = in progress, 5 = complete
    // -----------------------------------------------------------------
    public const string PushbackStatus = "L:FSDT_GSX_PUSHBACK_STATUS";

    // -----------------------------------------------------------------
    //  Passenger / cargo counters (useful for future enhancements)
    // -----------------------------------------------------------------
    public const string NumPassengers = "L:FSDT_GSX_NUMPASSENGERS";
    public const string BoardingPaxTotal = "L:FSDT_GSX_NUMPASSENGERS_BOARDING_TOTAL";
    public const string DeboardingPaxTotal = "L:FSDT_GSX_NUMPASSENGERS_DEBOARDING_TOTAL";
    public const string BoardingCargoPercent = "L:FSDT_GSX_BOARDING_CARGO_PERCENT";
    public const string DeboardingCargoPercent = "L:FSDT_GSX_DEBOARDING_CARGO_PERCENT";
}
