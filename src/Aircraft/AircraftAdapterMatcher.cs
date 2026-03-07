using SimpleGsxIntegrator.Aircraft.A330;
using SimpleGsxIntegrator.Aircraft.Pmdg;

namespace SimpleGsxIntegrator.Aircraft;

/// <summary>
/// Resolves the correct <see cref="IAircraftAdapter"/> for a given aircraft path or title.
/// Add new entries to <see cref="SupportedAdapters"/> when adding support for additional aircraft.
/// Add entries to <see cref="KnownNativeIntegrations"/> for aircraft that have their own GSX
/// integration and need no adapter.
/// </summary>
public static class AircraftAdapterMatcher
{
    private record AdapterEntry(
        Func<string, bool> IsMatch,
        Func<IAircraftAdapter> CreateAdapter,
        string DisplayName);

    private static readonly IReadOnlyList<AdapterEntry> SupportedAdapters =
        new AdapterEntry[]
        {
            new(
                path => !string.IsNullOrEmpty(path) &&
                        path.Contains("PMDG 777", StringComparison.OrdinalIgnoreCase),
                () => new Pmdg777Adapter(),
                "PMDG 777"),

            new(
                path => !string.IsNullOrEmpty(path) &&
                        path.Contains("PMDG 737", StringComparison.OrdinalIgnoreCase),
                () => new Pmdg737Adapter(),
                "PMDG 737"),
            new(
                path => !string.IsNullOrEmpty(path) &&
                        path.Contains("microsoft-a330", StringComparison.OrdinalIgnoreCase),
                () => new IniA330Adapter(),
                "Microsoft/iniBuilds A330"),
        };

    /// <summary>
    /// Aircraft that have their own native GSX integration – no adapter needed.
    /// </summary>
    private static readonly IReadOnlyList<(Func<string, bool> IsMatch, string DisplayName)> KnownNativeIntegrations =
        new (Func<string, bool>, string)[]
        {
            (path => !string.IsNullOrEmpty(path) &&
                    path.Contains("inibuilds", StringComparison.OrdinalIgnoreCase) &&
                    path.Contains("A340", StringComparison.OrdinalIgnoreCase),
                    "iniBuilds A340"),
            (path => !string.IsNullOrEmpty(path) &&
                    path.Contains("inibuilds", StringComparison.OrdinalIgnoreCase) &&
                    path.Contains("A350", StringComparison.OrdinalIgnoreCase),
                    "iniBuilds A350"),
            (path => !string.IsNullOrEmpty(path) &&
                    path.Contains("FNX_", StringComparison.OrdinalIgnoreCase), "Fenix"),
        };

    private static readonly IReadOnlyList<(Func<string, bool> IsMatch, string DisplayName)> KnownNonFunctionalIntegrations =
        new (Func<string, bool>, string)[]
        {
            (path => !string.IsNullOrEmpty(path) &&
                    path.Contains("FlyByWire", StringComparison.OrdinalIgnoreCase) &&
                    path.Contains("A380", StringComparison.OrdinalIgnoreCase), "FlyByWire A380"),
        };

    public enum MatchKind { Adapter, NativeIntegration, NonFunctional, Unknown }

    public record MatchResult(MatchKind Kind, IAircraftAdapter? Adapter, string? DisplayName);

    /// <summary>
    /// Resolves an adapter for the given aircraft path.
    /// Check <see cref="MatchResult.Kind"/> to distinguish between a matched adapter,
    /// a known aircraft with native GSX integration, and an unrecognised aircraft.
    /// </summary>
    public static MatchResult Resolve(string aircraftPath)
    {
        if (string.IsNullOrEmpty(aircraftPath))
            return new MatchResult(MatchKind.Unknown, null, null);

        // Check specific adapters first – they take priority over generic native-integration matches.
        foreach (var entry in SupportedAdapters)
        {
            if (entry.IsMatch(aircraftPath))
                return new MatchResult(MatchKind.Adapter, entry.CreateAdapter(), entry.DisplayName);
        }

        foreach (var (isMatch, displayName) in KnownNativeIntegrations)
        {
            if (isMatch(aircraftPath))
                return new MatchResult(MatchKind.NativeIntegration, null, displayName);
        }

        foreach (var (isMatch, displayName) in KnownNonFunctionalIntegrations)
        {
            if (isMatch(aircraftPath))
                return new MatchResult(MatchKind.NonFunctional, null, displayName);
        }

        return new MatchResult(MatchKind.Unknown, null, null);
    }
}
