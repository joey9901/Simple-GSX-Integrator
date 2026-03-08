using Microsoft.FlightSimulator.SimConnect;
using SimpleGsxIntegrator.Core;

namespace SimpleGsxIntegrator.Aircraft;

/// <summary>
/// Aircraft-specific integration.
/// </summary>
public interface IAircraftAdapter : IDisposable
{
    /// <summary>Called once when SimConnect connects. Register all vars here.</summary>
    void OnSimConnectConnected(SimConnect sc);

    /// <summary>Called by the simconnectmanager for every <c>OnRecvSimobjectData</c> event.</summary>
    void OnSimObjectData(SIMCONNECT_RECV_SIMOBJECT_DATA data);

    /// <summary>Returns true if any door is currently open.</summary>
    bool AreAnyDoorsOpen();

    /// <summary>Returns the set of door IDs (PMDG event codes) that are open.</summary>
    IReadOnlySet<uint> GetOpenDoorIds();

    /// <summary>
    /// Sends a close command for each open door and awaits the inter-door delays.
    /// The caller should then poll <see cref="AreAnyDoorsOpen"/> to confirm closure.
    /// </summary>
    Task CloseAllOpenDoorsAsync();

    /// <summary>Sends a close command for the specified door ID.</summary>
    void CloseDoor(uint doorId);

    /// <summary>
    /// The door ID (event code) for the primary boarding door on this aircraft.
    /// Used by <see cref="SimpleGsxIntegrator.Automation.DoorManager"/> to close it after boarding completes.
    /// </summary>
    uint MainBoardingDoorId { get; }

    /// <summary>
    /// Removes ground equipment (GPU, chocks) specific to this aircraft.
    /// </summary>
    void RemoveGroundEquipment();

    /// <summary>
    /// Places ground equipment (GPU, chocks) specific to this aircraft.
    /// </summary>
    Task PlaceGroundEquipmentAndChocks();

    /// <summary>
    /// Prepares the aircraft for pushback: closes all open doors and removes ground equipment.
    /// Override for aircraft where the door or equipment sequence differs.
    /// </summary>
    async Task PrepareForPushbackAsync()
    {
        Logger.Info("Adapter: Removing Ground Equipment");
        RemoveGroundEquipment();
        await Task.Delay(2_000);

        await CloseAllOpenDoorsAsync();

        var deadline = DateTime.UtcNow.AddSeconds(60);
        while (AreAnyDoorsOpen() && DateTime.UtcNow < deadline)
            await Task.Delay(2_000);

        if (AreAnyDoorsOpen())
            Logger.Warning("Adapter: Doors still Open after 60 s - Proceeding with Pushback");
        else
            Logger.Info("Adapter: All Doors Confirmed Closed");
    }

    /// <summary>
    /// Prepares the aircraft for deboarding: places ground equipment and chocks.
    /// Override for aircraft with different arrival sequences.
    /// </summary>
    Task PrepareForDeboardingAsync() => PlaceGroundEquipmentAndChocks();
}
