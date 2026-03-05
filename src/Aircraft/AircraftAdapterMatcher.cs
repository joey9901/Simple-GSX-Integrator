using Microsoft.FlightSimulator.SimConnect;
using SimpleGsxIntegrator.Aircraft.Pmdg;

namespace SimpleGsxIntegrator.Aircraft;

/// <summary>
/// Creates the correct <see cref="IAircraftAdapter"/> for a given aircraft.
///
/// Aircraft matching uses the full aircraft path returned by SimConnect
/// Add new entries here when adding support for additional aircraft.
/// </summary>
public static class AircraftAdapterMatcher
{
    private record AdapterRegistration(
        Func<string, bool> Matches,
        Func<IAircraftAdapter> Matcher,
        string FriendlyName);

    private static readonly IReadOnlyList<AdapterRegistration> Registrations =
        new AdapterRegistration[]
        {
            new(
                path => !string.IsNullOrEmpty(path) &&
                        (path.Contains("PMDG 777",  StringComparison.OrdinalIgnoreCase)),
                () => new Pmdg777Adapter(),
                "PMDG 777"),

            new(
                path => !string.IsNullOrEmpty(path) &&
                        path.Contains("PMDG 737", StringComparison.OrdinalIgnoreCase),
                () => new Pmdg737Adapter(),
                "PMDG 737"),
        };

    /// <summary>
    /// Returns an adapter for the given aircraft path/title, or null if no
    /// registered adapter matches (aircraft runs without door/equipment integration).
    /// </summary>
    public static IAircraftAdapter? Create(string aircraftPathOrTitle)
    {
        if (string.IsNullOrEmpty(aircraftPathOrTitle))
            return null;

        foreach (var reg in Registrations)
        {
            if (reg.Matches(aircraftPathOrTitle))
            {
                Logger.Debug($"AircraftAdapterMatcher: matched adapter '{reg.FriendlyName}' for '{aircraftPathOrTitle}'");
                return reg.Matcher();
            }
        }

        Logger.Debug($"AircraftAdapterMatcher: no adapter matched for '{aircraftPathOrTitle}' – running without aircraft integration");
        return null;
    }
}
