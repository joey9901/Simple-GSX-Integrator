using System.Windows.Forms;

namespace SimpleGsxIntegrator;

public partial class MainForm : Form
{
    private Label lblSimConnectStatus = null!;
    private Label lblGsxStatus = null!;
    private Label lblSystemStatus = null!;
    private TextBox txtActivationKey = null!;
    private TextBox txtResetKey = null!;
    private TextBox txtToggleRefuelKey = null!;
    private RichTextBox txtLog = null!;
    private CheckBox chkRefuelEnabled = null!;
    private Label lblCurrentAircraft = null!;
    private Button btnPrintState = null!;
    private Button btnMovementDebug = null!;
    private Button btnToggleMovement = null!;
    
    private string _originalActivationKey = "";
    private string _originalResetKey = "";
    private string _originalToggleRefuelKey = "";
    private TextBox? _activeRebindTextBox = null;

    public MainForm()
    {
        InitializeComponent();
        this.Text = "Simple GSX Integrator";
        this.Size = new Size(700, 600);
        this.FormBorderStyle = FormBorderStyle.FixedSingle;
        this.MaximizeBox = false;
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

        var lblStatusHeader = new Label
        {
            Text = "Status",
            Font = new Font("Segoe UI", 12, FontStyle.Bold),
            Location = new Point(20, 65),
            Size = new Size(200, 25)
        };

        lblSimConnectStatus = new Label
        {
            Text = "● SimConnect: Disconnected",
            Location = new Point(40, 95),
            Size = new Size(300, 20),
            ForeColor = Color.Gray
        };

        lblGsxStatus = new Label
        {
            Text = "● GSX: Not Detected",
            Location = new Point(40, 120),
            Size = new Size(300, 20),
            ForeColor = Color.Gray
        };

        lblSystemStatus = new Label
        {
            Text = "● System: Inactive",
            Location = new Point(40, 145),
            Size = new Size(300, 20),
            ForeColor = Color.DarkOrange
        };

        var lblHotkeysHeader = new Label
        {
            Text = "Hotkeys",
            Font = new Font("Segoe UI", 12, FontStyle.Bold),
            Location = new Point(20, 180),
            Size = new Size(200, 25)
        };

        var lblActivationKey = new Label
        {
            Text = "Activation:",
            Location = new Point(40, 210),
            Size = new Size(100, 20)
        };

        txtActivationKey = new TextBox
        {
            Location = new Point(140, 208),
            Size = new Size(150, 20),
            ReadOnly = true,
            BackColor = SystemColors.Window,
            Cursor = Cursors.Hand,
            TabStop = false
        };
        txtActivationKey.Click += (s, e) => OnHotkeyTextBoxClick(txtActivationKey, _originalActivationKey, "Activation");
        txtActivationKey.KeyDown += (s, e) => OnHotkeyKeyDown(txtActivationKey, e, "Activation");

        var lblResetKey = new Label
        {
            Text = "Reset:",
            Location = new Point(40, 240),
            Size = new Size(100, 20)
        };

        txtResetKey = new TextBox
        {
            Location = new Point(140, 238),
            Size = new Size(150, 20),
            ReadOnly = true,
            BackColor = SystemColors.Window,
            Cursor = Cursors.Hand,
            TabStop = false
        };
        txtResetKey.Click += (s, e) => OnHotkeyTextBoxClick(txtResetKey, _originalResetKey, "Reset");
        txtResetKey.KeyDown += (s, e) => OnHotkeyKeyDown(txtResetKey, e, "Reset");

        var lblToggleRefuel = new Label
        {
            Text = "Toggle Refuel:",
            Location = new Point(40, 270),
            Size = new Size(100, 20)
        };

        txtToggleRefuelKey = new TextBox
        {
            Location = new Point(140, 268),
            Size = new Size(150, 20),
            ReadOnly = true,
            BackColor = SystemColors.Window,
            Cursor = Cursors.Hand,
            TabStop = false
        };
        txtToggleRefuelKey.Click += (s, e) => OnHotkeyTextBoxClick(txtToggleRefuelKey, _originalToggleRefuelKey, "ToggleRefuel");
        txtToggleRefuelKey.KeyDown += (s, e) => OnHotkeyKeyDown(txtToggleRefuelKey, e, "ToggleRefuel");

        var lblAircraftHeader = new Label
        {
            Text = "Current Aircraft",
            Font = new Font("Segoe UI", 12, FontStyle.Bold),
            Location = new Point(20, 310),
            Size = new Size(200, 25)
        };

        lblCurrentAircraft = new Label
        {
            Text = "None",
            Location = new Point(40, 340),
            Size = new Size(400, 20),
            ForeColor = Color.Gray,
            Font = new Font("Segoe UI", 10, FontStyle.Regular)
        };

        chkRefuelEnabled = new CheckBox
        {
            Text = "Enable automatic refueling for this aircraft",
            Location = new Point(40, 365),
            Size = new Size(350, 20)
        };
        chkRefuelEnabled.CheckedChanged += ChkRefuelEnabled_CheckedChanged;

        var lblLogHeader = new Label
        {
            Text = "Log",
            Font = new Font("Segoe UI", 12, FontStyle.Bold),
            Location = new Point(20, 400),
            Size = new Size(200, 25)
        };

        txtLog = new RichTextBox
        {
            Location = new Point(20, 430),
            Size = new Size(640, 110),
            ScrollBars = RichTextBoxScrollBars.Vertical,
            ReadOnly = true,
            BackColor = SystemColors.Window,
            Font = new Font("Consolas", 9)
        };

        var lblDebugHeader = new Label
        {
            Text = "Debug:",
            Font = new Font("Segoe UI", 12, FontStyle.Bold),
            Location = new Point(480, 145),
            Size = new Size(180, 25)
        };

        btnPrintState = new Button
        {
            Text = "Print State",
            Location = new Point(480, 180),
            Size = new Size(180, 30),
            BackColor = SystemColors.Control
        };
        btnPrintState.Click += (s, e) => Program.PrintCurrentState();

        btnMovementDebug = new Button
        {
            Text = "Print Movement Debug",
            Location = new Point(480, 220),
            Size = new Size(180, 30),
            BackColor = SystemColors.Control
        };
        btnMovementDebug.Click += (s, e) => Program.PrintMovementDebug();

        btnToggleMovement = new Button
        {
            Text = "Toggle HasMoved Flag",
            Location = new Point(480, 260),
            Size = new Size(180, 30),
            BackColor = SystemColors.Control
        };
        btnToggleMovement.Click += (s, e) => Program.ToggleMovementFlag();

        this.Controls.AddRange(new Control[]
        {
            lblTitle,
            lblStatusHeader, lblSimConnectStatus, lblGsxStatus, lblSystemStatus,
            lblHotkeysHeader, lblActivationKey, txtActivationKey,
            lblResetKey, txtResetKey, lblToggleRefuel, txtToggleRefuelKey,
            lblAircraftHeader, lblCurrentAircraft, chkRefuelEnabled,
            lblLogHeader, txtLog,
            lblDebugHeader, btnPrintState, btnMovementDebug, btnToggleMovement
        });

        this.ResumeLayout();
    }

    private void ChkRefuelEnabled_CheckedChanged(object? sender, EventArgs e)
    {
        if (!string.IsNullOrEmpty(lblCurrentAircraft.Text) && lblCurrentAircraft.Text != "None")
        {
            Program.UpdateRefuelSetting(lblCurrentAircraft.Text, chkRefuelEnabled.Checked);
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

    public void SetHotkeys(string activation, string reset, string toggleRefuel)
    {
        if (InvokeRequired)
        {
            Invoke(() => SetHotkeys(activation, reset, toggleRefuel));
            return;
        }

        txtActivationKey.Text = activation;
        txtResetKey.Text = reset;
        txtToggleRefuelKey.Text = toggleRefuel;
        
        _originalActivationKey = activation;
        _originalResetKey = reset;
        _originalToggleRefuelKey = toggleRefuel;
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
        else if (hotkeyType == "ToggleRefuel")
            _originalToggleRefuelKey = textBox.Text;
        
        _activeRebindTextBox = textBox;
        textBox.ReadOnly = false;
        textBox.TabStop = true;
        textBox.Text = "Press key combination...";
        textBox.ForeColor = Color.Gray;
        textBox.SelectAll();
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
            else if (hotkeyType == "ToggleRefuel")
                textBox.Text = _originalToggleRefuelKey;
                
            EndRebindMode(textBox, false);
            return;
        }
        
        List<string> parts = new List<string>();
        if (e.Control) parts.Add("CTRL");
        if (e.Alt) parts.Add("ALT");
        if (e.Shift) parts.Add("SHIFT");
        
        if (e.KeyCode != Keys.ControlKey && e.KeyCode != Keys.ShiftKey && 
            e.KeyCode != Keys.Menu && e.KeyCode != Keys.LWin && e.KeyCode != Keys.RWin)
        {
            parts.Add(e.KeyCode.ToString());
        }
        
        if (parts.Count == 0 || (parts.Count == 1 && (parts[0] == "CTRL" || parts[0] == "ALT" || parts[0] == "SHIFT")))
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
        textBox.ForeColor = SystemColors.WindowText;
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
        lblCurrentAircraft.ForeColor = Color.Black;
        chkRefuelEnabled.Checked = refuelEnabled;
    }

    public void UpdateRefuelCheckbox(bool enabled)
    {
        if (InvokeRequired)
        {
            Invoke(() => UpdateRefuelCheckbox(enabled));
            return;
        }

        chkRefuelEnabled.Checked = enabled;
    }

    public void AppendLog(string message)
    {
        if (InvokeRequired)
        {
            Invoke(() => AppendLog(message));
            return;
        }

        Color textColor = SystemColors.WindowText; // Default black
        
        if (message.Contains("[OK]"))
        {
            textColor = Color.FromArgb(0, 128, 0); // Green
        }
        else if (message.Contains("[WARN]"))
        {
            textColor = Color.FromArgb(255, 140, 0); // Orange
        }
        else if (message.Contains("[ERROR]"))
        {
            textColor = Color.Red;
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
        txtLog.ScrollToCaret();
    }
}
