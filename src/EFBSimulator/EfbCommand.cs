namespace SimpleGsxIntegrator.Efb;

public abstract record EfbCommand;

public sealed record NavigateTo(string Selector) : EfbCommand;

public sealed record ClickElement(string Selector) : EfbCommand;

public sealed record DispatchClick(string Selector) : EfbCommand;

public sealed record SetCheckbox(string Selector, bool Checked) : EfbCommand;