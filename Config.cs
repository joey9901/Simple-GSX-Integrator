namespace SimpleGsxIntegrator;

public class AppConfig
{
    public HotkeyConfig Hotkeys { get; set; } = new HotkeyConfig();
    public UiConfig UI { get; set; } = new UiConfig();
    public Dictionary<string, AircraftConfig> Aircraft { get; set; } = new Dictionary<string, AircraftConfig>();
}

public class UiConfig
{
    public bool DarkMode { get; set; } = false;
}

public class HotkeyConfig
{
    public string ActivationKey { get; set; } = "ALT+G";
    public string ResetKey { get; set; } = "ALT+B";
}

public class AircraftConfig
{
    public bool RefuelBeforeBoarding { get; set; } = true;
    public bool CateringOnNewFlight { get; set; } = false;
    public bool CateringOnTurnaround { get; set; } = false;
    public bool AutoCallTurnaroundServices { get; set; } = true;
    public int TurnaroundDelaySeconds { get; set; } = 120;
}

public class ParsedHotkey
{
    public bool RequiresAlt { get; set; }
    public bool RequiresCtrl { get; set; }
    public bool RequiresShift { get; set; }
    public int KeyCode { get; set; }
    public string DisplayName { get; set; } = "";
}

public static class HotkeyParser
{
    private static readonly Dictionary<string, int> KeyCodes = new()
    {
        { "A", 0x41 }, { "B", 0x42 }, { "C", 0x43 }, { "D", 0x44 }, { "E", 0x45 },
        { "F", 0x46 }, { "G", 0x47 }, { "H", 0x48 }, { "I", 0x49 }, { "J", 0x4A },
        { "K", 0x4B }, { "L", 0x4C }, { "M", 0x4D }, { "N", 0x4E }, { "O", 0x4F },
        { "P", 0x50 }, { "Q", 0x51 }, { "R", 0x52 }, { "S", 0x53 }, { "T", 0x54 },
        { "U", 0x55 }, { "V", 0x56 }, { "W", 0x57 }, { "X", 0x58 }, { "Y", 0x59 },
        { "Z", 0x5A },

        { "0", 0x30 }, { "1", 0x31 }, { "2", 0x32 }, { "3", 0x33 }, { "4", 0x34 },
        { "5", 0x35 }, { "6", 0x36 }, { "7", 0x37 }, { "8", 0x38 }, { "9", 0x39 },

        { "F1", 0x70 }, { "F2", 0x71 }, { "F3", 0x72 }, { "F4", 0x73 },
        { "F5", 0x74 }, { "F6", 0x75 }, { "F7", 0x76 }, { "F8", 0x77 },
        { "F9", 0x78 }, { "F10", 0x79 }, { "F11", 0x7A }, { "F12", 0x7B },

        { "ALT", 0x12 }, { "CONTROL", 0x11 }, { "CTRL", 0x11 }, { "SHIFT", 0x10 }
    };
    
    public static ParsedHotkey Parse(string hotkey)
    {
        var parsed = new ParsedHotkey { DisplayName = hotkey };
        
        if (string.IsNullOrWhiteSpace(hotkey))
        {
            Logger.Warning("Empty hotkey string");
            return parsed;
        }
        
        var parts = hotkey.ToUpperInvariant().Split('+').Select(p => p.Trim()).ToArray();
        
        string? mainKey = null;
        foreach (var part in parts)
        {
            switch (part)
            {
                case "ALT":
                    parsed.RequiresAlt = true;
                    break;
                case "CTRL":
                case "CONTROL":
                    parsed.RequiresCtrl = true;
                    break;
                case "SHIFT":
                    parsed.RequiresShift = true;
                    break;
                default:
                    mainKey = part;
                    break;
            }
        }
        
        if (mainKey != null && KeyCodes.TryGetValue(mainKey, out int keyCode))
        {
            parsed.KeyCode = keyCode;
        }
        else
        {
            Logger.Warning($"Unknown key in hotkey: {mainKey ?? "none"}");
        }
        
        return parsed;
    }
}

public static class ConfigManager
{
    private static readonly string ConfigDirectory = Path.Combine(AppContext.BaseDirectory, "config");
    private static readonly string ConfigFilePath = Path.Combine(AppContext.BaseDirectory, "config", "simplegsx.ini");
    private static AppConfig? _config;

    public static AppConfig Load()
    {
        try
        {
            if (!Directory.Exists(ConfigDirectory))
            {
                Directory.CreateDirectory(ConfigDirectory);
            }
            
            if (File.Exists(ConfigFilePath))
            {
                var config = new AppConfig();
                var lines = File.ReadAllLines(ConfigFilePath);
                string? currentSection = null;
                string? currentAircraft = null;

                foreach (var line in lines)
                {
                    var trimmed = line.Trim();
                    
                    if (string.IsNullOrWhiteSpace(trimmed) || trimmed.StartsWith("#") || trimmed.StartsWith(";"))
                        continue;
                    
                    if (trimmed.StartsWith("[") && trimmed.EndsWith("]"))
                    {
                        var sectionName = trimmed.Substring(1, trimmed.Length - 2);
                        
                        if (sectionName == "Hotkeys")
                        {
                            currentSection = "Hotkeys";
                            currentAircraft = null;
                        }
                        else if (sectionName == "UI")
                        {
                            currentSection = "UI";
                            currentAircraft = null;
                        }
                        else if (sectionName.StartsWith("Aircraft:"))
                        {
                            currentSection = "Aircraft";
                            currentAircraft = sectionName.Substring(9).Trim();
                            if (!config.Aircraft.ContainsKey(currentAircraft))
                            {
                                config.Aircraft[currentAircraft] = new AircraftConfig();
                            }
                        }
                        continue;
                    }
                    
                    var parts = trimmed.Split('=', 2);
                    if (parts.Length == 2)
                    {
                        var key = parts[0].Trim();
                        var value = parts[1].Trim();
                        
                        if (currentSection == "Hotkeys")
                        {
                            if (key == "ActivationKey")
                                config.Hotkeys.ActivationKey = value;
                            else if (key == "ResetKey")
                                config.Hotkeys.ResetKey = value;
                        }
                        else if (currentSection == "UI")
                        {
                            if (key == "DarkMode")
                                config.UI.DarkMode = value.Equals("true", StringComparison.OrdinalIgnoreCase);
                        }
                        else if (currentSection == "Aircraft" && currentAircraft != null)
                        {
                            if (key == "RefuelBeforeBoarding")
                                config.Aircraft[currentAircraft].RefuelBeforeBoarding = value.Equals("true", StringComparison.OrdinalIgnoreCase);
                            else if (key == "CateringOnNewFlight")
                                config.Aircraft[currentAircraft].CateringOnNewFlight = value.Equals("true", StringComparison.OrdinalIgnoreCase);
                            else if (key == "CateringOnTurnaround")
                                config.Aircraft[currentAircraft].CateringOnTurnaround = value.Equals("true", StringComparison.OrdinalIgnoreCase);
                            else if (key == "AutoCallTurnaroundServices")
                                config.Aircraft[currentAircraft].AutoCallTurnaroundServices = value.Equals("true", StringComparison.OrdinalIgnoreCase);
                            else if (key == "TurnaroundDelaySeconds" && int.TryParse(value, out int seconds))
                                config.Aircraft[currentAircraft].TurnaroundDelaySeconds = seconds;
                        }
                    }
                }
                
                _config = config;
                Logger.Debug($"Configuration loaded from {ConfigFilePath}");
                return _config;
            }
        }
        catch (Exception ex)
        {
            Logger.Warning($"Failed to load config: {ex.Message}");
        }

        _config = new AppConfig();
        Save(_config);
        Logger.Debug($"Default configuration created at {ConfigFilePath}");
        return _config;
    }

    public static void Save(AppConfig config)
    {
        try
        {
            if (!Directory.Exists(ConfigDirectory))
            {
                Directory.CreateDirectory(ConfigDirectory);
            }
            
            var lines = new List<string>
            {
                "# Simple GSX Integrator Configuration",
                "# Edit hotkeys below - format examples: ALT+G, CTRL+SHIFT+R, R",
                "",
                "[Hotkeys]",
                $"ActivationKey={config.Hotkeys.ActivationKey}",
                $"ResetKey={config.Hotkeys.ResetKey}",
                "",
                "[UI]",
                $"DarkMode={config.UI.DarkMode.ToString().ToLower()}",
                ""
            };
            
            if (config.Aircraft.Count > 0)
            {
                lines.Add("# Aircraft-specific settings");
                lines.Add("# Settings are saved automatically per aircraft type");
                lines.Add("");
                
                foreach (var kvp in config.Aircraft.OrderBy(x => x.Key))
                {
                    lines.Add($"[Aircraft:{kvp.Key}]");
                    lines.Add($"RefuelBeforeBoarding={kvp.Value.RefuelBeforeBoarding.ToString().ToLower()}");
                    lines.Add($"CateringOnNewFlight={kvp.Value.CateringOnNewFlight.ToString().ToLower()}");
                    lines.Add($"CateringOnTurnaround={kvp.Value.CateringOnTurnaround.ToString().ToLower()}");
                    lines.Add($"# Automatically call turnaround services (disable for one-way trips)");
                    lines.Add($"AutoCallTurnaroundServices={kvp.Value.AutoCallTurnaroundServices.ToString().ToLower()}");
                    lines.Add($"# Delay in seconds after deboarding completes before turnaround services start");
                    lines.Add($"TurnaroundDelaySeconds={kvp.Value.TurnaroundDelaySeconds}");
                    lines.Add("");
                }
            }
            
            File.WriteAllLines(ConfigFilePath, lines);
            _config = config;
            Logger.Debug($"Configuration saved to {ConfigFilePath}");
        }
        catch (Exception ex)
        {
            Logger.Warning($"Failed to save config: {ex.Message}");
        }
    }

    public static AppConfig GetConfig()
    {
        return _config ?? Load();
    }
    
    public static AircraftConfig GetAircraftConfig(string aircraftTitle)
    {
        if (_config == null)
        {
            _config = Load();
        }
        
        if (!_config.Aircraft.ContainsKey(aircraftTitle))
        {
            _config.Aircraft[aircraftTitle] = new AircraftConfig();
            Save(_config);
            Logger.Debug($"Added '{aircraftTitle}' to config with RefuelBeforeBoarding=true");
            Logger.Debug($"Config saved to: {Path.GetFullPath(ConfigFilePath)}");
        }
        
        return _config.Aircraft[aircraftTitle];
    }
    
    public static void SaveAircraftConfig(string aircraftTitle, AircraftConfig aircraftConfig)
    {
        if (_config == null)
        {
            _config = Load();
        }
        
        _config.Aircraft[aircraftTitle] = aircraftConfig;
        Save(_config);
    }
}
