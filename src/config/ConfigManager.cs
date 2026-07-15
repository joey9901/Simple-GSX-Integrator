using SimpleGsxIntegrator.Aircraft;

namespace SimpleGsxIntegrator.Config;

public static class ConfigManager
{
    private static readonly string ConfigDir = Path.Combine(AppContext.BaseDirectory, "config");
    private static readonly string ConfigPath = Path.Combine(ConfigDir, "simplegsx.ini");

    private static AppConfig _config = Load();

    public static AppConfig GetConfig() => _config;

    private static string NormalizeTitle(string title) =>
        AircraftAdapterMatcher.FindByTitle(title)?.DisplayName ?? title;

    public static AircraftConfig GetAircraftConfig(string aircraftTitle)
    {
        if (string.IsNullOrWhiteSpace(aircraftTitle))
            return new AircraftConfig();

        var normalized = NormalizeTitle(aircraftTitle);

        if (_config.Aircraft.TryGetValue(normalized, out var cfg))
            return cfg;

        // Migrate any legacy entry whose raw title resolves to the same family
        var legacyKey = _config.Aircraft.Keys
            .FirstOrDefault(k => NormalizeTitle(k) == normalized);

        if (legacyKey != null)
        {
            cfg = _config.Aircraft[legacyKey];
            _config.Aircraft.Remove(legacyKey);
            _config.Aircraft[normalized] = cfg;
            WriteFile(_config);
            Logger.Debug($"ConfigManager: migrated '{legacyKey}' → '{normalized}'");
            return cfg;
        }

        cfg = new AircraftConfig();
        _config.Aircraft[normalized] = cfg;
        WriteFile(_config);
        Logger.Debug($"ConfigManager: created default config for '{normalized}'");
        return cfg;
    }

    public static void SaveAircraftConfig(string aircraftTitle, AircraftConfig cfg)
    {
        if (string.IsNullOrWhiteSpace(aircraftTitle)) return;
        _config.Aircraft[NormalizeTitle(aircraftTitle)] = cfg;
        WriteFile(_config);
    }

    public static void Save(AppConfig config)
    {
        _config = config;
        WriteFile(config);
    }

    public static IReadOnlyList<string> GetSavedAircraftTitles()
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var result = new List<string>();
        foreach (var key in _config.Aircraft.Keys.OrderBy(k => k))
        {
            var normalized = NormalizeTitle(key);
            if (seen.Add(normalized))
                result.Add(normalized);
        }
        return result;
    }

    private static AppConfig Load()
    {
        try
        {
            if (!Directory.Exists(ConfigDir)) Directory.CreateDirectory(ConfigDir);

            if (!File.Exists(ConfigPath))
            {
                var fresh = new AppConfig();
                WriteFile(fresh);
                Logger.Debug($"ConfigManager: created default config at {ConfigPath}");
                return fresh;
            }

            var config = new AppConfig();
            string? section = null;
            string? aircraft = null;

            foreach (var raw in File.ReadAllLines(ConfigPath))
            {
                var line = raw.Trim();
                if (string.IsNullOrEmpty(line) || line.StartsWith('#') || line.StartsWith(';'))
                    continue;

                if (line.StartsWith('[') && line.EndsWith(']'))
                {
                    var name = line[1..^1];
                    if (name == "Hotkeys") { section = "Hotkeys"; aircraft = null; }
                    else if (name.StartsWith("Aircraft:"))
                    {
                        section = "Aircraft";
                        aircraft = name["Aircraft:".Length..].Trim();
                        if (!config.Aircraft.ContainsKey(aircraft))
                            config.Aircraft[aircraft] = new AircraftConfig();
                    }
                    continue;
                }

                var sep = line.IndexOf('=');
                if (sep < 0) continue;

                var key = line[..sep].Trim();
                var val = line[(sep + 1)..].Trim();

                switch (section)
                {
                    case "Hotkeys":
                        if (key == "ActivationKey") config.Hotkeys.ActivationKey = val;
                        if (key == "ResetKey") config.Hotkeys.ResetKey = val;
                        break;

                    case "Aircraft" when aircraft != null:
                        var ac = config.Aircraft[aircraft];
                        if (key == "RefuelBeforeBoarding") ac.RefuelBeforeBoarding = ParseBool(val);
                        if (key == "CateringOnNewFlight") ac.CateringOnNewFlight = ParseBool(val);
                        if (key == "RealisticCrewComms") ac.RealisticCrewComms = ParseBool(val);
                        if (key == "DisableRemoteControl") ac.DisableRemoteControl = ParseBool(val);
                        if (key == "RemoveCovers") ac.RemoveCovers = ParseBool(val);
                        if (key == "ManageGroundEquipment") ac.ManageGroundEquipment = ParseBool(val);
                        if (key == "ManageDoors") ac.ManageDoors = ParseBool(val);
                        if (key == "ActivationLvar") ac.ActivationLvar = val;
                        if (key == "ActivationValue" && double.TryParse(val, out double av)) ac.ActivationValue = av;
                        break;
                }
            }

            Logger.Debug($"ConfigManager: loaded from {ConfigPath}");
            return config;
        }
        catch (Exception ex)
        {
            Logger.Warning($"ConfigManager: load failed ({ex.Message}), using defaults");
            return new AppConfig();
        }
    }

    private static void WriteFile(AppConfig config)
    {
        try
        {
            if (!Directory.Exists(ConfigDir)) Directory.CreateDirectory(ConfigDir);

            var lines = new List<string>
            {
                "# Simple GSX Integrator Configuration",
                "",
                "[Hotkeys]",
                $"ActivationKey={config.Hotkeys.ActivationKey}",
                $"ResetKey={config.Hotkeys.ResetKey}",
                "",
            };

            foreach (var (title, ac) in config.Aircraft.OrderBy(e => e.Key))
            {
                lines.Add($"[Aircraft:{title}]");
                lines.Add($"RefuelBeforeBoarding={ac.RefuelBeforeBoarding.ToString().ToLowerInvariant()}");
                lines.Add($"CateringOnNewFlight={ac.CateringOnNewFlight.ToString().ToLowerInvariant()}");
                lines.Add($"RealisticCrewComms={ac.RealisticCrewComms.ToString().ToLowerInvariant()}");
                lines.Add($"DisableRemoteControl={ac.DisableRemoteControl.ToString().ToLowerInvariant()}");
                lines.Add($"RemoveCovers={ac.RemoveCovers.ToString().ToLowerInvariant()}");
                lines.Add($"ManageGroundEquipment={ac.ManageGroundEquipment.ToString().ToLowerInvariant()}");
                lines.Add($"ManageDoors={ac.ManageDoors.ToString().ToLowerInvariant()}");
                lines.Add($"ActivationLvar={ac.ActivationLvar}");
                lines.Add($"ActivationValue={ac.ActivationValue}");
                lines.Add(string.Empty);
            }

            File.WriteAllLines(ConfigPath, lines);
        }
        catch (Exception ex)
        {
            Logger.Warning($"ConfigManager: save failed: {ex.Message}");
        }
    }

    private static bool ParseBool(string s)
    {
        return s.Equals("true", StringComparison.OrdinalIgnoreCase);
    }
}

public static class HotkeyParser
{
    private static readonly Dictionary<string, int> KeyMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["A"] = 0x41,
        ["B"] = 0x42,
        ["C"] = 0x43,
        ["D"] = 0x44,
        ["E"] = 0x45,
        ["F"] = 0x46,
        ["G"] = 0x47,
        ["H"] = 0x48,
        ["I"] = 0x49,
        ["J"] = 0x4A,
        ["K"] = 0x4B,
        ["L"] = 0x4C,
        ["M"] = 0x4D,
        ["N"] = 0x4E,
        ["O"] = 0x4F,
        ["P"] = 0x50,
        ["Q"] = 0x51,
        ["R"] = 0x52,
        ["S"] = 0x53,
        ["T"] = 0x54,
        ["U"] = 0x55,
        ["V"] = 0x56,
        ["W"] = 0x57,
        ["X"] = 0x58,
        ["Y"] = 0x59,
        ["Z"] = 0x5A,
        ["0"] = 0x30,
        ["1"] = 0x31,
        ["2"] = 0x32,
        ["3"] = 0x33,
        ["4"] = 0x34,
        ["5"] = 0x35,
        ["6"] = 0x36,
        ["7"] = 0x37,
        ["8"] = 0x38,
        ["9"] = 0x39,
        ["F1"] = 0x70,
        ["F2"] = 0x71,
        ["F3"] = 0x72,
        ["F4"] = 0x73,
        ["F5"] = 0x74,
        ["F6"] = 0x75,
        ["F7"] = 0x76,
        ["F8"] = 0x77,
        ["F9"] = 0x78,
        ["F10"] = 0x79,
        ["F11"] = 0x7A,
        ["F12"] = 0x7B,
    };

    public static ParsedHotkey Parse(string hotkey)
    {
        if (string.IsNullOrWhiteSpace(hotkey))
            return new ParsedHotkey { DisplayName = hotkey };

        var parts = hotkey.ToUpperInvariant().Split('+').Select(p => p.Trim()).ToArray();

        bool alt = false, ctrl = false, shift = false;
        int keyCode = 0;

        foreach (var part in parts)
        {
            if (part is "ALT") { alt = true; continue; }
            if (part is "CTRL" or "CONTROL") { ctrl = true; continue; }
            if (part is "SHIFT") { shift = true; continue; }
            if (KeyMap.TryGetValue(part, out int code)) keyCode = code;
        }

        return new ParsedHotkey
        {
            RequiresAlt = alt,
            RequiresCtrl = ctrl,
            RequiresShift = shift,
            KeyCode = keyCode,
            DisplayName = hotkey,
        };
    }
}
