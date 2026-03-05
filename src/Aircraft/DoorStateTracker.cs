namespace SimpleGsxIntegrator.Aircraft;

/// <summary>
/// Tracks the open/closed state of aircraft doors by observing the direction of
/// change in each door's raw L:var value
/// </summary>
internal sealed class DoorStateTracker
{

    public enum DoorState { Unknown, Open, Closed }

    private const double ZeroThreshold = 1e-4;

    private readonly record struct DoorEntry(double LastRaw, DoorState State);

    private readonly Dictionary<uint, DoorEntry> _doors = new();

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

    public DoorState GetState(uint doorId)
        => _doors.TryGetValue(doorId, out var e) ? e.State : DoorState.Unknown;

    public bool IsOpen(uint doorId) => GetState(doorId) == DoorState.Open;

    public bool IsAnyOpen(IReadOnlyList<uint> doorIds) => doorIds.Any(IsOpen);

    public IReadOnlySet<uint> GetOpenIds(IReadOnlyList<uint> doorIds)
    {
        var result = new HashSet<uint>();
        foreach (uint id in doorIds)
            if (IsOpen(id)) result.Add(id);
        return result;
    }

    public void Reset() => _doors.Clear();
}
