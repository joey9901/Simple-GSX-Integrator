using System.Windows.Forms;

namespace SimpleGsxIntegrator;

public class AircraftConfigForm : Form
{
    private readonly string _aircraftTitle;
    private CheckBox chkRefuelBeforeBoarding = null!;
    private CheckBox chkCateringOnNewFlight = null!;
    private CheckBox chkCateringOnTurnaround = null!;
    private CheckBox chkAutoCallTurnaroundServices = null!;
    private NumericUpDown nudTurnaroundDelay = null!;
    private Button btnSave = null!;
    private Button btnCancel = null!;

    public AircraftConfigForm(string aircraftTitle)
    {
        _aircraftTitle = aircraftTitle;
        InitializeComponent();
        LoadCurrentSettings();
    }

    private void InitializeComponent()
    {
        this.Text = "Aircraft Configuration";
        this.ClientSize = new Size(450, 340);
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
            Text = $"Settings for: {_aircraftTitle}",
            Location = new Point(20, 20),
            Size = new Size(410, 25),
            Font = new Font("Segoe UI", 12, FontStyle.Bold),
            AutoSize = false
        };

        var lblDepartureHeader = new Label
        {
            Text = "New Flight Settings",
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

        var lblTurnaroundHeader = new Label
        {
            Text = "Turnaround Settings",
            Location = new Point(20, 145),
            Size = new Size(250, 20),
            Font = new Font("Segoe UI", 10, FontStyle.Bold)
        };

        chkAutoCallTurnaroundServices = new CheckBox
        {
            Text = "Automatically call Turnaround Services (Uncheck for one-way trips)",
            Location = new Point(40, 170),
            Size = new Size(400, 20)
        };
        chkAutoCallTurnaroundServices.CheckedChanged += OnAutoCallTurnaroundChanged;

        chkCateringOnTurnaround = new CheckBox
        {
            Text = "Enable Catering on Turnaround",
            Location = new Point(40, 195),
            Size = new Size(380, 20)
        };

        var lblTurnaroundDelay = new Label
        {
            Text = "Delay before Turnaround Services (seconds):",
            Location = new Point(40, 225),
            Size = new Size(280, 20)
        };

        nudTurnaroundDelay = new NumericUpDown
        {
            Location = new Point(330, 223),
            Size = new Size(80, 23),
            Minimum = 0,
            Maximum = 300,
            Value = 120
        };

        btnSave = new Button
        {
            Text = "Save",
            Location = new Point(260, 295),
            Size = new Size(85, 30)
        };
        btnSave.Click += BtnSave_Click;

        btnCancel = new Button
        {
            Text = "Cancel",
            Location = new Point(355, 295),
            Size = new Size(85, 30)
        };
        btnCancel.Click += (s, e) => this.Close();

        this.Controls.AddRange(new Control[]
        {
            lblTitle,
            lblDepartureHeader,
            chkRefuelBeforeBoarding,
            chkCateringOnNewFlight,
            lblTurnaroundHeader,
            chkCateringOnTurnaround,
            chkAutoCallTurnaroundServices,
            lblTurnaroundDelay,
            nudTurnaroundDelay,
            btnSave,
            btnCancel
        });

        ApplyTheme();
    }

    private void LoadCurrentSettings()
    {
        var config = ConfigManager.GetAircraftConfig(_aircraftTitle);
        chkRefuelBeforeBoarding.Checked = config.RefuelBeforeBoarding;
        chkCateringOnNewFlight.Checked = config.CateringOnNewFlight;
        chkAutoCallTurnaroundServices.Checked = config.AutoCallTurnaroundServices;
        chkCateringOnTurnaround.Checked = config.CateringOnTurnaround;
        chkCateringOnTurnaround.Enabled = config.AutoCallTurnaroundServices;
        nudTurnaroundDelay.Value = config.TurnaroundDelaySeconds;
    }
    
    private void OnAutoCallTurnaroundChanged(object? sender, EventArgs e)
    {
        chkCateringOnTurnaround.Enabled = chkAutoCallTurnaroundServices.Checked;
        if (!chkAutoCallTurnaroundServices.Checked)
        {
            chkCateringOnTurnaround.Checked = false;
        }
    }

    private void BtnSave_Click(object? sender, EventArgs e)
    {
        var config = ConfigManager.GetAircraftConfig(_aircraftTitle);
        config.RefuelBeforeBoarding = chkRefuelBeforeBoarding.Checked;
        config.CateringOnNewFlight = chkCateringOnNewFlight.Checked;
        config.CateringOnTurnaround = chkCateringOnTurnaround.Checked;
        config.AutoCallTurnaroundServices = chkAutoCallTurnaroundServices.Checked;
        config.TurnaroundDelaySeconds = (int)nudTurnaroundDelay.Value;
        
        ConfigManager.SaveAircraftConfig(_aircraftTitle, config);
        
        Logger.Info($"Aircraft configuration saved for '{_aircraftTitle}'");
        this.Close();
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
