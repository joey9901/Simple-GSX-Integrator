using Microsoft.FlightSimulator.SimConnect;
using SimpleGsxIntegrator.Core;
using System.Runtime.InteropServices;

namespace SimpleGsxIntegrator.Aircraft.A330;

public sealed class IniA330Adapter : IAircraftAdapter
{
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    private struct ScalarStruct { public double Value; }

    private SimConnect? _sc;

    // Built during enumeration: event name ↔ hash
    private readonly Dictionary<string, ulong> _nameToHash = new();
    private readonly Dictionary<ulong, string> _hashToName = new();

    // Live state: event name → current double value (0=closed/removed, 1=open/placed)
    private readonly Dictionary<string, double> _values = new();

    private enum EvtId : uint { SetExit = 100 }
    private enum Group : uint { Default = 0 }

    private static readonly HashSet<string> TargetEvents =
    [
        A330Constants.IE_DOOR_1L,
        A330Constants.IE_DOOR_1R,
        A330Constants.IE_DOOR_2L,
        A330Constants.IE_DOOR_2R,
        A330Constants.IE_DOOR_3L,
        A330Constants.IE_DOOR_3R,
        A330Constants.IE_CARGO_FWD,
        A330Constants.IE_CARGO_AFT
    ];

    public void OnSimConnectConnected(SimConnect sc)
    {
        // Detach handlers from any previous SimConnect instance
        if (_sc != null)
        {
            _sc.OnRecvEnumerateInputEvents -= HandleEnumerateInputEvents;
            _sc.OnRecvSubscribeInputEvent -= HandleSubscribeInputEvent;
        }

        _sc = sc;
        _nameToHash.Clear();
        _hashToName.Clear();
        _values.Clear();

        sc.OnRecvEnumerateInputEvents += HandleEnumerateInputEvents;
        sc.OnRecvSubscribeInputEvent += HandleSubscribeInputEvent;

        // Register writable SimVars for ground equipment
        sc.AddToDataDefinition(SimDef.A330Chocks,
            A330Constants.AVar_Chocks, "Number", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
        sc.RegisterDataDefineStruct<ScalarStruct>(SimDef.A330Chocks);

        sc.AddToDataDefinition(SimDef.A330Gpu,
            A330Constants.LVar_Gpu, "Number", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
        sc.RegisterDataDefineStruct<ScalarStruct>(SimDef.A330Gpu);

        sc.MapClientEventToSimEvent(EvtId.SetExit, "SET_AIRCRAFT_EXIT");

        try
        {
            sc.EnumerateInputEvents(SimReq.A330InputEventEnum);
            Logger.Debug("IniA330Adapter: requested Input Event enumeration");
        }
        catch (Exception ex)
        {
            Logger.Warning($"IniA330Adapter: Input Event enumeration request failed: {ex.Message}");
        }
    }

    public void OnSimObjectData(SIMCONNECT_RECV_SIMOBJECT_DATA data) { }

    private void HandleEnumerateInputEvents(SimConnect _, SIMCONNECT_RECV_ENUMERATE_INPUT_EVENTS data)
    {
        for (int i = 0; i < data.dwArraySize; i++)
        {
            var descriptor = (SIMCONNECT_INPUT_EVENT_DESCRIPTOR)data.rgData[i];
            if (!TargetEvents.Contains(descriptor.Name)) continue;
            if (_nameToHash.ContainsKey(descriptor.Name)) continue; // take first occurrence

            _nameToHash[descriptor.Name] = descriptor.Hash;
            _hashToName[descriptor.Hash] = descriptor.Name;
            Logger.Debug($"IniA330Adapter: found '{descriptor.Name}' hash=0x{descriptor.Hash:X16}");
        }

        bool lastPage = data.dwEntryNumber + data.dwArraySize >= data.dwOutOf;
        if (!lastPage) return;

        foreach (var (name, hash) in _nameToHash)
        {
            try
            {
                _sc?.SubscribeInputEvent(hash);
                Logger.Debug($"IniA330Adapter: subscribed to '{name}'");
            }
            catch (Exception ex)
            {
                Logger.Warning($"IniA330Adapter: subscribe to '{name}' failed: {ex.Message}");
            }
        }
    }

    private void HandleSubscribeInputEvent(SimConnect _, SIMCONNECT_RECV_SUBSCRIBE_INPUT_EVENT data)
    {
        if (!_hashToName.TryGetValue(data.Hash, out string? name)) return;

        double value = data.Value is { Length: > 0 }
            ? Convert.ToDouble(data.Value[0])
            : 0.0;

        bool changed = !_values.TryGetValue(name, out double prev) || Math.Abs(prev - value) > 0.5;
        _values[name] = value;

        if (changed)
            Logger.Debug($"IniA330Adapter: '{name}' changed → {value:F1}");
    }

    private async Task CloseAllOpenDoorsAsync()
    {
        var open = A330Constants.AllDoorIds.Where(IsDoorOpen).ToList();
        if (open.Count == 0) return;

        Logger.Info($"IniA330Adapter: Closing {open.Count} open door(s)");
        foreach (uint doorId in open)
        {
            CloseExit(doorId);
            await Task.Delay(300);
        }
    }

    private void CloseDoor(uint doorId)
    {
        if (!IsDoorOpen(doorId))
        {
            Logger.Debug($"IniA330Adapter: {GetDoorName(doorId)} already closed");
            return;
        }

        Logger.Info($"IniA330Adapter: closing {GetDoorName(doorId)}");
        CloseExit(doorId);
    }

    private void RemoveGroundEquipment()
    {
        if (_sc == null) return;
        Logger.Debug("IniA330Adapter: removing chocks (L:COVER ON:0 = 0)");
        WriteSimVar(SimDef.A330Chocks, 0.0);
        Logger.Debug("IniA330Adapter: removing GPU (L:INI_GPU_AVAIL = 0)");
        WriteSimVar(SimDef.A330Gpu, 0.0);
    }

    private Task PlaceGroundEquipmentAndChocks()
    {
        if (_sc == null) return Task.CompletedTask;
        Logger.Info("IniA330Adapter: Placing Chocks and GPU");
        Logger.Debug("IniA330Adapter: placing chocks (COVER ON:0 = 1)");
        WriteSimVar(SimDef.A330Chocks, 1.0);
        Logger.Debug("IniA330Adapter: placing GPU (L:INI_GPU_AVAIL = 1)");
        WriteSimVar(SimDef.A330Gpu, 1.0);
        return Task.CompletedTask;
    }

    public Task OnBeforePushbackAsync()
    {
        RemoveGroundEquipment();
        return Task.CompletedTask;
    }

    public Task OnBeforeDeboardingAsync()
    {
        return PlaceGroundEquipmentAndChocks();
    }

    public void Dispose()
    {
        if (_sc == null) return;

        _sc.OnRecvEnumerateInputEvents -= HandleEnumerateInputEvents;
        _sc.OnRecvSubscribeInputEvent -= HandleSubscribeInputEvent;

        foreach (ulong hash in _nameToHash.Values)
        {
            try { _sc.UnsubscribeInputEvent(hash); }
            catch { }
        }

        _sc = null;
        Logger.Debug("IniA330Adapter: disposed");
    }

    private void WriteSimVar(SimDef def, double value)
    {
        if (_sc == null) return;
        try
        {
            _sc.SetDataOnSimObject(def, SimConnect.SIMCONNECT_OBJECT_ID_USER,
                SIMCONNECT_DATA_SET_FLAG.DEFAULT, new ScalarStruct { Value = value });
        }
        catch (Exception ex)
        {
            Logger.Warning($"IniA330Adapter: WriteSimVar({def}) = {value} failed: {ex.Message}");
        }
    }

    private bool IsDoorOpen(uint doorId)
    {
        string? name = DoorEventName(doorId);
        return name != null && _values.TryGetValue(name, out double v) && v > 0.5;
    }

    private static string? DoorEventName(uint doorId)
    {
        switch (doorId)
        {
            case A330Constants.DoorId1L: return A330Constants.IE_DOOR_1L;
            case A330Constants.DoorId1R: return A330Constants.IE_DOOR_1R;
            case A330Constants.DoorId2L: return A330Constants.IE_DOOR_2L;
            case A330Constants.DoorId2R: return A330Constants.IE_DOOR_2R;
            case A330Constants.DoorIdCargoFwd: return A330Constants.IE_CARGO_FWD;
            case A330Constants.DoorId3L: return A330Constants.IE_DOOR_3L;
            case A330Constants.DoorId3R: return A330Constants.IE_DOOR_3R;
            case A330Constants.DoorIdCargoAft: return A330Constants.IE_CARGO_AFT;
            default: return null;
        }
    }

    private void CloseExit(uint doorId)
    {
        if (_sc == null) return;
        try
        {
            // doorId values (1-4) match SET_AIRCRAFT_EXIT exit indices directly
            _sc.TransmitClientEvent_EX1(
                SimConnect.SIMCONNECT_OBJECT_ID_USER,
                EvtId.SetExit,
                Group.Default,
                SIMCONNECT_EVENT_FLAG.GROUPID_IS_PRIORITY,
                doorId, 0u, 0, 0, 0);
        }
        catch (Exception ex)
        {
            Logger.Warning($"IniA330Adapter: CloseExit({doorId}) failed: {ex.Message}");
        }
    }

    private static string GetDoorName(uint doorId)
    {
        switch (doorId)
        {
            case A330Constants.DoorId1L: return "Door 1L";
            case A330Constants.DoorId1R: return "Door 1R";
            case A330Constants.DoorId2L: return "Door 2L";
            case A330Constants.DoorId2R: return "Door 2R";
            case A330Constants.DoorIdCargoFwd: return "Cargo Fwd";
            case A330Constants.DoorId3L: return "Door 3L";
            case A330Constants.DoorId3R: return "Door 3R";
            case A330Constants.DoorIdCargoAft: return "Cargo Aft";
            default: return $"Door {doorId}";
        }
    }
}
