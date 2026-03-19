using SimpleGsxIntegrator.Aircraft.A300;
using SimpleGsxIntegrator.Aircraft.A330;
using SimpleGsxIntegrator.Aircraft.FlyByWire;
using SimpleGsxIntegrator.Aircraft.Pmdg;
using SimpleGsxIntegrator.Aircraft.TFDi;

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
        if (Has(aircraftPath, "inibuilds", "A300")) return Adapter("iniBuilds A300", new IniA300Adapter());
        if (Has(aircraftPath, "TFDi_Design_MD-11")) return Adapter("TFDi MD-11", new Md11Adapter());

        if (Has(aircraftPath, "FlyByWire", "A380")) return Native("FlyByWire A380", new FbwA380Adapter());
        if (Has(aircraftPath, "FlyByWire", "A320")) return Native("FlyByWire A320", new FbwA380Adapter());
        if (Has(aircraftPath, "inibuilds", "A340")) return Native("iniBuilds A340");
        if (Has(aircraftPath, "inibuilds", "A350")) return Native("iniBuilds A350");
        if (Has(aircraftPath, "FNX_")) return Native("Fenix");

        return Unknown;
    }

    private static bool Has(string path, params string[] keywords)
    {
        foreach (string keyword in keywords)
        {
            if (!path.Contains(keyword, StringComparison.OrdinalIgnoreCase))
                return false;
        }
        return true;
    }

    private static MatchResult Adapter(string name, IAircraftAdapter adapter)
    {
        return new(MatchKind.Adapter, adapter, name);
    }

    private static MatchResult Native(string name)
    {
        return new(MatchKind.NativeIntegration, null, name);
    }

    private static MatchResult Native(string name, IAircraftAdapter adapter)
    {
        return new(MatchKind.NativeIntegration, adapter, name);
    }

    private static MatchResult NonFunctional(string name)
    {
        return new(MatchKind.NonFunctional, null, name);
    }
    private static readonly MatchResult Unknown = new(MatchKind.Unknown, null, null);
}
