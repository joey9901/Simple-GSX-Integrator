namespace SimpleGsxIntegrator.Gsx;

public static class GsxConstants
{
    public const string CouatlStarted = "L:FSDT_GSX_STATE";

    public const string MenuOpen = "L:FSDT_GSX_MENU_OPEN";
    public const string MenuChoice = "L:FSDT_GSX_MENU_CHOICE";

    public const string BoardingState = "L:FSDT_GSX_BOARDING_STATE";
    public const string DeboardingState = "L:FSDT_GSX_DEBOARDING_STATE";
    public const string PushbackState = "L:FSDT_GSX_DEPARTURE_STATE";
    public const string RefuelingState = "L:FSDT_GSX_REFUELING_STATE";
    public const string CateringState = "L:FSDT_GSX_CATERING_STATE";

    public const string NumPassengers = "L:FSDT_GSX_NUMPASSENGERS";
    public const string BoardingPaxTotal = "L:FSDT_GSX_NUMPASSENGERS_BOARDING_TOTAL";
    public const string DeboardingPaxTotal = "L:FSDT_GSX_NUMPASSENGERS_DEBOARDING_TOTAL";
    public const string BoardingCargoPercent = "L:FSDT_GSX_BOARDING_CARGO_PERCENT";
    public const string DeboardingCargoPercent = "L:FSDT_GSX_DEBOARDING_CARGO_PERCENT";
}
