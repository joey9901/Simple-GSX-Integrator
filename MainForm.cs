using System.Windows.Forms;
using System.Runtime.InteropServices;

namespace SimpleGsxIntegrator;

public partial class MainForm : Form
{
    private Label lblSimConnectStatus = null!;
    private Label lblGsxStatus = null!;
    private Label lblSystemStatus = null!;
    private Label lblVersion = null!;
    private TextBox txtActivationKey = null!;
    private TextBox txtResetKey = null!;
    private RichTextBox txtLog = null!;
    private Button btnAircraftConfig = null!;
    private Label lblCurrentAircraft = null!;
    private Label lblAircraftStateBeacon = null!;
    private Label lblAircraftStateBrake = null!;
    private Label lblAircraftStateEngines = null!;
    private Label lblAircraftStateHasMoved = null!;
    private Label lblAircraftStateSpeed = null!;
    private Button btnPrintState = null!;
    private Button btnMovementDebug = null!;
    private Button btnToggleMovement = null!;
    private CheckBox chkDarkMode = null!;
    private Panel pnlUpdateAvailable = null!;
    private Label lblUpdateMessage = null!;
    private Button btnDownloadUpdate = null!;
    private ProgressBar prgUpdateProgress = null!;

    private string _originalActivationKey = "";
    private string _originalResetKey = "";
    private TextBox? _activeRebindTextBox = null;

    private const int VK_MENU = 0x12;
    private const int VK_CONTROL = 0x11;
    private const int VK_SHIFT = 0x10;

    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int vKey);

    public MainForm()
    {
        var config = ConfigManager.GetConfig();
        Theme.IsDarkMode = config.UI.DarkMode;

        InitializeComponent();
        this.Text = "Simple GSX Integrator";
        this.ClientSize = new Size(700, 645);
        this.MinimumSize = new Size(700, 645);
        this.FormBorderStyle = FormBorderStyle.Sizable;
        this.MaximizeBox = true;
        this.StartPosition = FormStartPosition.CenterScreen;

        try
        {
            string iconPath = Path.Combine(AppContext.BaseDirectory, "logo.ico");
            if (File.Exists(iconPath))
            {
                this.Icon = new Icon(iconPath);
            }
        }
        catch { }
    }

    private void InitializeComponent()
    {
        this.SuspendLayout();

        var lblTitle = new Label
        {
            Text = "Simple GSX Integrator",
            Font = new Font("Segoe UI", 16, FontStyle.Bold),
            Location = new Point(20, 20),
            Size = new Size(300, 35),
            AutoSize = false
        };

        lblVersion = new Label
        {
            Text = $"v{UpdateChecker.GetCurrentVersion()}",
            Font = new Font("Segoe UI", 11),
            Location = new Point(330, 27),
            AutoSize = true,
            ForeColor = Theme.IsDarkMode ? Color.FromArgb(200, 200, 200) : Color.FromArgb(100, 100, 100)
        };

        chkDarkMode = new CheckBox
        {
            Text = "Dark Mode",
            Location = new Point(560, 25),
            Size = new Size(100, 25),
            Checked = Theme.IsDarkMode
        };
        chkDarkMode.CheckedChanged += ChkDarkMode_CheckedChanged;

        pnlUpdateAvailable = new Panel
        {
            Location = new Point(20, 60),
            Size = new Size(640, 40),
            BackColor = Color.FromArgb(255, 243, 205),
            BorderStyle = BorderStyle.FixedSingle,
            Visible = false
        };

        lblUpdateMessage = new Label
        {
            Text = "Update available! v0.0.0",
            Location = new Point(10, 11),
            Size = new Size(400, 20),
            Font = new Font("Segoe UI", 10, FontStyle.Bold),
            ForeColor = Color.FromArgb(133, 100, 4)
        };

        btnDownloadUpdate = new Button
        {
            Text = "Download",
            Location = new Point(540, 8),
            Size = new Size(90, 25),
            BackColor = Color.FromArgb(255, 193, 7),
            ForeColor = Color.Black,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 9, FontStyle.Bold)
        };
        btnDownloadUpdate.FlatAppearance.BorderColor = Color.FromArgb(200, 150, 0);
        btnDownloadUpdate.Click += BtnDownloadUpdate_Click;

        prgUpdateProgress = new ProgressBar
        {
            Location = new Point(420, 11),
            Size = new Size(210, 20),
            Visible = false
        };

        pnlUpdateAvailable.Controls.AddRange(new Control[] { lblUpdateMessage, btnDownloadUpdate, prgUpdateProgress });

        var lblStatusHeader = new Label
        {
            Text = "Status",
            Font = new Font("Segoe UI", 12, FontStyle.Bold),
            Location = new Point(20, 110),
            Size = new Size(200, 25)
        };

        lblSimConnectStatus = new Label
        {
            Text = "● SimConnect: Disconnected",
            Location = new Point(40, 140),
            Size = new Size(300, 20),
            ForeColor = Color.Gray
        };

        lblGsxStatus = new Label
        {
            Text = "● GSX: Not Detected",
            Location = new Point(40, 165),
            Size = new Size(300, 20),
            ForeColor = Color.Gray
        };

        lblSystemStatus = new Label
        {
            Text = "● System: Inactive",
            Location = new Point(40, 190),
            Size = new Size(300, 20),
            ForeColor = Color.DarkOrange
        };

        var lblHotkeysHeader = new Label
        {
            Text = "Hotkeys",
            Font = new Font("Segoe UI", 12, FontStyle.Bold),
            Location = new Point(20, 225),
            Size = new Size(200, 25)
        };

        var lblActivationKey = new Label
        {
            Text = "Activation:",
            Location = new Point(40, 265),
            Size = new Size(100, 20)
        };

        txtActivationKey = new TextBox
        {
            Location = new Point(140, 263),
            Size = new Size(150, 20),
            ReadOnly = true,
            BackColor = SystemColors.Window,
            Cursor = Cursors.Hand,
            TabStop = false
        };
        txtActivationKey.Click += (s, e) => OnHotkeyTextBoxClick(txtActivationKey, _originalActivationKey, "Activation");
        txtActivationKey.PreviewKeyDown += (s, e) => OnHotkeyPreviewKeyDown(e);
        txtActivationKey.KeyDown += (s, e) => OnHotkeyKeyDown(txtActivationKey, e, "Activation");

        var lblResetKey = new Label
        {
            Text = "Reset:",
            Location = new Point(40, 295),
            Size = new Size(100, 20)
        };

        txtResetKey = new TextBox
        {
            Location = new Point(140, 293),
            Size = new Size(150, 20),
            ReadOnly = true,
            BackColor = SystemColors.Window,
            Cursor = Cursors.Hand,
            TabStop = false
        };
        txtResetKey.Click += (s, e) => OnHotkeyTextBoxClick(txtResetKey, _originalResetKey, "Reset");
        txtResetKey.PreviewKeyDown += (s, e) => OnHotkeyPreviewKeyDown(e);
        txtResetKey.KeyDown += (s, e) => OnHotkeyKeyDown(txtResetKey, e, "Reset");

        var lblAircraftHeader = new Label
        {
            Text = "Current Aircraft",
            Font = new Font("Segoe UI", 12, FontStyle.Bold),
            Location = new Point(20, 365),
            Size = new Size(200, 25)
        };

        lblCurrentAircraft = new Label
        {
            Text = "None",
            Location = new Point(40, 395),
            Size = new Size(300, 20),
            ForeColor = Color.Gray,
            Font = new Font("Segoe UI", 10, FontStyle.Regular)
        };

        btnAircraftConfig = new Button
        {
            Text = "Configure Aircraft Settings",
            Location = new Point(40, 420),
            Size = new Size(200, 25),
            Enabled = false
        };
        btnAircraftConfig.Click += BtnAircraftConfig_Click;

        var lblLogHeader = new Label
        {
            Text = "Log",
            Font = new Font("Segoe UI", 12, FontStyle.Bold),
            Location = new Point(20, 455),
            Size = new Size(200, 25)
        };

        txtLog = new RichTextBox
        {
            Location = new Point(20, 485),
            Size = new Size(660, 150),
            ScrollBars = RichTextBoxScrollBars.Vertical,
            ReadOnly = true,
            BackColor = SystemColors.Window,
            Font = new Font("Consolas", 9),
            Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
            WordWrap = true,
            DetectUrls = false,
            Multiline = true
        };

        var lblAircraftStateHeader = new Label
        {
            Text = "Aircraft State",
            Font = new Font("Segoe UI", 12, FontStyle.Bold),
            Location = new Point(480, 110),
            Size = new Size(200, 25)
        };

        lblAircraftStateBeacon = new Label
        {
            Text = "Beacon Light: -",
            Location = new Point(480, 140),
            Size = new Size(200, 16),
            Font = new Font("Segoe UI", 9, FontStyle.Regular),
            ForeColor = Color.Gray,
            AutoSize = false
        };

        lblAircraftStateBrake = new Label
        {
            Text = "Parking Brake: -",
            Location = new Point(480, 160),
            Size = new Size(200, 16),
            Font = new Font("Segoe UI", 9, FontStyle.Regular),
            ForeColor = Color.Gray,
            AutoSize = false
        };

        lblAircraftStateEngines = new Label
        {
            Text = "Engines Running: -",
            Location = new Point(480, 180),
            Size = new Size(200, 16),
            Font = new Font("Segoe UI", 9, FontStyle.Regular),
            ForeColor = Color.Gray,
            AutoSize = false
        };

        lblAircraftStateHasMoved = new Label
        {
            Text = "Has Moved: -",
            Location = new Point(480, 200),
            Size = new Size(200, 16),
            Font = new Font("Segoe UI", 9, FontStyle.Regular),
            ForeColor = Color.Gray,
            AutoSize = false
        };

        lblAircraftStateSpeed = new Label
        {
            Text = "Speed: -",
            Location = new Point(480, 220),
            Size = new Size(200, 16),
            Font = new Font("Segoe UI", 9, FontStyle.Regular),
            ForeColor = Theme.Text,
            AutoSize = false
        };

        var lblDebugHeader = new Label
        {
            Text = "Debug",
            Font = new Font("Segoe UI", 12, FontStyle.Bold),
            Location = new Point(480, 280),
            Size = new Size(180, 25)
        };

        btnPrintState = new Button
        {
            Text = "Print State",
            Location = new Point(480, 310),
            Size = new Size(180, 30),
            BackColor = SystemColors.Control
        };
        btnPrintState.Click += (s, e) => Program.PrintCurrentState();

        btnMovementDebug = new Button
        {
            Text = "Print Movement Debug",
            Location = new Point(480, 350),
            Size = new Size(180, 30),
            BackColor = SystemColors.Control
        };
        btnMovementDebug.Click += (s, e) => Program.PrintMovementDebug();

        btnToggleMovement = new Button
        {
            Text = "Toggle HasMoved Flag",
            Location = new Point(480, 390),
            Size = new Size(180, 30),
            BackColor = SystemColors.Control
        };
        btnToggleMovement.Click += (s, e) => Program.ToggleMovementFlag();

        this.Controls.AddRange(new Control[]
        {
            lblTitle, lblVersion, chkDarkMode, pnlUpdateAvailable,
            lblStatusHeader, lblSimConnectStatus, lblGsxStatus, lblSystemStatus,
            lblHotkeysHeader, lblActivationKey, txtActivationKey,
            lblResetKey, txtResetKey,
            lblAircraftHeader, lblCurrentAircraft, btnAircraftConfig,
            lblLogHeader, txtLog,
            lblAircraftStateHeader, lblAircraftStateBeacon, lblAircraftStateBrake, lblAircraftStateEngines, lblAircraftStateHasMoved, lblAircraftStateSpeed,
            lblDebugHeader, btnPrintState, btnMovementDebug, btnToggleMovement
        });

        this.ResumeLayout();

        txtLog.Width = this.ClientSize.Width - 40;
        txtLog.Height = this.ClientSize.Height - txtLog.Top - 20;

        ApplyTheme();

        Task.Run(async () => await CheckForUpdatesAsync());
    }

    private void BtnAircraftConfig_Click(object? sender, EventArgs e)
    {
        if (!string.IsNullOrEmpty(lblCurrentAircraft.Text) && lblCurrentAircraft.Text != "None")
        {
            var configForm = new AircraftConfigForm(lblCurrentAircraft.Text);
            configForm.ShowDialog(this);
        }
    }

    private void ChkDarkMode_CheckedChanged(object? sender, EventArgs e)
    {
        Theme.IsDarkMode = chkDarkMode.Checked;
        ApplyTheme();

        var config = ConfigManager.GetConfig();
        config.UI.DarkMode = Theme.IsDarkMode;
        ConfigManager.Save(config);
    }

    private void ApplyTheme()
    {
        this.BackColor = Theme.Background;

        ApplyThemeToControl(this);
    }

    private void ApplyThemeToControl(Control control)
    {
        if (control is Label label)
        {
            if (label == lblSimConnectStatus || label == lblGsxStatus || label == lblSystemStatus)
            {
                label.BackColor = Theme.Background;
            }
            else if (label == lblVersion)
            {
                label.ForeColor = Theme.IsDarkMode ? Color.FromArgb(200, 200, 200) : Color.FromArgb(100, 100, 100);
                label.BackColor = Theme.Background;
            }
            else
            {
                label.ForeColor = Theme.Text;
                label.BackColor = Theme.Background;
            }
        }
        else if (control is TextBox textBox)
        {
            textBox.BackColor = Theme.Surface;
            textBox.ForeColor = Theme.Text;
        }
        else if (control is RichTextBox richTextBox)
        {
            richTextBox.BackColor = Theme.Surface;
        }
        else if (control is Button button)
        {
            button.BackColor = Theme.ButtonBackground;
            button.ForeColor = Theme.ButtonText;
            button.FlatStyle = FlatStyle.Flat;
            button.FlatAppearance.BorderColor = Theme.Border;
        }
        else if (control is CheckBox checkBox)
        {
            checkBox.ForeColor = Theme.Text;
            checkBox.BackColor = Theme.Background;
        }

        foreach (Control child in control.Controls)
        {
            ApplyThemeToControl(child);
        }
    }

    public void SetSimConnectStatus(bool connected)
    {
        if (InvokeRequired)
        {
            Invoke(() => SetSimConnectStatus(connected));
            return;
        }

        lblSimConnectStatus.Text = connected ? "● SimConnect: Connected" : "● SimConnect: Disconnected";
        lblSimConnectStatus.ForeColor = connected ? Color.LimeGreen : Color.Gray;
    }

    public void SetAircraftStateDetails(string title, bool beaconOn, bool parkingBrakeSet, bool enginesRunning, bool hasMoved, double speed)
    {
        if (InvokeRequired)
        {
            Invoke(() => SetAircraftStateDetails(title, beaconOn, parkingBrakeSet, enginesRunning, hasMoved, speed));
            return;
        }

        lblAircraftStateBeacon.Text = $"Beacon Light: {(beaconOn ? "ON" : "OFF")}";
        lblAircraftStateBeacon.ForeColor = beaconOn ? Color.LimeGreen : Color.Red;

        lblAircraftStateBrake.Text = $"Parking Brake: {(parkingBrakeSet ? "SET" : "RELEASED")}";
        lblAircraftStateBrake.ForeColor = parkingBrakeSet ? Color.LimeGreen : Color.Red;

        lblAircraftStateEngines.Text = $"Engines Running: {(enginesRunning ? "YES" : "NO")}";
        lblAircraftStateEngines.ForeColor = enginesRunning ? Color.LimeGreen : Color.Red;

        lblAircraftStateHasMoved.Text = $"Has Moved: {(hasMoved ? "YES" : "NO")}";
        lblAircraftStateHasMoved.ForeColor = hasMoved ? Color.Red : Color.LimeGreen;

        lblAircraftStateSpeed.Text = $"Speed: {speed:F1} kts";
        lblAircraftStateSpeed.ForeColor = Theme.Text;
    }

    public void SetGsxStatus(bool detected)
    {
        if (InvokeRequired)
        {
            Invoke(() => SetGsxStatus(detected));
            return;
        }

        lblGsxStatus.Text = detected ? "● GSX: Running" : "● GSX: Not Detected";
        lblGsxStatus.ForeColor = detected ? Color.LimeGreen : Color.Gray;
    }

    public void SetSystemStatus(bool active)
    {
        if (InvokeRequired)
        {
            Invoke(() => SetSystemStatus(active));
            return;
        }

        lblSystemStatus.Text = active ? "● System: Active" : "● System: Inactive";
        lblSystemStatus.ForeColor = active ? Color.LimeGreen : Color.DarkOrange;
    }

    public void SetHotkeys(string activation, string reset)
    {
        if (InvokeRequired)
        {
            Invoke(() => SetHotkeys(activation, reset));
            return;
        }

        txtActivationKey.Text = activation;
        txtResetKey.Text = reset;

        _originalActivationKey = activation;
        _originalResetKey = reset;
    }

    private void OnHotkeyTextBoxClick(TextBox textBox, string originalValue, string hotkeyType)
    {
        Program.SetRebindingMode(true);

        if (_activeRebindTextBox != null && _activeRebindTextBox != textBox)
        {
            EndRebindMode(_activeRebindTextBox, false);
        }

        if (hotkeyType == "Activation")
            _originalActivationKey = textBox.Text;
        else if (hotkeyType == "Reset")
            _originalResetKey = textBox.Text;

        _activeRebindTextBox = textBox;
        textBox.ReadOnly = false;
        textBox.TabStop = true;
        textBox.Text = "Press key combination...";
        textBox.ForeColor = Color.Gray;
        textBox.SelectAll();
    }

    private void OnHotkeyPreviewKeyDown(PreviewKeyDownEventArgs e)
    {
        e.IsInputKey = true;
    }

    private void OnHotkeyKeyDown(TextBox textBox, KeyEventArgs e, string hotkeyType)
    {
        if (_activeRebindTextBox != textBox)
            return;

        e.SuppressKeyPress = true;
        e.Handled = true;

        if (e.KeyCode == Keys.Escape)
        {
            if (hotkeyType == "Activation")
                textBox.Text = _originalActivationKey;
            else if (hotkeyType == "Reset")
                textBox.Text = _originalResetKey;

            EndRebindMode(textBox, false);
            return;
        }

        System.Threading.Thread.Sleep(10);

        List<string> parts = new List<string>();

        bool ctrlPressed = e.Control || (GetAsyncKeyState(VK_CONTROL) & 0x8000) != 0;
        bool altPressed = e.Alt || (GetAsyncKeyState(VK_MENU) & 0x8000) != 0;
        bool shiftPressed = e.Shift || (GetAsyncKeyState(VK_SHIFT) & 0x8000) != 0;

        if (ctrlPressed) parts.Add("CTRL");
        if (altPressed) parts.Add("ALT");
        if (shiftPressed) parts.Add("SHIFT");

        if (e.KeyCode != Keys.ControlKey && e.KeyCode != Keys.ShiftKey &&
            e.KeyCode != Keys.Menu && e.KeyCode != Keys.Alt &&
            e.KeyCode != Keys.LWin && e.KeyCode != Keys.RWin &&
            e.KeyCode != Keys.LControlKey && e.KeyCode != Keys.RControlKey &&
            e.KeyCode != Keys.LShiftKey && e.KeyCode != Keys.RShiftKey)
        {
            parts.Add(e.KeyCode.ToString());
        }

        if (parts.Count == 0 || parts.All(p => p == "CTRL" || p == "ALT" || p == "SHIFT"))
        {
            return;
        }

        string hotkeyString = string.Join("+", parts);
        textBox.Text = hotkeyString;

        EndRebindMode(textBox, true);
        Program.UpdateHotkey(hotkeyType, hotkeyString);
    }

    private void EndRebindMode(TextBox textBox, bool success)
    {
        textBox.ReadOnly = true;
        textBox.TabStop = false;
        textBox.ForeColor = Theme.Text;
        _activeRebindTextBox = null;
        Program.SetRebindingMode(false);
    }

    public void SetCurrentAircraft(string aircraft, bool refuelEnabled)
    {
        if (InvokeRequired)
        {
            Invoke(() => SetCurrentAircraft(aircraft, refuelEnabled));
            return;
        }

        lblCurrentAircraft.Text = aircraft;
        lblCurrentAircraft.ForeColor = Theme.Text;
        btnAircraftConfig.Enabled = !string.IsNullOrEmpty(aircraft) && aircraft != "None";
    }

    public void AppendLog(string message)
    {
        if (InvokeRequired)
        {
            Invoke(() => AppendLog(message));
            return;
        }

        Color textColor = Theme.Text; // Use theme text color as default

        if (message.Contains("[OK]"))
        {
            textColor = Theme.IsDarkMode ? Color.FromArgb(0, 200, 0) : Color.FromArgb(0, 128, 0);
        }
        else if (message.Contains("[WARN]"))
        {
            textColor = Theme.IsDarkMode ? Color.FromArgb(255, 180, 0) : Color.FromArgb(255, 140, 0);
        }
        else if (message.Contains("[ERROR]"))
        {
            textColor = Theme.IsDarkMode ? Color.FromArgb(255, 100, 100) : Color.Red;
        }
        else if (message.Contains("[INFO]"))
        {
            textColor = Color.FromArgb(0, 102, 204); // Blue
        }

        txtLog.SelectionStart = txtLog.TextLength;
        txtLog.SelectionLength = 0;
        txtLog.SelectionColor = textColor;
        txtLog.AppendText(message + Environment.NewLine);
        txtLog.SelectionColor = txtLog.ForeColor;

        txtLog.SelectionStart = txtLog.TextLength;
        txtLog.ScrollToCaret();
    }

    private async Task CheckForUpdatesAsync()
    {
        try
        {
            Logger.Debug($"Checking for updates... Current version: {UpdateChecker.GetCurrentVersion()}");
            var updateInfo = await UpdateChecker.CheckForUpdatesAsync();
            Logger.Debug($"Update check result: {(updateInfo != null ? $"Update available: {updateInfo.LatestVersion}" : "No update available")}");

            if (updateInfo != null)
            {
                if (InvokeRequired)
                {
                    Invoke(() => ShowUpdateNotification(updateInfo));
                }
                else
                {
                    ShowUpdateNotification(updateInfo);
                }
            }
        }
        catch (Exception ex)
        {
            Logger.Debug($"Update check error: {ex.Message}");
        }
    }

    private void ShowUpdateNotification(UpdateInfo updateInfo)
    {
        lblUpdateMessage.Text = $"Update available! v{updateInfo.LatestVersion}";
        lblUpdateMessage.Tag = updateInfo.DownloadUrl;
        pnlUpdateAvailable.Visible = true;

        if (Theme.IsDarkMode)
        {
            pnlUpdateAvailable.BackColor = Color.FromArgb(70, 60, 20);
            lblUpdateMessage.ForeColor = Color.FromArgb(255, 220, 130);
            btnDownloadUpdate.BackColor = Color.FromArgb(180, 140, 20);
        }
        else
        {
            pnlUpdateAvailable.BackColor = Color.FromArgb(255, 243, 205);
            lblUpdateMessage.ForeColor = Color.FromArgb(133, 100, 4);
            btnDownloadUpdate.BackColor = Color.FromArgb(255, 193, 7);
        }

        Logger.Info($"Update available: v{updateInfo.LatestVersion}");
    }

    private async void BtnDownloadUpdate_Click(object? sender, EventArgs e)
    {
        string? url = lblUpdateMessage.Tag as string;
        if (!string.IsNullOrEmpty(url))
        {
            try
            {
                btnDownloadUpdate.Enabled = false;
                btnDownloadUpdate.Visible = false;
                prgUpdateProgress.Visible = true;
                prgUpdateProgress.Value = 0;
                lblUpdateMessage.Text = "Downloading update...";

                Logger.Info("Starting update download...");

                var progress = new Progress<int>(percent =>
                {
                    if (InvokeRequired)
                    {
                        Invoke(() =>
                        {
                            prgUpdateProgress.Value = percent;
                            lblUpdateMessage.Text = $"Downloading update... {percent}%";
                        });
                    }
                    else
                    {
                        prgUpdateProgress.Value = percent;
                        lblUpdateMessage.Text = $"Downloading update... {percent}%";
                    }
                });

                var zipPath = await UpdateChecker.DownloadUpdateAsync(url, progress);

                if (zipPath != null)
                {
                    lblUpdateMessage.Text = "Installing update...";
                    Logger.Info("Download complete, installing...");

                    await Task.Run(() => UpdateChecker.InstallUpdateAndRestart(zipPath));
                }
                else
                {
                    lblUpdateMessage.Text = "Download failed!";
                    Logger.Warning("Update download failed");

                    btnDownloadUpdate.Enabled = true;
                    btnDownloadUpdate.Visible = true;
                    prgUpdateProgress.Visible = false;
                }
            }
            catch (Exception ex)
            {
                Logger.Warning($"Update failed: {ex.Message}");
                lblUpdateMessage.Text = "Update failed!";
                btnDownloadUpdate.Enabled = true;
                btnDownloadUpdate.Visible = true;
                prgUpdateProgress.Visible = false;
            }
        }
    }
}
