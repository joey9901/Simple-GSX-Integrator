using System.Reflection;
using System.Text.Json;

namespace SimpleGsxIntegrator;

public class UpdateInfo
{
    public string LatestVersion { get; set; } = "";
    public string DownloadUrl { get; set; } = "";
    public string ReleaseNotes { get; set; } = "";
}

public static class UpdateChecker
{
    private const string UpdateCheckUrl = "https://raw.githubusercontent.com/joey9901/SimpleGSXIntegrator/dev/version.json";
    
    public static string GetCurrentVersion()
    {
        var version = Assembly.GetExecutingAssembly().GetName().Version;
        return version != null ? $"{version.Major}.{version.Minor}.{version.Build}" : "1.0.0";
    }
    
    public static async Task<UpdateInfo?> CheckForUpdatesAsync()
    {
        try
        {
            using var client = new HttpClient();
            client.Timeout = TimeSpan.FromSeconds(5);
            
            var json = await client.GetStringAsync(UpdateCheckUrl);
            var updateInfo = JsonSerializer.Deserialize<UpdateInfo>(json, new JsonSerializerOptions 
            { 
                PropertyNameCaseInsensitive = true 
            });
            
            if (updateInfo != null && IsNewerVersion(updateInfo.LatestVersion))
            {
                return updateInfo;
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
}
