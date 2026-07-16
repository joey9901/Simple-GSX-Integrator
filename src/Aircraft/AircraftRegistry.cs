using SimpleGsxIntegrator.Aircraft.A300;
using SimpleGsxIntegrator.Aircraft.A330;
using SimpleGsxIntegrator.Aircraft.Aerosoft;
using SimpleGsxIntegrator.Aircraft.FlyByWire;
using SimpleGsxIntegrator.Aircraft.FSLabs;
using SimpleGsxIntegrator.Aircraft.FSS;
using SimpleGsxIntegrator.Aircraft.JustFlight;
using SimpleGsxIntegrator.Aircraft.Pmdg;
using SimpleGsxIntegrator.Aircraft.TFDi;
using SimpleGsxIntegrator.Core;

namespace SimpleGsxIntegrator.Aircraft;

public enum AircraftSupportLevel { Custom, Native, Unknown }

public sealed record AircraftResolution(AircraftSupportLevel Level, string DisplayName, AircraftAdapterBase? Adapter);

public static class AircraftRegistry
{
    private sealed record Entry(string DisplayName, AircraftSupportLevel Level, Func<string, string, bool> Matches, Func<AircraftAdapterBase> Create);

    private static readonly Entry[] _entries =
    [
        new("PMDG B777", AircraftSupportLevel.Custom,
            (path, title) => path.HasAny("PMDG 777"),
            () => new Pmdg777Adapter()),

        new("PMDG B737", AircraftSupportLevel.Custom,
            (path, title) => path.HasAny("PMDG 737"),
            () => new Pmdg737Adapter()),

        new("Aerosoft/Toliss A346", AircraftSupportLevel.Native,
            (path, title) => path.HasAny("a346-pro"),
            () => new AeroA346Adapter()),

        new("FSS E-Jets Series", AircraftSupportLevel.Native,
            (path, title) => title.HasAny("FSS Embraer"),
            () => new FSSEJetsAdapter()),

        new("Just Flight Fokker 70/100", AircraftSupportLevel.Native,
            (path, title) => path.HasAny("Just Flight Fokker"),
            () => new JustFlightF100Adapter()),

        new("TFDi MD-11", AircraftSupportLevel.Custom,
            (path, title) => path.HasAny("TFDi_Design_MD-11"),
            () => new TfdiMd11Adapter()),

        new("FSLabs A32NX", AircraftSupportLevel.Custom,
            (path, title) => path.HasAny("FSLabs"),
            () => new FSLabsA320Adapter()),

        new("FlyByWire A380", AircraftSupportLevel.Native,
            (path, title) => path.HasAny("FlyByWire_A380"),
            () => new FbwA380Adapter()),

        new("FlyByWire A32NX", AircraftSupportLevel.Native,
            (path, title) => path.HasAny("FlyByWire_A320"),
            () => new FbwA32NXAdapter()),

        new("iniBuilds A300", AircraftSupportLevel.Custom,
            (path, title) => path.HasAll("inibuilds", "a300"),
            () => new IniA300Adapter()),

        new("Microsoft/iniBuilds A330", AircraftSupportLevel.Custom,
            (path, title) => path.HasAll("inibuilds", "a330"),
            () => new IniA330Adapter()),

        new("iniBuilds A340", AircraftSupportLevel.Native,
            (path, title) => path.HasAny("inibuilds-a340"),
            () => new AircraftAdapterBase()),

        new("iniBuilds A350", AircraftSupportLevel.Native,
            (path, title) => path.HasAll("inibuilds", "A350"),
            () => new AircraftAdapterBase()),

        new("Fenix A320 Family", AircraftSupportLevel.Native,
            (path, title) => path.HasAny("FNX_320", "FNX_321", "FNX_319"),
            () => new AircraftAdapterBase()),
    ];

    public static AircraftResolution Resolve(string aircraftPath, string aircraftTitle)
    {
        var path = aircraftPath ?? "";
        var title = aircraftTitle ?? "";
        var entry = _entries.FirstOrDefault(e => e.Matches(path, title));
        return entry != null
            ? new AircraftResolution(entry.Level, entry.DisplayName, entry.Create())
            : new AircraftResolution(AircraftSupportLevel.Unknown, title, null);
    }

    public static string? FindDisplayName(string title) =>
        Resolve("", title) is { Level: not AircraftSupportLevel.Unknown } r ? r.DisplayName : null;
}
