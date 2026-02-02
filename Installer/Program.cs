using System.Diagnostics;

namespace SimpleGSXIntegrator.Installer;

class Program
{
    [STAThread]
    static void Main(string[] args)
    {
        var runningProcesses = Process.GetProcessesByName("SimpleGSXIntegrator");
        if (runningProcesses.Length > 0)
        {
            var result = MessageBox.Show(
                "Simple GSX Integrator is currently running. It must be closed before installation can continue.\n\n" +
                "Would you like to close it now?",
                "Application Running",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning);

            if (result == DialogResult.Yes)
            {
                try
                {
                    foreach (var process in runningProcesses)
                    {
                        process.Kill();
                        process.WaitForExit(5000); 
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show(
                        $"Failed to close the application: {ex.Message}\n\nPlease close it manually and run the installer again.",
                        "Error",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Error);
                    return;
                }
            }
            else
            {
                return; // User chose not to close, exit installer
            }
        }

        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.Run(new InstallerForm());
    }
}