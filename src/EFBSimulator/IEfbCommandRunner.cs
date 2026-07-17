namespace SimpleGsxIntegrator.Efb;

public interface IEfbCommandRunner
{
    Task RunAsync(string efbUrl, IReadOnlyList<EfbCommand> commands);

    /// <summary>Tears down the current browser session so the next RunAsync starts fresh. Call when switching aircraft.</summary>
    Task ResetAsync();
}
