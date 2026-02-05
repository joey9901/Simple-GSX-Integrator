using System.Reflection;
using System.Text.Json;
using System.IO.Compression;

namespace SimpleGsxIntegrator;

public class UpdateInfo
{
    public string LatestVersion { get; set; } = "";
    public string DownloadUrl { get; set; } = "";
    public string ReleaseNotes { get; set; } = "";
}

public static class UpdateChecker
{
    private const string UpdateCheckUrl = "https://raw.githubusercontent.com/joey9901/Simple-GSX-Integrator/main/version.json";
    
    public static string GetCurrentVersion()
    {
        var version = Assembly.GetExecutingAssembly().GetName().Version;
        return version != null ? $"{version.Major}.{version.Minor}.{version.Build}" : "1.0.0";
    }
    
    public static async Task<UpdateInfo?> CheckForUpdatesAsync()
    {
        try
        {
            Logger.Debug($"Fetching update info from: {UpdateCheckUrl}");
            using var client = new HttpClient();
            client.Timeout = TimeSpan.FromSeconds(5);
            
            var json = await client.GetStringAsync(UpdateCheckUrl);
            Logger.Debug($"Received JSON: {json}");
            
            var updateInfo = JsonSerializer.Deserialize<UpdateInfo>(json, new JsonSerializerOptions 
            { 
                PropertyNameCaseInsensitive = true 
            });
            
            if (updateInfo != null)
            {
                Logger.Debug($"Parsed version: {updateInfo.LatestVersion}, Current: {GetCurrentVersion()}");
                if (IsNewerVersion(updateInfo.LatestVersion))
                {
                    Logger.Debug($"Newer version detected!");
                    return updateInfo;
                }
                else
                {
                    Logger.Debug($"Version {updateInfo.LatestVersion} is not newer than {GetCurrentVersion()}");
                }
            }
        }
        catch (Exception ex)
        {
            Logger.Debug($"Update check failed: {ex.Message}");
        }
        
        return null;
    }
    
    private static bool IsNewerVersion(string latestVersion)
    {
        try
        {
            var current = Version.Parse(GetCurrentVersion());
            var latest = Version.Parse(latestVersion);
            return latest > current;
        }
        catch
        {
            return false;
        }
    }
    
    public static async Task<string?> DownloadUpdateAsync(string downloadUrl, IProgress<int> progress)
    {
        try
        {
            Logger.Info($"Downloading update from: {downloadUrl}");
            
            var tempPath = Path.Combine(Path.GetTempPath(), "SimpleGSXIntegrator_Update");
            if (Directory.Exists(tempPath))
            {
                Directory.Delete(tempPath, true);
            }
            Directory.CreateDirectory(tempPath);
            
            // Determine file extension from URL
            var isZip = downloadUrl.EndsWith(".zip", StringComparison.OrdinalIgnoreCase);
            var fileName = isZip ? "update.zip" : "SimpleGSXIntegrator.exe";
            var downloadPath = Path.Combine(tempPath, fileName);
            
            using var client = new HttpClient();
            client.Timeout = TimeSpan.FromMinutes(5);
            
            using var response = await client.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead);
            response.EnsureSuccessStatusCode();
            
            var totalBytes = response.Content.Headers.ContentLength ?? 0;
            using var contentStream = await response.Content.ReadAsStreamAsync();
            using var fileStream = new FileStream(downloadPath, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true);
            
            var buffer = new byte[8192];
            long totalRead = 0;
            int bytesRead;
            
            while ((bytesRead = await contentStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
            {
                await fileStream.WriteAsync(buffer, 0, bytesRead);
                totalRead += bytesRead;
                
                if (totalBytes > 0)
                {
                    var percentComplete = (int)((totalRead * 100) / totalBytes);
                    progress.Report(percentComplete);
                }
            }
            
            Logger.Info($"Download complete: {downloadPath}");
            return downloadPath;
        }
        catch (Exception ex)
        {
            Logger.Warning($"Download failed: {ex.Message}");
            return null;
        }
    }
    
    public static void InstallUpdateAndRestart(string downloadedFile)
    {
        try
        {
            Logger.Info("Installing update...");
            
            var tempPath = Path.GetDirectoryName(downloadedFile)!;
            var currentExePath = System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName ?? "";
            var installDir = Path.GetDirectoryName(currentExePath)!;
            
            string updateSource;
            
            // Check if it's a zip file that needs extraction
            if (downloadedFile.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
            {
                Logger.Info("Extracting zip file...");
                var extractPath = Path.Combine(tempPath, "extracted");
                
                if (Directory.Exists(extractPath))
                {
                    Directory.Delete(extractPath, true);
                }
                ZipFile.ExtractToDirectory(downloadedFile, extractPath);
                
                Logger.Info($"Extracted update to: {extractPath}");
                updateSource = extractPath;
            }
            else
            {
                // It's a direct .exe file
                Logger.Info("Using direct executable update...");
                updateSource = downloadedFile;
            }
            
            // Create PowerShell script to replace files and restart
            var scriptPath = Path.Combine(tempPath, "update.ps1");
            
            var script = $@"
# Wait for the main process to exit
Start-Sleep -Seconds 2

Write-Host 'Updating SimpleGSXIntegrator...'

# Copy the update file(s) to install directory
Copy-Item -Path '{updateSource}' -Destination '{installDir}' -Recurse -Force

Write-Host 'Update complete! Restarting...'

# Restart the application
Start-Process '{currentExePath}'

# Clean up temp files
Start-Sleep -Seconds 1
Remove-Item -Path '{tempPath}' -Recurse -Force -ErrorAction SilentlyContinue
";
            
            File.WriteAllText(scriptPath, script);
            
            Logger.Info("Launching update script...");
            
            // Launch the PowerShell script
            var psi = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = $"-NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -File \"{scriptPath}\"",
                UseShellExecute = false,
                CreateNoWindow = true
            };
            
            System.Diagnostics.Process.Start(psi);
            
            Logger.Info("Update script launched. Exiting...");
            
            // Exit the current application
            Environment.Exit(0);
        }
        catch (Exception ex)
        {
            Logger.Warning($"Update installation failed: {ex.Message}");
            throw;
        }
    }
}
