using SimpleGsxIntegrator.Aircraft.A330;
using SimpleGsxIntegrator.Aircraft.Pmdg;

namespace SimpleGsxIntegrator.Aircraft;

public static class AircraftAdapterMatcher
{
    public enum MatchKind { Adapter, NativeIntegration, NonFunctional, Unknown }

    public record MatchResult(MatchKind Kind, IAircraftAdapter? Adapter, string? DisplayName);

    public static MatchResult Resolve(string aircraftPath)
    {
        if (string.IsNullOrEmpty(aircraftPath)) return Unknown;

        if (Has(aircraftPath, "PMDG 777")) return Adapter("PMDG 777", new Pmdg777Adapter());
        if (Has(aircraftPath, "PMDG 737")) return Adapter("PMDG 737", new Pmdg737Adapter());
        if (Has(aircraftPath, "microsoft-a330")) return Adapter("Microsoft/iniBuilds A330", new IniA330Adapter());

        if (Has(aircraftPath, "inibuilds", "A340")) return Native("iniBuilds A340");
        if (Has(aircraftPath, "inibuilds", "A350")) return Native("iniBuilds A350");
        if (Has(aircraftPath, "FNX_")) return Native("Fenix");

        if (Has(aircraftPath, "FlyByWire", "A380")) return NonFunctional("FlyByWire A380");

        return Unknown;
    }

    private static bool Has(string path, params string[] keywords)
    {
        return keywords.All(k => path.Contains(k, StringComparison.OrdinalIgnoreCase));
    }

    private static MatchResult Adapter(string name, IAircraftAdapter adapter)
    {
        return new(MatchKind.Adapter, adapter, name);
    }

    private static MatchResult Native(string name)
    {
        return new(MatchKind.NativeIntegration, null, name);
    }

    private static MatchResult NonFunctional(string name)
    {
        return new(MatchKind.NonFunctional, null, name);
    }
    private static readonly MatchResult Unknown = new(MatchKind.Unknown, null, null);
}
