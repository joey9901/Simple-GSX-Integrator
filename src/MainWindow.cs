using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;
using System.Text.Json;
using System.Net;
using System.Runtime.InteropServices;

namespace SimpleGsxIntegrator;

public class MainWindow : Form
{
    private WebView2 _webView = null!;
    private HttpListener? _httpListener;
    private readonly string _uiPath = Path.Combine(AppContext.BaseDirectory, "ui");

    private string? _pendingConfigTitle;
    private string? _pendingUpdateUrl;

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        int val = 1;
        DwmSetWindowAttribute(Handle, 20, ref val, sizeof(int));
    }

    public MainWindow()
    {
        Text = "Simple GSX Integrator";
        Size = new Size(1000, 700);
        StartPosition = FormStartPosition.CenterScreen;
        Icon = TryLoadIcon();

        _webView = new WebView2 { Dock = DockStyle.Fill };
        Controls.Add(_webView);

        Load += OnLoad;
        FormClosing += OnFormClosing;
    }


    private async void OnLoad(object? sender, EventArgs e)
    {
        try
        {
            var env = await CoreWebView2Environment.CreateAsync(
                userDataFolder: Path.Combine(AppContext.BaseDirectory, ".webview-data"));
            await _webView.EnsureCoreWebView2Async(env);

            _webView.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;
            _webView.CoreWebView2.Settings.IsStatusBarEnabled = false;
            _webView.CoreWebView2.Settings.IsGeneralAutofillEnabled = false;
            _webView.CoreWebView2.Settings.IsPasswordAutosaveEnabled = false;

            _webView.CoreWebView2.NavigationCompleted += OnNavigationCompleted;
            _webView.CoreWebView2.WebMessageReceived += OnWebMessageReceived;

            StartHttpServer();
            _webView.CoreWebView2.Navigate("http://localhost:12345/index.html");
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to initialize UI: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            Application.Exit();
        }
    }

    private void StartHttpServer()
    {
        _httpListener = new HttpListener();
        _httpListener.Prefixes.Add("http://localhost:12345/");
        _httpListener.Start();
        _ = Task.Run(HttpServerLoop);
    }

    private async Task HttpServerLoop()
    {
        while (_httpListener?.IsListening == true)
        {
            try
            {
                var context = await _httpListener.GetContextAsync();
                var response = context.Response;
                response.AddHeader("Cache-Control", "no-cache");

                var urlPath = context.Request.Url?.LocalPath.TrimStart('/') ?? "index.html";
                if (urlPath == "") urlPath = "index.html";

                var fullPath = Path.Combine(_uiPath, urlPath);
                if (File.Exists(fullPath))
                {
                    response.ContentType = Path.GetExtension(fullPath).ToLower() switch
                    {
                        ".html" => "text/html",
                        ".css" => "text/css",
                        ".js" => "application/javascript",
                        _ => "application/octet-stream"
                    };
                    var bytes = File.ReadAllBytes(fullPath);
                    response.ContentLength64 = bytes.Length;
                    response.OutputStream.Write(bytes, 0, bytes.Length);
                }
                else
                {
                    response.StatusCode = 404;
                }
                response.OutputStream.Close();
            }
            catch { }
        }
    }

    public void SendMessage(object data)
    {
        try
        {
            _webView.CoreWebView2.PostWebMessageAsString(JsonSerializer.Serialize(data));
        }
        catch { }
    }

    private void OnNavigationCompleted(object? sender, CoreWebView2NavigationCompletedEventArgs e)
    {
        if (!e.IsSuccess) return;
        var cfg = Config.ConfigManager.GetConfig();
        SendMessage(new { type = "simconnect", connected = Program.IsSimConnectConnected });
        SendMessage(new { type = "gsx", running = Program.IsGsxRunning });
        SendMessage(new { type = "system", active = Program.IsSystemActive });
        SendMessage(new { type = "aircraft", title = string.IsNullOrEmpty(Program.CurrentAircraftTitle) ? (string?)null : Program.CurrentAircraftTitle });
        Program.RefreshDisplayState();
        Program.RefreshGroundEquipState();
        Program.RefreshServiceStates();
        SendMessage(new { type = "hotkeys", activation = cfg.Hotkeys.ActivationKey, reset = cfg.Hotkeys.ResetKey });
        _ = Task.Run(RunUpdateCheckAsync);
    }

    private async Task RunUpdateCheckAsync()
    {
        var info = await UpdateChecker.CheckForUpdatesAsync();
        if (info == null) return;
        _pendingUpdateUrl = info.DownloadUrl;
        Invoke(() => SendMessage(new { type = "update", version = info.LatestVersion, url = info.DownloadUrl }));
    }

    private async Task RunDownloadUpdateAsync()
    {
        if (_pendingUpdateUrl == null) return;
        var url = _pendingUpdateUrl;
        var progress = new Progress<int>(pct => SendMessage(new { type = "updateProgress", value = pct }));
        var path = await UpdateChecker.DownloadUpdateAsync(url, progress);
        if (path != null)
            UpdateChecker.InstallUpdateAndRestart(path);
    }

    private void OnWebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
    {
        var json = e.TryGetWebMessageAsString();
        if (json == null) return;
        try
        {
            var msg = JsonSerializer.Deserialize<JsonElement>(json);
            if (!msg.TryGetProperty("type", out var typeEl)) return;

            switch (typeEl.GetString())
            {
                case "openConfig":
                    OpenAircraftSettings();
                    break;

                case "pickerSelected":
                    if (msg.TryGetProperty("title", out var pickedEl))
                        ShowConfigModal(pickedEl.GetString() ?? "");
                    break;

                case "pickerCancelled":
                    SendMessage(new { type = "hidePicker" });
                    break;

                case "saveConfig":
                    if (msg.TryGetProperty("config", out var cfgEl) && _pendingConfigTitle != null)
                    {
                        var opts = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                        var aircraftCfg = JsonSerializer.Deserialize<Config.AircraftConfig>(cfgEl.GetRawText(), opts)
                            ?? new Config.AircraftConfig();
                        Config.ConfigManager.SaveAircraftConfig(_pendingConfigTitle, aircraftCfg);
                        Program.ApplyAdapterConfig(_pendingConfigTitle);
                        Program.RegisterActivationForCurrentAircraft();
                        _pendingConfigTitle = null;
                    }
                    SendMessage(new { type = "hideConfig" });
                    break;

                case "cancelConfig":
                    _pendingConfigTitle = null;
                    SendMessage(new { type = "hideConfig" });
                    break;

                case "downloadUpdate":
                    _ = Task.Run(RunDownloadUpdateAsync);
                    break;

                case "toggleHasMoved":
                    Program.ToggleMovementFlag();
                    break;

                case "toggleEnginesRan":
                    Program.ToggleEnginesEverRunFlag();
                    break;

                case "rebindStart":
                    Program.SetRebindingMode(true);
                    break;

                case "rebindCancel":
                    Program.SetRebindingMode(false);
                    break;

                case "hotkeyCaptured":
                    if (msg.TryGetProperty("key", out var keyEl) && msg.TryGetProperty("value", out var valEl))
                    {
                        Program.SetRebindingMode(false);
                        Program.UpdateHotkey(keyEl.GetString() ?? "", valEl.GetString() ?? "");
                    }
                    break;
            }
        }
        catch { }
    }

    private void OpenAircraftSettings()
    {
        var current = Program.CurrentAircraftTitle;
        var saved = Config.ConfigManager.GetSavedAircraftTitles().ToList();

        bool needsPicker = saved.Count > 1
            || (saved.Count == 1 && saved[0] != current)
            || string.IsNullOrEmpty(current);

        if (needsPicker)
        {
            ShowPickerModal(saved, current);
        }
        else
        {
            ShowConfigModal(current ?? saved.FirstOrDefault() ?? "");
        }
    }

    private void ShowPickerModal(List<string> saved, string? current)
    {
        var withFamily = new Dictionary<string, List<string>>();
        var withoutFamily = new List<string>();

        foreach (var title in saved)
        {
            var family = Aircraft.AircraftAdapterMatcher.FindByTitle(title)?.DisplayName;
            if (family != null)
            {
                if (!withFamily.ContainsKey(family)) withFamily[family] = new();
                withFamily[family].Add(title);
            }
            else withoutFamily.Add(title);
        }

        SendMessage(new
        {
            type = "showPicker",
            withFamily = withFamily.OrderBy(kv => kv.Key)
                               .Select(kv => new { family = kv.Key, titles = kv.Value.OrderBy(x => x).ToList() })
                               .ToList(),
            withoutFamily = withoutFamily.OrderBy(x => x).ToList(),
            currentTitle = current
        });
    }

    private void ShowConfigModal(string title)
    {
        if (string.IsNullOrEmpty(title)) return;
        _pendingConfigTitle = title;

        var meta = Aircraft.AircraftAdapterMatcher.FindByTitle(title);
        var cfg = Config.ConfigManager.GetAircraftConfig(title);

        SendMessage(new
        {
            type = "showConfig",
            title,
            caps = new { canManageGroundEquipment = meta?.canRemoveAndPlaceGroundEquipment ?? false, canRemoveCovers = meta?.canRemoveCovers ?? false, canManageDoors = meta?.canManageDoors ?? false },
            config = new
            {
                refuelBeforeBoarding = cfg.RefuelBeforeBoarding,
                cateringOnNewFlight = cfg.CateringOnNewFlight,
                realisticCrewComms = cfg.RealisticCrewComms,
                disableRemoteControl = cfg.DisableRemoteControl,
                removeCovers = cfg.RemoveCovers,
                manageGroundEquipment = cfg.ManageGroundEquipment,
                manageDoors = cfg.ManageDoors,
                activationLvar = cfg.ActivationLvar,
                activationValue = cfg.ActivationValue
            }
        });
    }

    private void OnFormClosing(object? sender, FormClosingEventArgs e)
    {
        _httpListener?.Stop();
        _httpListener?.Close();
    }

    private Icon? TryLoadIcon()
    {
        try
        {
            var ico = Path.Combine(AppContext.BaseDirectory, "logo.ico");
            if (File.Exists(ico)) return new Icon(ico);
        }
        catch { }
        return null;
    }
}
