using System.Windows.Forms;
using SimpleGsxIntegrator.Config;

namespace SimpleGsxIntegrator;

public class AircraftConfigForm : Form
{
    private readonly string _aircraftTitle;
    private CheckBox chkRefuelBeforeBoarding = null!;
    private CheckBox chkCateringOnNewFlight = null!;
    private CheckBox chkRealisticCrewComms = null!;
    private PictureBox pbInfoRealisticComms = null!;
    private ToolTip _infoTip = null!;
    private TextBox txtActivationLvar = null!;
    private NumericUpDown nudActivationValue = null!;
    private Button btnApplyTo = null!;
    private Button btnSave = null!;
    private Button btnCancel = null!;

    public AircraftConfigForm(string aircraftTitle)
    {
        _aircraftTitle = aircraftTitle;
        InitializeComponent();
        LoadCurrentSettings();
        ApplyTheme();
    }

    private void InitializeComponent()
    {
        this.Text = "Aircraft Configuration";
        this.ClientSize = new Size(500, 290);
        this.FormBorderStyle = FormBorderStyle.FixedDialog;
        this.MaximizeBox = false;
        this.MinimizeBox = false;
        this.StartPosition = FormStartPosition.CenterParent;

        try
        {
            string iconPath = Path.Combine(AppContext.BaseDirectory, "logo.ico");
            if (File.Exists(iconPath))
            {
                this.Icon = new Icon(iconPath);
            }
        }
        catch { }

        var lblTitle = new Label
        {
            Text = $"{_aircraftTitle}",
            Location = new Point(20, 20),
            Size = new Size(410, 25),
            Font = new Font("Segoe UI", 12, FontStyle.Bold),
            AutoSize = false
        };

        var lblDepartureHeader = new Label
        {
            Text = "Service Options",
            Location = new Point(20, 60),
            Size = new Size(200, 20),
            Font = new Font("Segoe UI", 10, FontStyle.Bold)
        };

        chkRefuelBeforeBoarding = new CheckBox
        {
            Text = "Enable Refueling before Boarding",
            Location = new Point(40, 85),
            Size = new Size(380, 20)
        };

        chkCateringOnNewFlight = new CheckBox
        {
            Text = "Enable Catering before Boarding",
            Location = new Point(40, 110),
            Size = new Size(380, 20)
        };

        chkRealisticCrewComms = new CheckBox
        {
            Text = "Realistic Crew Communications",
            Location = new Point(40, 135),
            AutoSize = true
        };

        // Measure text width so we can sit the icon right next to the checkbox label
        int checkboxTextWidth;
        using (var g = Graphics.FromHwnd(IntPtr.Zero))
        using (var f = new Font("Segoe UI", 9f))
            checkboxTextWidth = (int)g.MeasureString(chkRealisticCrewComms.Text, f).Width;

        // 20 = checkbox glyph width, 4 = gap
        int iconX = 40 + 20 + checkboxTextWidth + 4;

        var infoIcon = new Bitmap(SystemIcons.Information.ToBitmap(), new Size(16, 16));
        pbInfoRealisticComms = new PictureBox
        {
            Image = infoIcon,
            Location = new Point(iconX, 137),
            Size = new Size(16, 16),
            SizeMode = PictureBoxSizeMode.StretchImage,
            Cursor = Cursors.Help
        };

        _infoTip = new ToolTip { InitialDelay = 200, ReshowDelay = 100 };
        _infoTip.SetToolTip(pbInfoRealisticComms,
            "When enabled, the system automatically de-activates after boarding is requested.\n" +
            "You must re-activate via your configured L:var or keybind before calling pushback,\n" +
            "slightly mimicking real ground crew radio communications.\n" +
            "Example:\n" +
            "  First Activation calls Boarding / Refueling / Catering depending on your settings\n" +
            "  System de-activates when Boarding is called\n" +
            "  System must be re-activated to call Pushback\n" +
            "  System remains activate after Pushback for automatic Deboarding call\n" +
            "  System de-activates after Deboarding is called.");

        var lblActivationHeader = new Label
        {
            Text = "System Activation (Set Custom L:var as System Activation Trigger)",
            Location = new Point(20, 170),
            Size = new Size(500, 20),
            Font = new Font("Segoe UI", 10, FontStyle.Bold)
        };

        var lblActivationL = new Label
        {
            Text = "L:",
            Location = new Point(40, 195),
            Size = new Size(18, 23),
            TextAlign = ContentAlignment.MiddleLeft
        };

        txtActivationLvar = new TextBox
        {
            Location = new Point(62, 195),
            Size = new Size(260, 23)
        };

        var lblActivationValue = new Label
        {
            Text = "Activation Value:",
            Location = new Point(330, 195),
            Size = new Size(100, 23)
        };

        nudActivationValue = new NumericUpDown
        {
            Location = new Point(430, 195),
            Size = new Size(60, 23),
            Minimum = 0,
            Maximum = 1000000,
            DecimalPlaces = 2
        };

        btnApplyTo = new Button
        {
            Text = "Apply to...",
            Location = new Point(20, 235),
            Size = new Size(100, 30)
        };
        btnApplyTo.Click += BtnApplyTo_Click;

        btnSave = new Button
        {
            Text = "Save",
            Location = new Point(310, 235),
            Size = new Size(85, 30)
        };
        btnSave.Click += ButtonSaveClick;

        btnCancel = new Button
        {
            Text = "Cancel",
            Location = new Point(405, 235),
            Size = new Size(85, 30)
        };
        btnCancel.Click += BtnCancel_Click;

        this.Controls.AddRange(new Control[]
        {
            lblTitle,
            lblDepartureHeader,
            chkRefuelBeforeBoarding,
            chkCateringOnNewFlight,
            chkRealisticCrewComms,
            pbInfoRealisticComms,
            lblActivationHeader,
            lblActivationL,
            txtActivationLvar,
            lblActivationValue,
            nudActivationValue,
            btnApplyTo,
            btnSave,
            btnCancel
        });
    }

    private void LoadCurrentSettings()
    {
        var config = ConfigManager.GetAircraftConfig(_aircraftTitle);
        chkRefuelBeforeBoarding.Checked = config.RefuelBeforeBoarding;
        chkCateringOnNewFlight.Checked = config.CateringOnNewFlight;
        chkRealisticCrewComms.Checked = config.RealisticCrewComms;
        txtActivationLvar.Text = config.ActivationLvar?.Replace("L:", "") ?? string.Empty;
        try { nudActivationValue.Value = (decimal)config.ActivationValue; } catch { nudActivationValue.Value = 1; }
    }

    private void BtnCancel_Click(object? sender, EventArgs e)
    {
        this.Close();
    }

    private void ButtonSaveClick(object? sender, EventArgs e)
    {
        var config = ConfigManager.GetAircraftConfig(_aircraftTitle);
        config.RefuelBeforeBoarding = chkRefuelBeforeBoarding.Checked;
        config.CateringOnNewFlight = chkCateringOnNewFlight.Checked;
        config.RealisticCrewComms = chkRealisticCrewComms.Checked;
        config.ActivationLvar = txtActivationLvar.Text?.Trim() ?? string.Empty;
        config.ActivationValue = (double)nudActivationValue.Value;

        ConfigManager.SaveAircraftConfig(_aircraftTitle, config);

        Program.RegisterActivationForCurrentAircraft();

        this.Close();
    }

    private void BtnApplyTo_Click(object? sender, EventArgs e)
    {
        using var picker = new AircraftPickerForm(_aircraftTitle, multiSelect: true);
        if (picker.ShowDialog(this) != DialogResult.OK) return;

        var targets = picker.SelectedTitles;
        if (targets.Count == 0) return;

        var template = new AircraftConfig
        {
            RefuelBeforeBoarding = chkRefuelBeforeBoarding.Checked,
            CateringOnNewFlight = chkCateringOnNewFlight.Checked,
            RealisticCrewComms = chkRealisticCrewComms.Checked,
            ActivationLvar = txtActivationLvar.Text?.Trim() ?? string.Empty,
            ActivationValue = (double)nudActivationValue.Value,
        };

        foreach (var title in targets)
        {
            var cfg = ConfigManager.GetAircraftConfig(title);
            cfg.RefuelBeforeBoarding = template.RefuelBeforeBoarding;
            cfg.CateringOnNewFlight = template.CateringOnNewFlight;
            cfg.RealisticCrewComms = template.RealisticCrewComms;
            cfg.ActivationLvar = template.ActivationLvar;
            cfg.ActivationValue = template.ActivationValue;
            ConfigManager.SaveAircraftConfig(title, cfg);
        }

        MessageBox.Show(
            $"Settings applied to {targets.Count} aircraft.",
            "Applied",
            MessageBoxButtons.OK,
            MessageBoxIcon.Information);
    }

    private void ApplyTheme()
    {
        if (Theme.IsDarkMode)
        {
            this.BackColor = Theme.Background;
            this.ForeColor = Theme.Text;

            foreach (Control control in this.Controls)
            {
                ApplyThemeToControl(control);
            }
        }
        else
        {
            this.BackColor = SystemColors.Control;
            this.ForeColor = SystemColors.ControlText;

            foreach (Control control in this.Controls)
            {
                if (control is Label)
                {
                    control.ForeColor = SystemColors.ControlText;
                }
                else if (control is CheckBox)
                {
                    control.BackColor = SystemColors.Control;
                    control.ForeColor = SystemColors.ControlText;
                }
                else if (control is Button)
                {
                    control.BackColor = SystemColors.Control;
                    control.ForeColor = SystemColors.ControlText;
                }
                else if (control is NumericUpDown)
                {
                    control.BackColor = SystemColors.Window;
                    control.ForeColor = SystemColors.WindowText;
                }
            }
        }
    }

    private void ApplyThemeToControl(Control control)
    {
        if (control is Label)
        {
            control.ForeColor = Theme.Text;
        }
        else if (control is CheckBox)
        {
            control.BackColor = Theme.Background;
            control.ForeColor = Theme.Text;
        }
        else if (control is Button)
        {
            control.BackColor = Theme.ButtonBackground;
            control.ForeColor = Theme.ButtonText;
        }
        else if (control is NumericUpDown)
        {
            control.BackColor = Theme.Background;
            control.ForeColor = Theme.Text;
        }
    }
}
