using Microsoft.FlightSimulator.SimConnect;

namespace SimpleGsxIntegrator.Aircraft;

public interface IAircraftAdapter : IDisposable
{
    void OnSimConnectConnected(SimConnect sc);

    void OnSimObjectData(SIMCONNECT_RECV_SIMOBJECT_DATA data);

    Task OnSpawned();

    Task OnBeforePushback();
    Task OnBeforeDeboarding();

    Task OnRefuelingRequested();
    Task OnRefuelingCompleted();

    Task OnCateringRequested();
    Task OnCateringCompleted();

    Task OnBoardingRequested();
    Task OnBoardingActive();
    Task OnBoardingCompleted();

    Task OnDeboardingRequested();
    Task OnDeboardingActive();
    Task OnDeboardingCompleted();

    Task OnPushbackRequested();
    Task OnPushbackCompleted();
}