using Microsoft.FlightSimulator.SimConnect;

namespace SimpleGsxIntegrator.Aircraft;

/// <summary>
/// Aircraft-specific integration. Implement this interface to add support for a new aircraft.
/// Override only the hooks relevant to your aircraft — all hooks are no-ops by default.
/// </summary>
public interface IAircraftAdapter : IDisposable
{
    /// <summary>Called once when SimConnect connects. Register all vars here.</summary>
    void OnSimConnectConnected(SimConnect sc);

    /// <summary>Called by the SimConnect manager for every <c>OnRecvSimobjectData</c> event.</summary>
    void OnSimObjectData(SIMCONNECT_RECV_SIMOBJECT_DATA data);

    async Task OnBeforePushbackAsync() { await Task.Delay(2_000); }
    Task OnBeforeDeboardingAsync() { return Task.CompletedTask; }

    Task OnRefuelingRequested() { return Task.CompletedTask; }
    Task OnRefuelingCompleted() { return Task.CompletedTask; }

    Task OnCateringRequested() { return Task.CompletedTask; }
    Task OnCateringCompleted() { return Task.CompletedTask; }

    Task OnBoardingRequested() { return Task.CompletedTask; }
    Task OnBoardingCompleted() { return Task.CompletedTask; }

    Task OnDeboardingRequested() { return Task.CompletedTask; }
    Task OnDeboardingCompleted() { return Task.CompletedTask; }

    Task OnPushbackRequested() { return Task.CompletedTask; }
    Task OnPushbackCompleted() { return Task.CompletedTask; }
}
