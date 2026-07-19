namespace SimpleGsxIntegrator.Aircraft;

public interface IGroundEquipment
{
    bool? GpuConnected { get; }
    bool? ChocksSet { get; }
    Task SetGpu(bool connected);
    Task SetChocks(bool placed);
}

public interface IEngineCovers
{
    Task RemoveCovers();
}

public interface IClosableDoors
{
    bool AnyDoorOpen { get; }
    int OpenDoorCount { get; }
    Task CloseOpenDoors();
}

public interface IArmableDoors
{
    Task ArmAllDoors();
}

public interface ICargoDoor
{
    bool? DoorOpen { get; }
    Task OpenCargoDoor();
    Task CloseCargoDoor();
}

public interface IEfbRunner
{
    Task PreloadEfb();
    Task DisposeEfb();
}
