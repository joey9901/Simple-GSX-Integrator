using Microsoft.FlightSimulator.SimConnect;

namespace SimpleGsxIntegrator.Aircraft;

public interface IAircraftAdapter : IDisposable
{
    void OnSimConnectConnected(SimConnect sc);

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
