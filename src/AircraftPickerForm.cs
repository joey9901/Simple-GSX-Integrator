using System.Windows.Forms;
using SimpleGsxIntegrator.Config;

namespace SimpleGsxIntegrator;

public sealed class AircraftPickerForm : Form
{
    private ListBox lstAircraft = null!;
    private Button btnOk = null!;
    private Button btnCancel = null!;

    private static readonly Color AccentColor = Color.FromArgb(0, 120, 212);
    private static readonly Font ItemFont = new Font("Segoe UI", 9.5f, FontStyle.Regular);
    private static readonly Font HeaderFont = new Font("Segoe UI", 10f, FontStyle.Bold);

    public string? SelectedTitle { get; private set; }

    public AircraftPickerForm(string? currentAircraftTitle)
    {
        InitializeComponent();
        PopulateList(currentAircraftTitle);
        ApplyTheme();
    }

    private void InitializeComponent()
    {
        this.Text = "Select Aircraft";
        this.ClientSize = new Size(480, 295);
        this.FormBorderStyle = FormBorderStyle.FixedDialog;
        this.MaximizeBox = false;
        this.MinimizeBox = false;
        this.StartPosition = FormStartPosition.CenterParent;

        try
        {
            string iconPath = Path.Combine(AppContext.BaseDirectory, "logo.ico");
            if (File.Exists(iconPath)) this.Icon = new Icon(iconPath);
        }
        catch { }

        var lblHeader = new Label
        {
            Text = "Select an aircraft to configure:",
            Location = new Point(20, 20),
            Size = new Size(440, 20),
            Font = HeaderFont
        };

        var listPanel = new Panel
        {
            Location = new Point(20, 50),
            Size = new Size(440, 200),
            BorderStyle = BorderStyle.FixedSingle
        };

        lstAircraft = new ListBox
        {
            Dock = DockStyle.Fill,
            DrawMode = DrawMode.OwnerDrawFixed,
            ItemHeight = 44,
            ScrollAlwaysVisible = false,
            IntegralHeight = false,
            BorderStyle = BorderStyle.None,
            SelectionMode = SelectionMode.One
        };
        lstAircraft.DrawItem += LstAircraft_DrawItem;
        lstAircraft.DoubleClick += (s, e) => AcceptSelection();

        listPanel.Controls.Add(lstAircraft);

        btnOk = new Button
        {
            Text = "OK",
            Location = new Point(280, 255),
            Size = new Size(85, 30),
            FlatStyle = FlatStyle.Flat,
            DialogResult = DialogResult.OK
        };
        btnOk.FlatAppearance.BorderColor = AccentColor;
        btnOk.Click += (s, e) => AcceptSelection();

        btnCancel = new Button
        {
            Text = "Cancel",
            Location = new Point(375, 255),
            Size = new Size(85, 30),
            FlatStyle = FlatStyle.Flat,
            DialogResult = DialogResult.Cancel
        };

        this.Controls.AddRange(new Control[]
        {
            lblHeader, listPanel, btnOk, btnCancel
        });

        this.AcceptButton = btnOk;
        this.CancelButton = btnCancel;
    }

    private void LstAircraft_DrawItem(object? sender, DrawItemEventArgs e)
    {
        if (e.Index < 0) return;

        bool selected = (e.State & DrawItemState.Selected) != 0;
        bool isDark = Theme.IsDarkMode;

        Color bg = selected
            ? (isDark ? Color.FromArgb(0, 84, 153) : Color.FromArgb(204, 228, 247))
            : (isDark ? Theme.Surface : Theme.Surface);
        Color fg = isDark ? Theme.Text : Theme.Text;

        using var bgBrush = new SolidBrush(bg);
        e.Graphics.FillRectangle(bgBrush, e.Bounds);

        if (selected)
        {
            using var accentBrush = new SolidBrush(AccentColor);
            e.Graphics.FillRectangle(accentBrush, new Rectangle(e.Bounds.X, e.Bounds.Y, 4, e.Bounds.Height));
        }

        using var sepPen = new Pen(isDark ? Theme.Border : Theme.Border);
        e.Graphics.DrawLine(sepPen,
            e.Bounds.Left, e.Bounds.Bottom - 1,
            e.Bounds.Right, e.Bounds.Bottom - 1);

        string text = lstAircraft.Items[e.Index]?.ToString() ?? string.Empty;
        int textX = e.Bounds.X + 16;
        float textY = e.Bounds.Y + (e.Bounds.Height - ItemFont.Height) / 2f;
        using var fgBrush = new SolidBrush(fg);
        e.Graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
        e.Graphics.DrawString(text, ItemFont, fgBrush, textX, textY);
    }

    private void PopulateList(string? currentTitle)
    {
        var saved = ConfigManager.GetSavedAircraftTitles().ToList();

        if (!string.IsNullOrEmpty(currentTitle) && !saved.Contains(currentTitle))
            saved.Insert(0, currentTitle);

        foreach (var title in saved)
            lstAircraft.Items.Add(title);

        if (!string.IsNullOrEmpty(currentTitle))
        {
            int idx = lstAircraft.Items.IndexOf(currentTitle);
            if (idx >= 0) lstAircraft.SelectedIndex = idx;
        }
        else if (lstAircraft.Items.Count > 0)
        {
            lstAircraft.SelectedIndex = 0;
        }
    }

    private void AcceptSelection()
    {
        if (lstAircraft.SelectedItem is string selected)
        {
            SelectedTitle = selected;
            this.DialogResult = DialogResult.OK;
            this.Close();
        }
        else
        {
            MessageBox.Show("Please select an aircraft.", "No Aircraft Selected",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private void ApplyTheme()
    {
        bool isDark = Theme.IsDarkMode;

        this.BackColor = isDark ? Theme.Background : SystemColors.Control;
        this.ForeColor = isDark ? Theme.Text : SystemColors.ControlText;

        foreach (Control c in this.Controls)
            ApplyThemeToControl(c);
    }

    private static void ApplyThemeToControl(Control c)
    {
        if (c is Label lbl)
        {
            lbl.ForeColor = Theme.IsDarkMode ? Theme.Text : SystemColors.ControlText;
        }
        else if (c is Panel pnl)
        {
            pnl.BackColor = Theme.IsDarkMode ? Theme.Border : Theme.Border;
            foreach (Control child in pnl.Controls)
                ApplyThemeToControl(child);
        }
        else if (c is ListBox lb)
        {
            lb.BackColor = Theme.Surface;
            lb.ForeColor = Theme.Text;
        }
        else if (c is Button btn)
        {
            btn.BackColor = Theme.ButtonBackground;
            btn.ForeColor = Theme.ButtonText;
        }
    }
}
