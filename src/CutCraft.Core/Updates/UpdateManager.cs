using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text.Json;

namespace CutCraft.Core.Updates;

public class UpdateManager
{
    public const string CurrentVersionString = "1.1.1";
    public static readonly Version CurrentVersion = new(1, 1, 1);

    public string RemoteManifestUrl { get; set; } = "https://raw.githubusercontent.com/matingohil11-ops/CutCraft/main/update-manifest.json";
    public string LocalFallbackManifestPath { get; set; } = "update-manifest.json";

    private readonly HttpClient _httpClient;

    public UpdateManager(HttpClient? httpClient = null)
    {
        _httpClient = httpClient ?? new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
        if (!_httpClient.DefaultRequestHeaders.Contains("User-Agent"))
        {
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "CutCraft-AutoUpdater/1.1.1");
        }
    }

    /// <summary>
    /// Checks for available updates using remote or local manifest.
    /// Properly reports network connection errors when offline.
    /// </summary>
    public async Task<UpdateCheckResult> CheckForUpdatesAsync(string? manifestUrlOrPath = null)
    {
        var target = manifestUrlOrPath ?? RemoteManifestUrl;
        UpdateInfo? updateInfo = null;
        string? failureReason = null;
        bool isNetworkError = false;

        bool isRemote = target.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                        target.StartsWith("https://", StringComparison.OrdinalIgnoreCase);

        if (isRemote)
        {
            try
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
            catch (Exception ex)
            {
                isNetworkError = true;
                failureReason = $"Network connection error: {ex.Message}";
            }
        }
        else
        {
            // Local file path
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
                catch (Exception ex)
                {
                    failureReason = $"Failed to parse manifest: {ex.Message}";
                }
            }
            else
            {
                failureReason = $"Local manifest file not found: {localPath}";
            }
        }

        // If remote check failed, only check local fallback if it was explicitly configured and exists
        if (updateInfo == null && isRemote && !string.IsNullOrWhiteSpace(LocalFallbackManifestPath))
        {
            var fallbackPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, LocalFallbackManifestPath);
            if (File.Exists(fallbackPath))
            {
                try
                {
                    var json = await File.ReadAllTextAsync(fallbackPath);
                    updateInfo = JsonSerializer.Deserialize<UpdateInfo>(json, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });
                    // Fallback recovered, clear failureReason
                    if (updateInfo != null)
                    {
                        isNetworkError = false;
                        failureReason = null;
                    }
                }
                catch { }
            }
        }

        if (updateInfo == null)
        {
            return new UpdateCheckResult
            {
                HasUpdate = false,
                IsSuccess = false,
                IsNetworkError = isNetworkError,
                ErrorMessage = failureReason ?? "Could not retrieve update information.",
                CurrentVersion = CurrentVersionString,
                LatestVersion = CurrentVersionString,
                Message = isNetworkError
                    ? "Unable to connect to the update server. Please check your internet connection."
                    : (failureReason ?? "No update information found.")
            };
        }

        var hasUpdate = IsVersionGreater(updateInfo.Version, CurrentVersionString);

        return new UpdateCheckResult
        {
            HasUpdate = hasUpdate,
            IsSuccess = true,
            Update = updateInfo,
            CurrentVersion = CurrentVersionString,
            LatestVersion = updateInfo.Version,
            Message = hasUpdate
                ? $"Update available: CutCraft {updateInfo.Version} ({updateInfo.Name})"
                : "CutCraft is up to date. You are using the latest version."
        };
    }

    /// <summary>
    /// Parses version strings safely (handles 'v' prefix, pre-releases, 2-part and 3-part versions).
    /// </summary>
    public static bool TryParseVersion(string? versionStr, out Version? version)
    {
        version = null;
        if (string.IsNullOrWhiteSpace(versionStr))
            return false;

        var clean = versionStr.Trim().TrimStart('v', 'V').Trim();

        // Strip pre-release or build suffix (e.g. "-beta", "+build123")
        var dashIdx = clean.IndexOfAny(new[] { '-', '+' });
        if (dashIdx >= 0)
        {
            clean = clean.Substring(0, dashIdx);
        }

        var parts = clean.Split('.', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0)
            return false;

        var numbers = new List<int>();
        foreach (var p in parts)
        {
            if (int.TryParse(p, out var n))
            {
                numbers.Add(n);
            }
            else
            {
                break;
            }
        }

        if (numbers.Count == 0)
            return false;

        while (numbers.Count < 2)
        {
            numbers.Add(0);
        }

        if (numbers.Count == 2)
            version = new Version(numbers[0], numbers[1]);
        else if (numbers.Count == 3)
            version = new Version(numbers[0], numbers[1], numbers[2]);
        else
            version = new Version(numbers[0], numbers[1], numbers[2], numbers[3]);

        return true;
    }

    /// <summary>
    /// Compares two semver / version strings correctly (e.g. "1.2.0" > "1.1.1", "0.10.0" > "0.9.0").
    /// </summary>
    public static bool IsVersionGreater(string candidateVersion, string baseVersion)
    {
        if (TryParseVersion(candidateVersion, out var candidate) &&
            TryParseVersion(baseVersion, out var current) &&
            candidate != null && current != null)
        {
            return candidate > current;
        }

        return string.Compare(candidateVersion, baseVersion, StringComparison.OrdinalIgnoreCase) > 0;
    }

    /// <summary>
    /// Downloads the update package into the destination folder securely.
    /// Verifies version number, enforces HTTPS, validates SHA-256 checksum,
    /// and avoids downloading again if a valid package is already cached.
    /// </summary>
    public async Task<string> DownloadUpdateAsync(
        UpdateInfo update,
        string destinationDir,
        IProgress<double>? progress = null,
        CancellationToken cancellationToken = default)
    {
        // 1. Verify update version before downloading
        if (!IsVersionGreater(update.Version, CurrentVersionString))
        {
            throw new InvalidOperationException($"Cannot download or install update: Version {update.Version} is not newer than current version {CurrentVersionString}.");
        }

        // 2. Validate download URL security
        bool isLocalFile = File.Exists(update.DownloadUrl);
        if (!isLocalFile)
        {
            if (!Uri.TryCreate(update.DownloadUrl, UriKind.Absolute, out var downloadUri))
            {
                throw new ArgumentException("Invalid download URL specified in update manifest.");
            }

            // Enforce HTTPS for remote downloads
            if (!downloadUri.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("Insecure download URL rejected. Official updates must use HTTPS.");
            }
        }

        Directory.CreateDirectory(destinationDir);

        var fileName = isLocalFile
            ? Path.GetFileName(update.DownloadUrl)
            : Path.GetFileName(new Uri(update.DownloadUrl).LocalPath);

        if (string.IsNullOrWhiteSpace(fileName))
        {
            fileName = $"CutCraft-Update-{update.Version}.zip";
        }

        var targetFile = Path.Combine(destinationDir, fileName);

        // 3. Avoid downloading repeatedly if valid package already exists
        bool isTargetSameAsSource = isLocalFile &&
            string.Equals(Path.GetFullPath(targetFile), Path.GetFullPath(update.DownloadUrl), StringComparison.OrdinalIgnoreCase);

        if (File.Exists(targetFile) && !isTargetSameAsSource)
        {
            bool isCachedValid = true;
            if (update.FileSizeBytes > 0)
            {
                var fi = new FileInfo(targetFile);
                if (fi.Length != update.FileSizeBytes)
                {
                    isCachedValid = false;
                }
            }

            if (isCachedValid && !string.IsNullOrWhiteSpace(update.Sha256Checksum))
            {
                var existingChecksum = ComputeSha256(targetFile);
                if (!string.Equals(existingChecksum, update.Sha256Checksum, StringComparison.OrdinalIgnoreCase))
                {
                    isCachedValid = false;
                }
            }

            if (isCachedValid)
            {
                progress?.Report(1.0);
                return targetFile;
            }
            else
            {
                try { File.Delete(targetFile); } catch { }
            }
        }

        // 4. Download file atomically via .tmp file
        var tempFile = targetFile + ".tmp";
        if (File.Exists(tempFile))
        {
            try { File.Delete(tempFile); } catch { }
        }

        if (isLocalFile)
        {
            File.Copy(update.DownloadUrl, tempFile, true);
            progress?.Report(1.0);
        }
        else
        {
            using var response = await _httpClient.GetAsync(update.DownloadUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            response.EnsureSuccessStatusCode();

            var totalBytes = response.Content.Headers.ContentLength ?? (update.FileSizeBytes > 0 ? update.FileSizeBytes : -1L);
            await using var contentStream = await response.Content.ReadAsStreamAsync(cancellationToken);
            await using var fileStream = new FileStream(tempFile, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true);

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
        }

        // 5. Verify integrity (SHA-256 Checksum)
        if (!string.IsNullOrWhiteSpace(update.Sha256Checksum))
        {
            var actualChecksum = ComputeSha256(tempFile);
            if (!string.Equals(actualChecksum, update.Sha256Checksum, StringComparison.OrdinalIgnoreCase))
            {
                try { File.Delete(tempFile); } catch { }
                throw new InvalidOperationException($"Security error: Downloaded update SHA-256 checksum mismatch. Expected: {update.Sha256Checksum}, Actual: {actualChecksum}. Installation aborted.");
            }
        }

        if (File.Exists(targetFile))
        {
            File.Delete(targetFile);
        }
        File.Move(tempFile, targetFile);

        progress?.Report(1.0);
        return targetFile;
    }

    /// <summary>
    /// Computes the SHA-256 hash of a file as an uppercase hex string.
    /// </summary>
    public static string ComputeSha256(string filePath)
    {
        using var sha256 = SHA256.Create();
        using var stream = File.OpenRead(filePath);
        var hash = sha256.ComputeHash(stream);
        return Convert.ToHexString(hash);
    }

    /// <summary>
    /// Launches the updater batch script to wait for the host process, replace application binaries, and restart.
    /// </summary>
    public bool ApplyUpdate(string updateZipPath, bool restartApp = true, int? targetPid = null)
    {
        try
        {
            if (!File.Exists(updateZipPath))
            {
                return false;
            }

            var appDir = AppDomain.CurrentDomain.BaseDirectory;
            var scriptPath = Path.Combine(appDir, "updater.bat");
            var pid = targetPid ?? Process.GetCurrentProcess().Id;
            var restartFlag = restartApp ? "1" : "0";

            if (!File.Exists(scriptPath))
            {
                // Generate robust updater script if it doesn't exist
                var scriptContent = @$"@echo off
setlocal enabledelayedexpansion
title CutCraft Auto-Updater
echo ========================================================
echo               CutCraft Auto-Updater
echo ========================================================
set ""APP_DIR=%~dp0""
set ""EXE_NAME=CutCraft.exe""
set ""UPDATE_PACKAGE=%~1""
set ""TARGET_PID=%~2""
set ""DO_RESTART=%~3""

if ""!UPDATE_PACKAGE!""=="""" set ""UPDATE_PACKAGE={updateZipPath}""
if ""!TARGET_PID!""=="""" set ""TARGET_PID={pid}""
if ""!DO_RESTART!""=="""" set ""DO_RESTART={restartFlag}""

echo [1/3] Waiting for CutCraft to close...
:waitloop
timeout /t 1 /nobreak >nul
if not ""!TARGET_PID!""=="""" (
    tasklist /fi ""PID eq !TARGET_PID!"" 2>nul | find ""!TARGET_PID!"" >nul
    if !ERRORLEVEL! EQU 0 goto waitloop
)

echo [2/3] Extracting update package: !UPDATE_PACKAGE!...
tar -xf ""!UPDATE_PACKAGE!"" -C ""!APP_DIR!""
if !ERRORLEVEL! NEQ 0 (
    echo [INFO] Falling back to PowerShell extraction...
    powershell -NoProfile -Command ""Expand-Archive -Path '!UPDATE_PACKAGE!' -DestinationPath '!APP_DIR!' -Force""
)

if exist ""!UPDATE_PACKAGE!"" del ""!UPDATE_PACKAGE!"" >nul 2>&1

echo [3/3] Relaunching CutCraft...
if ""!DO_RESTART!""==""1"" (
    if exist ""!APP_DIR!!EXE_NAME!"" (
        start """" ""!APP_DIR!!EXE_NAME!""
    )
)
echo CutCraft update complete!
exit /b 0
";
                File.WriteAllText(scriptPath, scriptContent);
            }

            var startInfo = new ProcessStartInfo
            {
                FileName = scriptPath,
                Arguments = $"\"{updateZipPath}\" {pid} {restartFlag}",
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
