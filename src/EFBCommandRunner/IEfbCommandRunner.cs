namespace SimpleGsxIntegrator.Efb;

public interface IEfbCommandRunner
{
    Task RunAsync(string efbUrl, IReadOnlyList<EfbCommand> commands);

    Task ResetAsync();
}
