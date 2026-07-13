using SimpleGsxIntegrator.Aircraft.A300;
using SimpleGsxIntegrator.Aircraft.A330;
using SimpleGsxIntegrator.Aircraft.FlyByWire;
using SimpleGsxIntegrator.Aircraft.Pmdg;
using SimpleGsxIntegrator.Aircraft.TFDi;

namespace SimpleGsxIntegrator.Aircraft;

public static class AircraftAdapterMatcher
{
    public enum MatchKind { Adapter, NativeIntegration, NonFunctional, Unknown }

    public record MatchResult(MatchKind Kind, AircraftAdapterBase? Adapter, string? DisplayName);

    public record AdapterCapabilities(bool CanManageGroundEquipment, bool CanRemoveCovers, bool CanCloseDoors)
    {
        public bool HasAny => CanManageGroundEquipment || CanRemoveCovers || CanCloseDoors;
        public static readonly AdapterCapabilities None = new(false, false, false);
    }

    public static MatchResult Resolve(string aircraftPath)
    {
        if (string.IsNullOrEmpty(aircraftPath)) return Unknown;

        if (Has(aircraftPath, "PMDG 777")) return Adapter("PMDG B777", new Pmdg777Adapter());
        if (Has(aircraftPath, "PMDG 737")) return Adapter("PMDG B737", new Pmdg737Adapter());

        if (Has(aircraftPath, "a346-pro")) return Native("Aerosoft/Toliss A346", new AerosoftA346Adapter());

        if (Has(aircraftPath, "microsoft-a330")) return Adapter("Microsoft/iniBuilds A330", new IniA330Adapter());

        if (Has(aircraftPath, "TFDi_Design_MD-11")) return Adapter("TFDi MD-11", new TfdiMd11Adapter());

        if (Has(aircraftPath, "FSLabs")) return Adapter("FSLabs A32NX", new FSLabsA320Adapter());

        if (Has(aircraftPath, "FlyByWire", "A380")) return Native("FlyByWire A380", new FbwA380Adapter());
        if (Has(aircraftPath, "FlyByWire", "A320")) return Native("FlyByWire A32NX", new FbwA380Adapter());

        if (Has(aircraftPath, "inibuilds", "A340")) return Native("iniBuilds A340");
        if (Has(aircraftPath, "inibuilds", "A350")) return Native("iniBuilds A350");
        if (Has(aircraftPath, "inibuilds", "A300")) return Adapter("iniBuilds A300", new IniA300Adapter());

        if (Has(aircraftPath, "FNX_")) return Native("Fenix A320 Family");

        return Unknown;
    }

    private static readonly (string[] Keywords, string Family)[] TitleFamilies =
    [
        (["PMDG",      "777"],  "PMDG B777"),
        (["PMDG",      "737"],  "PMDG B737"),
        (["FlyByWire", "A380"], "FlyByWire A380"),
        (["FlyByWire", "A32NX"], "FlyByWire A32NX"),
        (["MD-11"],             "TFDi MD-11"),
        (["FSLabs"],            "FSLabs A32NX"),
        (["A330"],              "Microsoft/iniBuilds A330"),
        (["A346"],              "Aerosoft/Toliss A346"),
        (["iniBuilds", "A300"], "iniBuilds A300"),
        (["iniBuilds", "A340"], "iniBuilds A340"),
        (["iniBuilds", "A350"], "iniBuilds A350"),
        (["Fenix"],             "Fenix A320 Family")
    ];

    private static readonly Dictionary<string, AdapterCapabilities> FamilyCaps = new()
    {
        ["PMDG B777"]                = new(true,  false, true),
        ["PMDG B737"]                = new(true,  false, true),
        ["Microsoft/iniBuilds A330"] = new(true,  true,  false),
        ["iniBuilds A300"]           = new(true,  false, false),
        ["TFDi MD-11"]               = new(true,  false, false),
        ["FlyByWire A380"]           = new(false, false, false),
        ["FlyByWire A32NX"]          = new(false, false, false),
        ["FSLabs A32NX"]             = new(false, false, false),
        ["Aerosoft/Toliss A346"]     = new(false, false, false),
        ["iniBuilds A340"]           = new(false, false, false),
        ["iniBuilds A350"]           = new(false, false, false),
        ["Fenix A320 Family"]        = new(false, false, false),
    };

    public static string? TryGetFamilyForTitle(string title)
    {
        foreach (var (keywords, family) in TitleFamilies)
            if (keywords.All(k => title.Contains(k, StringComparison.OrdinalIgnoreCase)))
                return family;
        return null;
    }

    public static AdapterCapabilities GetCapabilitiesForTitle(string title)
    {
        var family = TryGetFamilyForTitle(title);
        if (family != null && FamilyCaps.TryGetValue(family, out var caps))
            return caps;
        return AdapterCapabilities.None;
    }

    private static bool Has(string path, params string[] keywords)
    {
        foreach (string keyword in keywords)
            if (!path.Contains(keyword, StringComparison.OrdinalIgnoreCase))
                return false;
        return true;
    }

    private static MatchResult Adapter(string name, AircraftAdapterBase adapter) =>
        new(MatchKind.Adapter, adapter, name);

    private static MatchResult Native(string name) =>
        new(MatchKind.NativeIntegration, null, name);

    private static MatchResult Native(string name, AircraftAdapterBase adapter) =>
        new(MatchKind.NativeIntegration, adapter, name);

    private static readonly MatchResult Unknown = new(MatchKind.Unknown, null, null);
}