using Microsoft.FlightSimulator.SimConnect;

namespace SimpleGsxIntegrator.Aircraft;

public abstract class AircraftAdapterBase : IAircraftAdapter
{
    // Aircraft capabilities — defined by the adapter class, never modified at runtime.
    public virtual bool canRemoveAndPlaceGroundEquipment => false;
    public virtual bool canRemoveCovers => false;
    public virtual bool canManageDoors => false;

    // User preferences — loaded from config when the aircraft is identified.
    public bool removeCovers { get; set; } = false;
    public bool manageGroundEquipment { get; set; } = false;
    public bool manageDoors { get; set; } = false;

    public abstract string DisplayName { get; }
    public abstract string[] TitleKeywords { get; }

    public virtual string parkingBrakeVariable => "BRAKE PARKING INDICATOR";
    public virtual string beaconLightVariable => "LIGHT BEACON";
    public virtual string engine1RunningVariable => "GENERAL ENG COMBUSTION:1";
    public virtual string engine2RunningVariable => "GENERAL ENG COMBUSTION:2";
    public virtual string engine3RunningVariable => "GENERAL ENG COMBUSTION:3";
    public virtual string engine4RunningVariable => "GENERAL ENG COMBUSTION:4";

    public virtual bool? ChocksSet => null;
    public virtual bool? GpuConnected => null;
    public virtual int? OpenDoorCount => null;
    public event Action? GroundStateChanged;
    protected void NotifyGroundStateChanged() => GroundStateChanged?.Invoke();

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