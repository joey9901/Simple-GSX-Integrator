namespace SimpleGsxIntegrator.Efb;

public abstract record EfbCommand;

/// <summary>Best-effort click; missing elements are skipped, not an error (handles modded EFBs).</summary>
public sealed record NavigateTo(string Selector) : EfbCommand;

public sealed record ClickElement(string Selector) : EfbCommand;

/// <summary>Dispatches a synthetic MouseEvent instead of a native click - required for SVG controls.</summary>
public sealed record DispatchClick(string Selector) : EfbCommand;

/// <summary>Clicks only if the checkbox's current state differs from the desired state.</summary>
public sealed record SetCheckbox(string Selector, bool Checked) : EfbCommand;
