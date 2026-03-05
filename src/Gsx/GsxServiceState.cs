namespace SimpleGsxIntegrator.Gsx;

/// <summary>
/// Represents the current state of a GSX service as reported by its L:var.
/// Values match the integers written by GSX Pro to e.g. <c>L:FSDT_GSX_BOARDING_STATE</c>.
/// </summary>
public enum GsxServiceState
{
    Unknown = 0,

    Callable = 1,

    NotAvailable = 2,

    Bypassed = 3,

    Requested = 4,

    Active = 5,

    Completed = 6,
}
