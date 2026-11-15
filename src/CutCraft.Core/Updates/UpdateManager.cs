using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Text.Json;

namespace CutCraft.Core.Updates;

public class UpdateManager
{
    public const string CurrentVersionString = "0.1.1";
    public static readonly Version CurrentVersion = new(0, 1, 1);

    public string RemoteManifestUrl { get; set; } = "https://raw.githubusercontent.com/matingohil11-ops/CutCraft/main/update-manifest.json";
    public string LocalFallbackManifestPath { get; set; } = "update-manifest.json";

    private readonly HttpClient _httpClient;

    public UpdateManager(HttpClient? httpClient = null)
    {
        _httpClient = httpClient ?? new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
        if (!_httpClient.DefaultRequestHeaders.Contains("User-Agent"))
        {
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "CutCraft-AutoUpdater/0.1.1");
        }
    }

    /// <summary>
    /// Checks for available updates using remote or local manifest.
    /// </summary>
    public async Task<UpdateCheckResult> CheckForUpdatesAsync(string? manifestUrlOrPath = null)
    {
        var target = manifestUrlOrPath ?? RemoteManifestUrl;

        UpdateInfo? updateInfo = null;

        // Try downloading remote manifest first
        try
        {
            if (target.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                target.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                var cacheBustUrl = target.Contains('?') 
                    ? $"{target}&_t={DateTimeOffset.UtcNow.ToUnixTimeSeconds()}" 
                    : $"{target}?_t={DateTimeOffset.UtcNow.ToUnixTimeSeconds()}";
                var json = await _httpClient.GetStringAsync(cacheBustUrl);
                updateInfo = JsonSerializer.Deserialize<UpdateInfo>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
            }
        }
        catch
        {
            // If offline or network unreachable, check local manifest fallback
        }

        // Fallback to local manifest if remote check was not completed
        if (updateInfo == null)
        {
            var localPath = File.Exists(target) ? target : Path.Combine(AppDomain.CurrentDomain.BaseDirectory, LocalFallbackManifestPath);
            if (File.Exists(localPath))
            {
                try
                {
                    var json = await File.ReadAllTextAsync(localPath);
                    updateInfo = JsonSerializer.Deserialize<UpdateInfo>(json, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });
                }
                catch
                {
                    // Ignore parse errors
                }
            }
        }

        if (updateInfo == null)
        {
            return new UpdateCheckResult
            {
                HasUpdate = false,
                CurrentVersion = CurrentVersionString,
                LatestVersion = CurrentVersionString,
                Message = $"CutCraft is up to date (Version {CurrentVersionString})."
            };
        }

        var hasUpdate = IsVersionGreater(updateInfo.Version, CurrentVersionString);

        return new UpdateCheckResult
        {
            HasUpdate = hasUpdate,
            Update = updateInfo,
            CurrentVersion = CurrentVersionString,
            LatestVersion = updateInfo.Version,
            Message = hasUpdate
                ? $"Update available: CutCraft {updateInfo.Version} ({updateInfo.Name})"
                : $"CutCraft is up to date (Version {CurrentVersionString})."
        };
    }

    /// <summary>
    /// Compares two semver / version strings (e.g. "0.0", "0.1", "1.0.0").
    /// </summary>
    public static bool IsVersionGreater(string candidateVersion, string baseVersion)
    {
        if (Version.TryParse(NormalizeVersion(candidateVersion), out var candidate) &&
            Version.TryParse(NormalizeVersion(baseVersion), out var current))
        {
            return candidate > current;
        }

        return string.Compare(candidateVersion, baseVersion, StringComparison.OrdinalIgnoreCase) > 0;
    }

    private static string NormalizeVersion(string ver)
    {
        var parts = ver.TrimStart('v', 'V').Split('.');
        while (parts.Length < 3)
        {
            ver += ".0";
            parts = ver.Split('.');
        }
        return ver;
    }

    /// <summary>
    /// Downloads the update package into the destination folder.
    /// </summary>
    public async Task<string> DownloadUpdateAsync(UpdateInfo update, string destinationDir, IProgress<double>? progress = null, CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(destinationDir);
        var fileName = Path.GetFileName(new Uri(update.DownloadUrl).LocalPath);
        if (string.IsNullOrWhiteSpace(fileName))
        {
            fileName = $"CutCraft-Update-{update.Version}.zip";
        }

        var targetFile = Path.Combine(destinationDir, fileName);

        if (File.Exists(update.DownloadUrl))
        {
            File.Copy(update.DownloadUrl, targetFile, true);
            progress?.Report(1.0);
            return targetFile;
        }

        using var response = await _httpClient.GetAsync(update.DownloadUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();

        var totalBytes = response.Content.Headers.ContentLength ?? -1L;
        await using var contentStream = await response.Content.ReadAsStreamAsync(cancellationToken);
        await using var fileStream = new FileStream(targetFile, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true);

        var buffer = new byte[8192];
        long totalRead = 0;
        int bytesRead;

        while ((bytesRead = await contentStream.ReadAsync(buffer, 0, buffer.Length, cancellationToken)) > 0)
        {
            await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken);
            totalRead += bytesRead;
            if (totalBytes > 0 && progress != null)
            {
                progress.Report((double)totalRead / totalBytes);
            }
        }

        return targetFile;
    }

    /// <summary>
    /// Launches the updater batch script to replace current application binaries and restart.
    /// </summary>
    public bool ApplyUpdate(string updateZipPath, bool restartApp = true)
    {
        try
        {
            var appDir = AppDomain.CurrentDomain.BaseDirectory;
            var scriptPath = Path.Combine(appDir, "updater.bat");

            if (!File.Exists(scriptPath))
            {
                // Generate updater script if it doesn't exist
                var scriptContent = @$"@echo off
setlocal
echo Applying CutCraft update...
timeout /t 2 /nobreak >nul
tar -xf ""{updateZipPath}"" -C ""{appDir}""
if ""{restartApp}""==""True"" (
    start """" ""{Path.Combine(appDir, "CutCraft.exe")}""
)
del ""{updateZipPath}""
";
                File.WriteAllText(scriptPath, scriptContent);
            }

            var startInfo = new ProcessStartInfo
            {
                FileName = scriptPath,
                UseShellExecute = true,
                CreateNoWindow = false,
                WorkingDirectory = appDir
            };

            Process.Start(startInfo);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
