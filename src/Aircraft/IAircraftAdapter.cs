using Microsoft.FlightSimulator.SimConnect;
using SimpleGsxIntegrator.Core;

namespace SimpleGsxIntegrator.Aircraft;

/// <summary>
/// Aircraft-specific integration contract.
///
/// Each adapter registers its own SimConnect vars (via the SimConnect instance
/// received in <see cref="OnSimConnectConnected"/>) and processes data via
/// <see cref="OnSimObjectData"/>.  All actual SimConnect communication routes
/// through the <see cref="SimConnectHub"/> – adapters never hold their own
/// connection or run their own message pump.
/// </summary>
public interface IAircraftAdapter : IDisposable
{
    /// <summary>Called once when SimConnect connects. Register all vars here.</summary>
    void OnSimConnectConnected(SimConnect sc);

    /// <summary>Called by the hub for every <c>OnRecvSimobjectData</c> event.</summary>
    void OnSimObjectData(SIMCONNECT_RECV_SIMOBJECT_DATA data);


    /// <summary>Returns true if any passenger or cargo door is currently open.</summary>
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
    /// Removes ground equipment (GPU, chocks) specific to this aircraft.
    /// </summary>
    void RemoveGroundEquipment();
}
