using System.Diagnostics;
using System.Net.Http.Json;
using System.Reflection;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using PrinterAPP.Models;

namespace PrinterAPP.Services;

public class UpdateService
{
    private readonly ILogger<UpdateService> _logger;
    private readonly HttpClient _httpClient;
    private const string GITHUB_REPO = "mahmutkaya/rumi-restaurant";
    private const string RELEASES_API_URL = $"https://api.github.com/repos/{GITHUB_REPO}/releases/latest";

    public UpdateService(ILogger<UpdateService> logger)
    {
        _logger = logger;
        _httpClient = new HttpClient();
        _httpClient.DefaultRequestHeaders.Add("User-Agent", "PrinterApp-Updater");
    }

    public string GetCurrentVersion()
    {
        var assembly = Assembly.GetExecutingAssembly();
        var version = assembly.GetName().Version;
        return version != null ? $"{version.Major}.{version.Minor}.{version.Build}" : "1.0.0";
    }

    public async Task<UpdateInfo> CheckForUpdateAsync()
    {
        var updateInfo = new UpdateInfo
        {
            CurrentVersion = GetCurrentVersion()
        };

        try
        {
            _logger.LogInformation("Checking for updates from GitHub...");
            
            var response = await _httpClient.GetFromJsonAsync<GitHubRelease>(RELEASES_API_URL);
            
            if (response == null)
            {
                _logger.LogWarning("No release information found");
                return updateInfo;
            }

            // Parse version from tag (e.g., "v1.0.1" -> "1.0.1")
            var latestVersion = response.TagName?.TrimStart('v') ?? "0.0.0";
            updateInfo.LatestVersion = latestVersion;
            updateInfo.ReleaseName = response.Name ?? "";
            updateInfo.ReleaseNotes = response.Body ?? "";

            // Determine system architecture
            bool is64Bit = Environment.Is64BitOperatingSystem;
            string arch = is64Bit ? "x64" : "x86";

            // Find the best matching exe asset
            var exeAsset = response.Assets?.FirstOrDefault(a => 
                a.Name?.Contains(arch, StringComparison.OrdinalIgnoreCase) == true && 
                a.Name?.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) == true);

            // Fallback: If no arch-specific found, take any .exe (legacy support)
            if (exeAsset == null)
            {
                exeAsset = response.Assets?.FirstOrDefault(a => 
                    a.Name?.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) == true);
            }

            if (exeAsset != null)
            {
                updateInfo.DownloadUrl = exeAsset.BrowserDownloadUrl ?? "";
                updateInfo.FileSize = exeAsset.Size;
            }

            // Compare versions
            updateInfo.UpdateAvailable = CompareVersions(updateInfo.CurrentVersion, latestVersion) < 0;

            _logger.LogInformation("Current version: {Current}, Latest version: {Latest}, Update available: {Available}",
                updateInfo.CurrentVersion, latestVersion, updateInfo.UpdateAvailable);

            return updateInfo;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking for updates");
            return updateInfo;
        }
    }

    public async Task<bool> DownloadAndInstallUpdateAsync(UpdateInfo updateInfo, IProgress<int>? progress = null)
    {
        try
        {
            _logger.LogInformation("Starting download from {Url}", updateInfo.DownloadUrl);

            // Download to temp file
            var tempFile = Path.Combine(Path.GetTempPath(), "PrinterApp_Update.exe");
            
            using (var response = await _httpClient.GetAsync(updateInfo.DownloadUrl, HttpCompletionOption.ResponseHeadersRead))
            {
                response.EnsureSuccessStatusCode();
                
                var totalBytes = response.Content.Headers.ContentLength ?? 0;
                var downloadedBytes = 0L;

                using (var contentStream = await response.Content.ReadAsStreamAsync())
                using (var fileStream = new FileStream(tempFile, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true))
                {
                    var buffer = new byte[8192];
                    int bytesRead;

                    while ((bytesRead = await contentStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                    {
                        await fileStream.WriteAsync(buffer, 0, bytesRead);
                        downloadedBytes += bytesRead;

                        if (totalBytes > 0)
                        {
                            var progressPercent = (int)((downloadedBytes * 100) / totalBytes);
                            progress?.Report(progressPercent);
                        }
                    }
                }
            }

            _logger.LogInformation("Download complete. File size: {Size} bytes", new FileInfo(tempFile).Length);

            // Verify file was downloaded and has reasonable size
            var fileInfo = new FileInfo(tempFile);
            if (!fileInfo.Exists || fileInfo.Length < 1024)
            {
                _logger.LogError("Downloaded file is invalid or too small");
                return false;
            }

            // Install update
            return await InstallUpdateAsync(tempFile);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error downloading/installing update");
            return false;
        }
    }

    private async Task<bool> InstallUpdateAsync(string updateFilePath)
    {
        try
        {
            var currentExePath = Process.GetCurrentProcess().MainModule?.FileName;
            if (string.IsNullOrEmpty(currentExePath))
            {
                _logger.LogError("Could not determine current exe path");
                return false;
            }

            var backupPath = currentExePath + ".bak";
            _logger.LogInformation("Creating backup: {Backup}", backupPath);

            // Backup current version
            if (File.Exists(backupPath))
            {
                File.Delete(backupPath);
            }
            File.Copy(currentExePath, backupPath);

            _logger.LogInformation("Installing update...");

            // Create a batch script to replace the exe and restart
            var batchScript = Path.Combine(Path.GetTempPath(), "update_printerapp.bat");
            var scriptContent = $@"
@echo off
timeout /t 2 /nobreak > nul
copy /Y ""{updateFilePath}"" ""{currentExePath}""
if errorlevel 1 (
    echo Update failed, restoring backup...
    copy /Y ""{backupPath}"" ""{currentExePath}""
) else (
    echo Update successful
    del ""{backupPath}""
)
start """" ""{currentExePath}""
del ""{updateFilePath}""
del ""{batchScript}""
";

            await File.WriteAllTextAsync(batchScript, scriptContent);

            // Start the batch script and exit
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = batchScript,
                    CreateNoWindow = true,
                    UseShellExecute = false
                }
            };

            process.Start();
            
            _logger.LogInformation("Update script started. App will restart...");

            // Give the batch script time to start
            await Task.Delay(500);

            // Exit the current app
            Application.Current?.Quit();

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error installing update");
            return false;
        }
    }

    private int CompareVersions(string version1, string version2)
    {
        var v1Parts = version1.Split('.').Select(int.Parse).ToArray();
        var v2Parts = version2.Split('.').Select(int.Parse).ToArray();

        for (int i = 0; i < Math.Max(v1Parts.Length, v2Parts.Length); i++)
        {
            var v1 = i < v1Parts.Length ? v1Parts[i] : 0;
            var v2 = i < v2Parts.Length ? v2Parts[i] : 0;

            if (v1 < v2) return -1;
            if (v1 > v2) return 1;
        }

        return 0;
    }

    // GitHub API response models
    private class GitHubRelease
    {
        [JsonPropertyName("tag_name")]
        public string? TagName { get; set; }

        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("body")]
        public string? Body { get; set; }

        [JsonPropertyName("assets")]
        public List<GitHubAsset>? Assets { get; set; }
    }

    private class GitHubAsset
    {
        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("browser_download_url")]
        public string? BrowserDownloadUrl { get; set; }

        [JsonPropertyName("size")]
        public long Size { get; set; }
    }
}
