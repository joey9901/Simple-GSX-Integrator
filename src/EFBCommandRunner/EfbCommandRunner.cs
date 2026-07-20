using PuppeteerSharp;

namespace SimpleGsxIntegrator.Efb;

/// <summary>Drives an aircraft's EFB web page in a headless browser to work around L:vars that revert on write.
/// Shared across every aircraft that needs EFB automation - the URL is supplied per call, not fixed at construction.</summary>
public sealed class EfbCommandRunner : IEfbCommandRunner, IAsyncDisposable
{
    // Debug aid: flip to true and rebuild to watch the automation happen in a visible browser window.
    private const bool ShowBrowserWindow = false;

    private readonly SemaphoreSlim _lock = new(1, 1);
    private IBrowser? _browser;
    private IPage? _page;
    private string? _currentUrl;

    public async Task RunAsync(string efbUrl, IReadOnlyList<EfbCommand> commands)
    {
        await _lock.WaitAsync();
        try
        {
            var page = await GetPageAsync(efbUrl);
            foreach (var command in commands)
                await ExecuteAsync(page, command);
        }
        finally
        {
            _lock.Release();
        }
    }

    private async Task<IPage> GetPageAsync(string efbUrl)
    {
        if (_page == null)
        {
            await new BrowserFetcher().DownloadAsync();
            _browser = await Puppeteer.LaunchAsync(new LaunchOptions
            {
                Headless = !ShowBrowserWindow,
                Args = new[] { "--disable-gpu", "--no-sandbox" }
            });
            _page = await _browser.NewPageAsync();
        }

        if (_currentUrl != efbUrl)
        {
            await _page.GoToAsync(efbUrl, new NavigationOptions
            {
                WaitUntil = new[] { WaitUntilNavigation.Networkidle2 },
                Timeout = 15_000 // 15 second timeout to prevent indefinite hangs
            });
            _currentUrl = efbUrl;
        }

        return _page;
    }

    private static async Task ExecuteAsync(IPage page, EfbCommand command)
    {
        try
        {
            switch (command)
            {
                case NavigateTo cmd:
                    {
                        var found = await JsClickAsync(page, cmd.Selector);
                        if (!found) Logger.Debug($"EfbCommandRunner: '{cmd.Selector}' not found (may be a modded EFB)");
                        break;
                    }
                case ClickElement cmd:
                    {
                        var found = await JsClickAsync(page, cmd.Selector);
                        if (!found) Logger.Warning($"EfbCommandRunner: '{cmd.Selector}' not found");
                        break;
                    }
                case DispatchClick cmd:
                    await DispatchClickAsync(page, cmd.Selector);
                    break;
                case SetCheckbox cmd:
                    await SetCheckboxAsync(page, cmd.Selector, cmd.Checked);
                    break;
            }

            await Task.Delay(1000);
        }
        catch (Exception ex)
        {
            Logger.Warning($"EfbCommandRunner: {command} failed ({ex.Message})");
        }
    }

    private static Task<bool> JsClickAsync(IPage page, string selector) => page.EvaluateFunctionAsync<bool>(@"
        (sel) => {
            const el = document.querySelector(sel);
            if (!el) return false;
            el.click();
            return true;
        }", selector);

    private static Task DispatchClickAsync(IPage page, string selector) => page.EvaluateFunctionAsync(@"
        (sel) => {
            const el = document.querySelector(sel);
            if (el) el.dispatchEvent(new MouseEvent('click', { bubbles: true, cancelable: true, view: window }));
        }", selector);

    private static Task SetCheckboxAsync(IPage page, string selector, bool shouldCheck) => page.EvaluateFunctionAsync(@"
        (sel, shouldCheck) => {
            const el = document.querySelector(sel);
            if (el && el.checked !== shouldCheck) el.click();
        }", selector, shouldCheck);

    private static Task SetToggleByClassAsync(IPage page, string selector, string activeClass, bool shouldBeActive) => page.EvaluateFunctionAsync(@"
        (sel, activeClass, shouldBeActive) => {
            const el = document.querySelector(sel);
            if (el && el.classList.contains(activeClass) !== shouldBeActive) el.click();
        }", selector, activeClass, shouldBeActive);

    public async Task ResetAsync()
    {
        await _lock.WaitAsync();
        try
        {
            if (_browser != null)
                await _browser.CloseAsync();
            _browser = null;
            _page = null;
            _currentUrl = null;
        }
        finally
        {
            _lock.Release();
        }
    }

    public ValueTask DisposeAsync() => new(ResetAsync());
}
