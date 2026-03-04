namespace SimpleGsxIntegrator.Gsx;

/// <summary>
/// Represents the current state of a GSX service as reported by its L:var.
/// Values match the integers written by GSX Pro to e.g. <c>L:FSDT_GSX_BOARDING_STATE</c>.
/// </summary>
public enum GsxServiceState
{
    /// <summary>State not yet read or indeterminate.</summary>
    Unknown = 0,

    /// <summary>Service is available and can be called.</summary>
    Callable = 1,

    /// <summary>Service is not available at the current position/gate.</summary>
    NotAvailable = 2,

    /// <summary>Service was skipped / bypassed.</summary>
    Bypassed = 3,

    /// <summary>Service has been requested; truck is en route.</summary>
    Requested = 4,

    /// <summary>Service is actively running.</summary>
    Active = 5,

    /// <summary>Service has completed successfully.</summary>
    Completed = 6,
}
