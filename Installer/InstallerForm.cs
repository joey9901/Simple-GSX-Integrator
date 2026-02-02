using System.Diagnostics;
using System.Xml.Linq;

namespace SimpleGSXIntegrator.Installer;

public class InstallerForm : Form
{
    private const string AppName = "Simple GSX Integrator";
    private const string AppExeName = "SimpleGSXIntegrator.exe";
    private const string ExeXmlFileName = "exe.xml";
    private const string InstallInfoFileName = "install-info.txt";

    private Panel contentPanel = null!;
    private Button backButton = null!;
    private Button nextButton = null!;
    private Button cancelButton = null!;
    
    private int currentStep = 0;
    private string? detectedMsfsPath;
    private string selectedInstallPath = "";
    private bool isUninstallMode = false;

    public InstallerForm()
    {
        InitializeComponent();
        DetectMsfsAsync();
        ShowStep(0);
    }

    private void InitializeComponent()
    {
        this.Text = $"{AppName} - Setup";
        this.ClientSize = new Size(500, 350);
        this.StartPosition = FormStartPosition.CenterScreen;
        this.FormBorderStyle = FormBorderStyle.FixedDialog;
        this.MaximizeBox = false;
        this.MinimizeBox = false;

        contentPanel = new Panel
        {
            Location = new Point(0, 0),
            Size = new Size(500, 270),
            BackColor = Color.White
        };
        this.Controls.Add(contentPanel);

        Panel buttonPanel = new Panel
        {
            Location = new Point(0, 270),
            Size = new Size(500, 80),
            BackColor = SystemColors.Control
        };
        this.Controls.Add(buttonPanel);

        backButton = new Button
        {
            Text = "< Back",
            Size = new Size(90, 30),
            Location = new Point(200, 25),
            Enabled = false
        };
        backButton.Click += (s, e) => { currentStep--; ShowStep(currentStep); };
        buttonPanel.Controls.Add(backButton);

        nextButton = new Button
        {
            Text = "Next >",
            Size = new Size(90, 30),
            Location = new Point(300, 25)
        };
        nextButton.Click += NextButton_Click;
        buttonPanel.Controls.Add(nextButton);

        cancelButton = new Button
        {
            Text = "Cancel",
            Size = new Size(90, 30),
            Location = new Point(400, 25)
        };
        cancelButton.Click += (s, e) => this.Close();
        buttonPanel.Controls.Add(cancelButton);
    }

    private async void DetectMsfsAsync()
    {
        await Task.Run(() =>
        {
            var msfs = DetectMSFS();
            detectedMsfsPath = msfs?.ConfigPath;
        });
    }

    private void ShowStep(int step)
    {
        contentPanel.Controls.Clear();
        currentStep = step;

        backButton.Enabled = step > 0 && !isUninstallMode;
        cancelButton.Enabled = step != 2 && step != 4;

        switch (step)
        {
            case 0: ShowWelcomeStep(); break;
            case 1: ShowInstallLocationStep(); break;
            case 2: ShowInstallingStep(); break;
            case 3: ShowCompletedStep(); break;
            case 4: ShowUninstallingStep(); break;
            case 5: ShowUninstallCompleteStep(); break;
        }
    }

    private void ShowWelcomeStep()
    {
        var titleLabel = new Label
        {
            Text = $"{AppName}",
            Font = new Font("Segoe UI", 16, FontStyle.Bold),
            Location = new Point(30, 60),
            Size = new Size(440, 40),
            TextAlign = ContentAlignment.MiddleLeft
        };
        contentPanel.Controls.Add(titleLabel);

        var descLabel = new Label
        {
            Text = "Automated GSX integration for MSFS.\n\nClick Next to install.",
            Location = new Point(30, 110),
            Size = new Size(440, 60),
            Font = new Font("Segoe UI", 9)
        };
        contentPanel.Controls.Add(descLabel);

        var uninstallButton = new Button
        {
            Text = "Uninstall Existing Installation",
            Location = new Point(30, 210),
            Size = new Size(220, 30)
        };
        uninstallButton.Click += (s, e) => { isUninstallMode = true; ShowStep(4); };
        contentPanel.Controls.Add(uninstallButton);

        nextButton.Text = "Next >";
        nextButton.Enabled = true;
    }

    private void ShowInstallLocationStep()
    {
        var titleLabel = new Label
        {
            Text = "Install Location",
            Font = new Font("Segoe UI", 12, FontStyle.Bold),
            Location = new Point(30, 30),
            Size = new Size(440, 30)
        };
        contentPanel.Controls.Add(titleLabel);

        string defaultPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            "SimpleGSXIntegrator"
        );

        var pathLabel = new Label
        {
            Text = "Install to:",
            Location = new Point(30, 75),
            Size = new Size(440, 20)
        };
        contentPanel.Controls.Add(pathLabel);

        var pathTextBox = new TextBox
        {
            Text = selectedInstallPath == "" ? defaultPath : selectedInstallPath,
            Location = new Point(30, 100),
            Size = new Size(350, 25),
            Name = "pathTextBox"
        };
        contentPanel.Controls.Add(pathTextBox);

        var browseButton = new Button
        {
            Text = "Browse...",
            Location = new Point(390, 98),
            Size = new Size(80, 27)
        };
        browseButton.Click += (s, e) =>
        {
            using var dialog = new FolderBrowserDialog
            {
                Description = "Select parent folder for installation",
                UseDescriptionForTitle = true,
                SelectedPath = Path.GetDirectoryName(pathTextBox.Text) ?? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)
            };

            if (dialog.ShowDialog() == DialogResult.OK)
            {
                string installPath = Path.Combine(dialog.SelectedPath, "SimpleGSXIntegrator");
                pathTextBox.Text = installPath;
                selectedInstallPath = installPath;
            }
        };
        contentPanel.Controls.Add(browseButton);

        var msfsLabel = new Label
        {
            Text = detectedMsfsPath != null 
                ? "✓ MSFS detected" 
                : "⚠ MSFS not detected",
            Location = new Point(30, 140),
            Size = new Size(200, 25),
            ForeColor = detectedMsfsPath != null ? Color.Green : Color.Orange,
            Font = new Font("Segoe UI", 9)
        };
        contentPanel.Controls.Add(msfsLabel);

        if (detectedMsfsPath == null)
        {
            var browseMsfsButton = new Button
            {
                Text = "Browse for MSFS Folder...",
                Location = new Point(240, 138),
                Size = new Size(160, 27)
            };
            browseMsfsButton.Click += (s, e) =>
            {
                using var dialog = new FolderBrowserDialog
                {
                    Description = "Select your MSFS config folder (contains or will contain exe.xml)",
                    UseDescriptionForTitle = true
                };

                if (dialog.ShowDialog() == DialogResult.OK)
                {
                    detectedMsfsPath = dialog.SelectedPath;
                    msfsLabel.Text = "✓ MSFS path selected";
                    msfsLabel.ForeColor = Color.Green;
                    browseMsfsButton.Visible = false;
                }
            };
            contentPanel.Controls.Add(browseMsfsButton);
        }

        nextButton.Text = "Install";
        nextButton.Enabled = true;

        pathTextBox.TextChanged += (s, e) => selectedInstallPath = pathTextBox.Text;
    }

    private void ShowInstallingStep()
    {
        var titleLabel = new Label
        {
            Text = "Installing...",
            Font = new Font("Segoe UI", 12, FontStyle.Bold),
            Location = new Point(30, 30),
            Size = new Size(440, 30)
        };
        contentPanel.Controls.Add(titleLabel);

        var progressBar = new ProgressBar
        {
            Location = new Point(30, 80),
            Size = new Size(440, 30),
            Style = ProgressBarStyle.Marquee,
            MarqueeAnimationSpeed = 30,
            Name = "progressBar"
        };
        contentPanel.Controls.Add(progressBar);

        var statusLabel = new Label
        {
            Text = "Installing files...",
            Location = new Point(30, 120),
            Size = new Size(440, 20),
            Name = "statusLabel"
        };
        contentPanel.Controls.Add(statusLabel);

        var logBox = new RichTextBox
        {
            Location = new Point(30, 150),
            Size = new Size(440, 100),
            ReadOnly = true,
            BorderStyle = BorderStyle.FixedSingle,
            Name = "logBox"
        };
        contentPanel.Controls.Add(logBox);

        nextButton.Enabled = false;
        backButton.Enabled = false;

        Task.Run(() => PerformInstallation());
    }

    private void ShowCompletedStep()
    {
        var titleLabel = new Label
        {
            Text = "Installation Complete",
            Font = new Font("Segoe UI", 12, FontStyle.Bold),
            Location = new Point(30, 60),
            Size = new Size(440, 30),
            ForeColor = Color.Green
        };
        contentPanel.Controls.Add(titleLabel);

        var msgLabel = new Label
        {
            Text = detectedMsfsPath != null 
                ? "App will auto-launch with MSFS." 
                : "Run SimpleGSXIntegrator.exe before starting MSFS.",
            Location = new Point(30, 110),
            Size = new Size(440, 40),
            Font = new Font("Segoe UI", 9)
        };
        contentPanel.Controls.Add(msgLabel);

        nextButton.Text = "Finish";
        nextButton.Enabled = true;
        backButton.Enabled = false;
    }

    private void ShowUninstallingStep()
    {
        var titleLabel = new Label
        {
            Text = "Uninstalling...",
            Font = new Font("Segoe UI", 12, FontStyle.Bold),
            Location = new Point(30, 30),
            Size = new Size(440, 30)
        };
        contentPanel.Controls.Add(titleLabel);

        var progressBar = new ProgressBar
        {
            Location = new Point(30, 80),
            Size = new Size(440, 30),
            Style = ProgressBarStyle.Marquee,
            MarqueeAnimationSpeed = 30
        };
        contentPanel.Controls.Add(progressBar);

        var statusLabel = new Label
        {
            Text = "Removing files...",
            Location = new Point(30, 120),
            Size = new Size(440, 20),
            Name = "statusLabel"
        };
        contentPanel.Controls.Add(statusLabel);

        nextButton.Enabled = false;
        backButton.Enabled = false;

        Task.Run(() => PerformUninstallation());
    }

    private void ShowUninstallCompleteStep()
    {
        var titleLabel = new Label
        {
            Text = "Uninstall Complete",
            Font = new Font("Segoe UI", 12, FontStyle.Bold),
            Location = new Point(30, 60),
            Size = new Size(440, 30),
            ForeColor = Color.Green
        };
        contentPanel.Controls.Add(titleLabel);

        var msgLabel = new Label
        {
            Text = "Successfully removed.",
            Location = new Point(30, 110),
            Size = new Size(440, 30),
            Font = new Font("Segoe UI", 9)
        };
        contentPanel.Controls.Add(msgLabel);

        nextButton.Text = "Finish";
        nextButton.Enabled = true;
        backButton.Enabled = false;
    }

    private void NextButton_Click(object? sender, EventArgs e)
    {
        if (currentStep == 0)
        {
            ShowStep(1);
        }
        else if (currentStep == 1)
        {
            if (string.IsNullOrWhiteSpace(selectedInstallPath))
            {
                MessageBox.Show("Please select an installation location.", "Error", 
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            ShowStep(2); 
        }
        else if (currentStep == 3 || currentStep == 5)
        {
            this.Close(); 
        }
    }

    private void PerformInstallation()
    {
        try
        {
            LogMessage("Creating installation directory...");
            Directory.CreateDirectory(selectedInstallPath);

            LogMessage("Copying application files...");
            string payloadDir = Path.Combine(AppContext.BaseDirectory, "Payload");
            
            if (!Directory.Exists(payloadDir))
            {
                throw new Exception($"Payload directory not found: {payloadDir}");
            }

            CopyDirectory(payloadDir, selectedInstallPath, true);
            LogMessage("Files copied successfully");

            if (detectedMsfsPath != null)
            {
                LogMessage("Configuring MSFS auto-launch...");
                string exeXmlPath = Path.Combine(detectedMsfsPath, ExeXmlFileName);
                string appExePath = Path.Combine(selectedInstallPath, AppExeName);

                if (File.Exists(exeXmlPath))
                {
                    string backupPath = exeXmlPath + ".backup";
                    if (!File.Exists(backupPath))
                    {
                        File.Copy(exeXmlPath, backupPath, true);
                    }
                }

                UpdateExeXml(exeXmlPath, appExePath);
                LogMessage("Auto-launch configured");
            }

            SaveInstallInfo(selectedInstallPath, detectedMsfsPath ?? "");
            LogMessage("Installation complete!");

            this.Invoke(() => ShowStep(3));
        }
        catch (Exception ex)
        {
            this.Invoke(() =>
            {
                MessageBox.Show($"Installation failed:\n\n{ex.Message}", "Error", 
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                ShowStep(0);
            });
        }
    }

    private void PerformUninstallation()
    {
        try
        {
            string? installPath = null;
            string? msfsConfigPath = null;

            string appDataPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "SimpleGSXIntegrator"
            );
            string centralInfoPath = Path.Combine(appDataPath, InstallInfoFileName);
            
            if (File.Exists(centralInfoPath))
            {
                var info = LoadInstallInfo(centralInfoPath);
                installPath = info.InstallPath;
                msfsConfigPath = info.MsfsConfigPath;
            }
            else
            {
                this.Invoke(() =>
                {
                    var result = MessageBox.Show(
                        "Could not find installation info. Please browse to the installation folder.",
                        "Locate Installation",
                        MessageBoxButtons.OKCancel,
                        MessageBoxIcon.Question
                    );
                    
                    if (result == DialogResult.OK)
                    {
                        using var dialog = new FolderBrowserDialog
                        {
                            Description = "Select the SimpleGSXIntegrator installation folder",
                            UseDescriptionForTitle = true
                        };
                        
                        if (dialog.ShowDialog() == DialogResult.OK)
                        {
                            installPath = dialog.SelectedPath;
                            
                            string localInfoPath = Path.Combine(installPath, InstallInfoFileName);
                            if (File.Exists(localInfoPath))
                            {
                                var info = LoadInstallInfo(localInfoPath);
                                msfsConfigPath = info.MsfsConfigPath;
                            }
                            else
                            {
                                msfsConfigPath = detectedMsfsPath;
                            }
                        }
                    }
                });
                
                if (string.IsNullOrEmpty(installPath))
                {
                    throw new Exception("Installation folder not specified.");
                }
            }

            if (!string.IsNullOrEmpty(msfsConfigPath))
            {
                UpdateStatusLabel("Removing from exe.xml...");
                string exeXmlPath = Path.Combine(msfsConfigPath, ExeXmlFileName);
                if (File.Exists(exeXmlPath))
                {
                    RemoveFromExeXml(exeXmlPath);
                }
            }

            if (Directory.Exists(installPath))
            {
                UpdateStatusLabel("Deleting files...");
                Directory.Delete(installPath, true);
            }

            this.Invoke(() => ShowStep(5));
        }
        catch (Exception ex)
        {
            this.Invoke(() =>
            {
                MessageBox.Show($"Uninstall failed:\n\n{ex.Message}", "Error", 
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                ShowStep(0);
            });
        }
    }

    private void LogMessage(string message)
    {
        this.Invoke(() =>
        {
            var logBox = contentPanel.Controls.Find("logBox", false).FirstOrDefault() as RichTextBox;
            if (logBox != null)
            {
                logBox.AppendText(message + Environment.NewLine);
            }
        });
    }

    private void UpdateStatusLabel(string text)
    {
        this.Invoke(() =>
        {
            var statusLabel = contentPanel.Controls.Find("statusLabel", false).FirstOrDefault() as Label;
            if (statusLabel != null)
            {
                statusLabel.Text = text;
            }
        });
    }

    private MsfsInfo? DetectMSFS()
    {
        string msfs2024Limitless = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Packages", "Microsoft.Limitless_8wekyb3d8bbwe", "LocalCache"
        );

        if (Directory.Exists(msfs2024Limitless))
        {
            return new MsfsInfo { Version = "MSFS 2024", ConfigPath = msfs2024Limitless };
        }

        string msfs2024Store = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Packages", "Microsoft.FlightSimulator_8wekyb3d8bbwe", "LocalCache"
        );

        if (Directory.Exists(msfs2024Store))
        {
            return new MsfsInfo { Version = "MSFS 2024 (MS Store)", ConfigPath = msfs2024Store };
        }

        string msfsAppData = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Microsoft Flight Simulator"
        );

        if (Directory.Exists(msfsAppData))
        {
            return new MsfsInfo { Version = "MSFS 2020/2024", ConfigPath = msfsAppData };
        }

        return null;
    }

    private void UpdateExeXml(string exeXmlPath, string appExePath)
    {
        XDocument doc;

        if (File.Exists(exeXmlPath))
        {
            doc = XDocument.Load(exeXmlPath);
            var existingAddon = doc.Descendants("Launch.Addon")
                .FirstOrDefault(e => e.Element("Name")?.Value == AppName);

            if (existingAddon != null)
            {
                var pathElement = existingAddon.Element("Path");
                if (pathElement != null)
                    pathElement.Value = appExePath;
            }
            else
            {
                doc.Root?.Add(CreateAddonElement(appExePath));
            }
        }
        else
        {
            doc = new XDocument(
                new XElement("SimBase.Document",
                    new XAttribute("Type", "Launch"),
                    new XAttribute("version", "1,0"),
                    new XElement("Descr", "Launch"),
                    new XElement("Filename", ExeXmlFileName),
                    new XElement("Disabled", "False"),
                    new XElement("Launch.ManualLoad", "False"),
                    CreateAddonElement(appExePath)
                )
            );
        }

        doc.Save(exeXmlPath);
    }

    private XElement CreateAddonElement(string appExePath)
    {
        return new XElement("Launch.Addon",
            new XElement("Name", AppName),
            new XElement("Disabled", "False"),
            new XElement("ManualLoad", "False"),
            new XElement("Path", appExePath),
            new XElement("CommandLine", "")
        );
    }

    private void RemoveFromExeXml(string exeXmlPath)
    {
        if (!File.Exists(exeXmlPath)) return;

        try
        {
            var doc = XDocument.Load(exeXmlPath);
            var addonToRemove = doc.Descendants("Launch.Addon")
                .FirstOrDefault(e => e.Element("Name")?.Value == AppName);
            addonToRemove?.Remove();
            doc.Save(exeXmlPath);
        }
        catch { }
    }

    private void CopyDirectory(string sourceDir, string destDir, bool recursive)
    {
        var dir = new DirectoryInfo(sourceDir);
        if (!dir.Exists)
            throw new DirectoryNotFoundException($"Source directory not found: {sourceDir}");

        Directory.CreateDirectory(destDir);

        foreach (FileInfo file in dir.GetFiles())
        {
            file.CopyTo(Path.Combine(destDir, file.Name), true);
        }

        if (recursive)
        {
            foreach (DirectoryInfo subDir in dir.GetDirectories())
            {
                CopyDirectory(subDir.FullName, Path.Combine(destDir, subDir.Name), true);
            }
        }
    }

    private void SaveInstallInfo(string installPath, string msfsConfigPath)
    {
        string infoPath = Path.Combine(installPath, InstallInfoFileName);
        File.WriteAllText(infoPath, $"{installPath}\n{msfsConfigPath}");
        
        string appDataPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "SimpleGSXIntegrator"
        );
        Directory.CreateDirectory(appDataPath);
        string centralInfoPath = Path.Combine(appDataPath, InstallInfoFileName);
        File.WriteAllText(centralInfoPath, $"{installPath}\n{msfsConfigPath}");
    }

    private (string InstallPath, string MsfsConfigPath) LoadInstallInfo(string infoPath)
    {
        var lines = File.ReadAllLines(infoPath);
        return (lines.Length > 0 ? lines[0] : "", lines.Length > 1 ? lines[1] : "");
    }

    private class MsfsInfo
    {
        public string Version { get; set; } = "";
        public string ConfigPath { get; set; } = "";
    }
}
