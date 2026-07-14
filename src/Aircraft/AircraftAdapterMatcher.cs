using SimpleGsxIntegrator.Aircraft.A300;
using SimpleGsxIntegrator.Aircraft.A330;
using SimpleGsxIntegrator.Aircraft.Fenix;
using SimpleGsxIntegrator.Aircraft.FlyByWire;
using SimpleGsxIntegrator.Aircraft.iniBuilds;
using SimpleGsxIntegrator.Aircraft.Pmdg;
using SimpleGsxIntegrator.Aircraft.TFDi;

namespace SimpleGsxIntegrator.Aircraft;

public static class AircraftAdapterMatcher
{
    public enum MatchKind { Adapter, NativeIntegration, NonFunctional, Unknown }

    public record MatchResult(MatchKind Kind, AircraftAdapterBase? Adapter);

    private static readonly AircraftAdapterBase[] _catalog =
    [
        new Pmdg777Adapter(),
        new Pmdg737Adapter(),
        new IniA330Adapter(),
        new IniA300Adapter(),
        new IniA340Adapter(),
        new IniA350Adapter(),
        new TfdiMd11Adapter(),
        new FbwA380Adapter(),
        new FbwA32NXAdapter(),
        new FSLabsA320Adapter(),
        new AerosoftA346Adapter(),
        new JustFlightF100Adapter(),
        new FenixA320Adapter(),
    ];

    public static AircraftAdapterBase? FindByTitle(string title)
    {
        if (string.IsNullOrEmpty(title)) return null;
        return _catalog.FirstOrDefault(a =>
            a.TitleKeywords.All(k => title.Contains(k, StringComparison.OrdinalIgnoreCase)));
    }

    public static MatchResult Resolve(string aircraftPath)
    {
        if (string.IsNullOrEmpty(aircraftPath)) return Unknown;

        if (Contains(aircraftPath, "PMDG 777")) return Adapter(new Pmdg777Adapter());
        if (Contains(aircraftPath, "PMDG 737")) return Adapter(new Pmdg737Adapter());

        if (Contains(aircraftPath, "a346-pro")) return Native(new AerosoftA346Adapter());

        if (Contains(aircraftPath, "Just Flight Fokker")) return Native(new JustFlightF100Adapter());

        if (ContainsAll(aircraftPath, "inibuilds", "a330")) return Adapter(new IniA330Adapter());

        if (Contains(aircraftPath, "TFDi_Design_MD-11")) return Adapter(new TfdiMd11Adapter());

        if (Contains(aircraftPath, "FSLabs")) return Adapter(new FSLabsA320Adapter());

        if (Contains(aircraftPath, "FlyByWire_A380")) return Native(new FbwA380Adapter());
        if (Contains(aircraftPath, "FlyByWire_A320")) return Native(new FbwA32NXAdapter());

        if (Contains(aircraftPath, "inibuilds-a340")) return Native(new IniA340Adapter());
        if (ContainsAll(aircraftPath, "inibuilds", "A350")) return Native(new IniA350Adapter());
        if (ContainsAll(aircraftPath, "inibuilds", "a300")) return Adapter(new IniA300Adapter());

        if (Contains(aircraftPath, "FNX_320", "FNX_321", "FNX_319")) return Native(new FenixA320Adapter());

        return Unknown;
    }

    private static bool Contains(string path, params string[] keywords)
    {
        foreach (string keyword in keywords)
            if (path.Contains(keyword, StringComparison.OrdinalIgnoreCase))
                return true;
        return false;
    }

    private static bool ContainsAll(string path, params string[] keywords)
    {
        foreach (string keyword in keywords)
            if (!path.Contains(keyword, StringComparison.OrdinalIgnoreCase))
                return false;
        return true;
    }

    private static MatchResult Adapter(AircraftAdapterBase adapter) => new(MatchKind.Adapter, adapter);
    private static MatchResult Native(AircraftAdapterBase adapter) => new(MatchKind.NativeIntegration, adapter);
    private static readonly MatchResult Unknown = new(MatchKind.Unknown, null);
}
