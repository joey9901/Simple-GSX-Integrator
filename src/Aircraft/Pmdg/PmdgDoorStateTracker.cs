namespace SimpleGsxIntegrator.Aircraft;

internal sealed class DoorStateTracker
{
    public enum DoorState { Unknown, Open, Closed }

    private const double ClosedThreshold = 1e-4;

    private readonly record struct DoorSnapshot(double Value, DoorState State);

    private readonly Dictionary<uint, DoorSnapshot> _doors = new();

    public void Update(uint doorId, double value, string doorName = "")
    {
        if (double.IsNaN(value))
            return;

        if (!_doors.TryGetValue(doorId, out var previous))
        {
            RegisterNewDoor(doorId, value, doorName);
            return;
        }

        var newState = DetermineState(value, previous);

        if (newState != previous.State)
            Logger.Debug($"DoorTracker [{doorName}]: {previous.State} → {newState} (raw={value:F4})");

        _doors[doorId] = new DoorSnapshot(value, newState);
    }

    private void RegisterNewDoor(uint doorId, double value, string doorName)
    {
        var state = value <= ClosedThreshold ? DoorState.Closed : DoorState.Open;
        _doors[doorId] = new DoorSnapshot(value, state);
        Logger.Debug($"DoorTracker [{doorName}]: initial → {state} (raw={value:F4})");
    }

    private static DoorState DetermineState(double value, DoorSnapshot previous)
    {
        if (value > previous.Value)
            return DoorState.Open;

        if (value < previous.Value)
            return DoorState.Closed;

        if (value <= ClosedThreshold)
            return DoorState.Closed;

        return previous.State;
    }

    public DoorState GetState(uint doorId)
    {
        if (_doors.TryGetValue(doorId, out var snapshot))
            return snapshot.State;

        return DoorState.Unknown;
    }

    public bool IsOpen(uint doorId)
    {
        return GetState(doorId) == DoorState.Open;
    }

    public bool IsAnyOpen(IReadOnlyList<uint> doorIds)
    {
        foreach (uint id in doorIds)
            if (IsOpen(id)) return true;

        return false;
    }

    public IReadOnlySet<uint> GetOpenIds(IReadOnlyList<uint> doorIds)
    {
        var result = new HashSet<uint>();
        foreach (uint id in doorIds)
            if (IsOpen(id)) result.Add(id);
        return result;
    }

    public void Reset()
    {
        _doors.Clear();
    }
}
