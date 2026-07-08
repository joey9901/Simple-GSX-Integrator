using Microsoft.FlightSimulator.SimConnect;

namespace SimpleGsxIntegrator.Aircraft;

public abstract class AircraftAdapterBase : IAircraftAdapter
{
    public virtual bool CanRemoveAndPlaceGroundEquipment { get; set; } = false;
    public virtual bool canRemoveCovers { get; set; } = false;

    public virtual bool removeCovers { get; set; } = false;

    public virtual string ParkingBrakeVariable => "BRAKE PARKING INDICATOR";
    public virtual string BeaconLightVariable => "LIGHT BEACON";
    public virtual string Engine1RunningVariable => "GENERAL ENG COMBUSTION:1";
    public virtual string Engine2RunningVariable => "GENERAL ENG COMBUSTION:2";
    public virtual string Engine3RunningVariable => "GENERAL ENG COMBUSTION:3";
    public virtual string Engine4RunningVariable => "GENERAL ENG COMBUSTION:4";

    public virtual void OnSimConnectConnected(SimConnect sc) { }

    public virtual void OnSimObjectData(SIMCONNECT_RECV_SIMOBJECT_DATA data) { }

    public virtual Task OnSpawned()
        => Task.CompletedTask;

    public virtual Task OnBeforePushback()
        => Task.CompletedTask;

    public virtual Task OnBeforeDeboarding()
        => Task.CompletedTask;

    public virtual Task OnRefuelingRequested()
        => Task.CompletedTask;

    public virtual Task OnRefuelingCompleted()
        => Task.CompletedTask;

    public virtual Task OnCateringRequested()
        => Task.CompletedTask;

    public virtual Task OnCateringCompleted()
        => Task.CompletedTask;

    public virtual Task OnBoardingRequested()
        => Task.CompletedTask;

    public virtual Task OnBoardingActive()
        => Task.CompletedTask;

    public virtual Task OnBoardingCompleted()
        => Task.CompletedTask;

    public virtual Task OnDeboardingRequested()
        => Task.CompletedTask;

    public virtual Task OnDeboardingActive()
        => Task.CompletedTask;

    public virtual Task OnDeboardingCompleted()
        => Task.CompletedTask;

    public virtual Task OnPushbackRequested()
        => Task.CompletedTask;

    public virtual Task OnPushbackCompleted()
        => Task.CompletedTask;

    public virtual void Dispose() { }
}