namespace SimpleGsxIntegrator.Aircraft;

/// <summary>
/// Tracks the open/closed state of aircraft doors by observing the <em>direction of
/// change</em> in each door's raw L:var value rather than comparing against a fixed
/// threshold.
///
/// <para>Transition rules applied on every new sample:</para>
/// <list type="bullet">
///   <item>First observation: value ≈ 0 → <see cref="DoorState.Closed"/>, value &gt; 0 → <see cref="DoorState.Open"/></item>
///   <item>Value increased               → <see cref="DoorState.Open"/>   (door is moving open)</item>
///   <item>Value decreased to ≈ 0        → <see cref="DoorState.Closed"/> (door has fully shut)</item>
///   <item>Value decreased but still &gt; 0 → <see cref="DoorState.Open"/>   (door closing, not yet shut)</item>
///   <item>Value unchanged               → state carries over unchanged</item>
/// </list>
///
/// <para>
/// No hard threshold is used for "is this door open?" — only whether the value
/// is effectively zero (≤ <see cref="ZeroThreshold"/>) determines <see cref="DoorState.Closed"/>.
/// </para>
/// </summary>
internal sealed class DoorStateTracker
{

    public enum DoorState { Unknown, Open, Closed }


    /// <summary>
    /// Raw values at or below this are treated as "effectively zero / fully closed".
    /// Sized to swallow floating-point noise while remaining far below any real
    /// door-open value (PMDG 737 uses 0/1, PMDG 777 uses 0–100).
    /// </summary>
    private const double ZeroThreshold = 1e-4;


    private readonly record struct DoorEntry(double LastRaw, DoorState State);


    private readonly Dictionary<uint, DoorEntry> _doors = new();


    /// <summary>
    /// Feed the latest raw L:var reading for a door. Call once per SimConnect
    /// data callback for every door being tracked.
    /// </summary>
    /// <param name="doorId">Unique door identifier (typically the PMDG event code).</param>
    /// <param name="rawValue">Current L:var value. Pass <see cref="double.NaN"/> to skip.</param>
    /// <param name="doorName">Human-readable name used in log messages.</param>
    public void Update(uint doorId, double rawValue, string doorName = "")
    {
        if (double.IsNaN(rawValue)) return;

        bool isZero = rawValue <= ZeroThreshold;

        if (!_doors.TryGetValue(doorId, out var entry))
        {
            // First observation – seed state directly from value, no motion needed.
            var initial = isZero ? DoorState.Closed : DoorState.Open;
            _doors[doorId] = new DoorEntry(rawValue, initial);
            Logger.Debug($"DoorTracker [{doorName}]: initial → {initial} (raw={rawValue:F4})");
            return;
        }

        DoorState newState;

        if (rawValue > entry.LastRaw + ZeroThreshold)
        {
            // Value is increasing → door is opening.
            newState = DoorState.Open;
        }
        else if (rawValue < entry.LastRaw - ZeroThreshold)
        {
            // Value is decreasing → door is closing.
            // It is only Closed once it reaches effectively-zero.
            newState = DoorState.Closed;
        }
        else
        {
            // Value is stable → if effectively zero, confirm closed; otherwise carry existing state.
            newState = isZero ? DoorState.Closed : entry.State;
        }

        if (newState != entry.State)
            Logger.Debug($"DoorTracker [{doorName}]: {entry.State} → {newState} (raw={rawValue:F4})");

        _doors[doorId] = new DoorEntry(rawValue, newState);
    }

    /// <summary>Returns the current state for a door, or <see cref="DoorState.Unknown"/> if not yet observed.</summary>
    public DoorState GetState(uint doorId)
        => _doors.TryGetValue(doorId, out var e) ? e.State : DoorState.Unknown;

    /// <summary>Returns true if the door is known to be open (or in motion).</summary>
    public bool IsOpen(uint doorId) => GetState(doorId) == DoorState.Open;

    /// <summary>Returns true if any door in the supplied list is open or in motion.</summary>
    public bool IsAnyOpen(IReadOnlyList<uint> doorIds) => doorIds.Any(IsOpen);

    /// <summary>Returns the set of door IDs from the supplied list that are open or in motion.</summary>
    public IReadOnlySet<uint> GetOpenIds(IReadOnlyList<uint> doorIds)
    {
        var result = new HashSet<uint>();
        foreach (uint id in doorIds)
            if (IsOpen(id)) result.Add(id);
        return result;
    }

    /// <summary>Clears all tracked state. Call on SimConnect disconnect or aircraft change.</summary>
    public void Reset() => _doors.Clear();
}
