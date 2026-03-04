namespace SimpleGsxIntegrator.Config;

// -----------------------------------------------------------------
//  Top-level config model
// -----------------------------------------------------------------

public sealed class AppConfig
{
    public HotkeyConfig Hotkeys { get; set; } = new();
    public UiConfig UI { get; set; } = new();
    public Dictionary<string, AircraftConfig> Aircraft { get; set; } = new();
}

// -----------------------------------------------------------------
//  Hotkeys
// -----------------------------------------------------------------

public sealed class HotkeyConfig
{
    public string ActivationKey { get; set; } = "ALT+G";
    public string ResetKey { get; set; } = "ALT+B";
}

/// <summary>Parsed, ready-to-check hotkey definition.</summary>
public sealed class ParsedHotkey
{
    public bool RequiresAlt { get; init; }
    public bool RequiresCtrl { get; init; }
    public bool RequiresShift { get; init; }
    public int KeyCode { get; init; }
    public string DisplayName { get; init; } = string.Empty;
}

// -----------------------------------------------------------------
//  UI
// -----------------------------------------------------------------

public sealed class UiConfig
{
    public bool DarkMode { get; set; } = false;
}

// -----------------------------------------------------------------
//  Per-aircraft settings
// -----------------------------------------------------------------

public sealed class AircraftConfig
{
    /// <summary>Call refueling before boarding instead of simultaneously.</summary>
    public bool RefuelBeforeBoarding { get; set; } = false;

    /// <summary>Call catering on a new first flight.</summary>
    public bool CateringOnNewFlight { get; set; } = false;

    /// <summary>
    /// Optional L:var name to watch as an in-sim activation trigger.
    /// When this L:var transitions to <see cref="ActivationValue"/>, the system toggles.
    /// </summary>
    public string ActivationLvar { get; set; } = string.Empty;

    /// <summary>
    /// If true, the door manager will automatically close open PMDG doors after boarding
    /// completes and before pushback is requested.
    /// </summary>
    public bool AutoCloseDoors { get; set; } = false;

    /// <summary>The value of <see cref="ActivationLvar"/> that triggers the toggle.</summary>
    public double ActivationValue { get; set; } = 1.0;
}
