namespace SimpleGsxIntegrator.Config;


public sealed class AppConfig
{
    public HotkeyConfig Hotkeys { get; set; } = new();
    public UiConfig UI { get; set; } = new();
    public Dictionary<string, AircraftConfig> Aircraft { get; set; } = new();
}


public sealed class HotkeyConfig
{
    public string ActivationKey { get; set; } = "ALT+G";
    public string ResetKey { get; set; } = "ALT+B";
}

public sealed class ParsedHotkey
{
    public bool RequiresAlt { get; init; }
    public bool RequiresCtrl { get; init; }
    public bool RequiresShift { get; init; }
    public int KeyCode { get; init; }
    public string DisplayName { get; init; } = string.Empty;
}


public sealed class UiConfig
{
    public bool DarkMode { get; set; } = false;
}


public sealed class AircraftConfig
{
    public bool RefuelBeforeBoarding { get; set; } = false;
    public bool CateringOnNewFlight { get; set; } = false;
    public string ActivationLvar { get; set; } = string.Empty;
    public bool AutoCloseDoors { get; set; } = false;
    public double ActivationValue { get; set; } = 1.0;
}
