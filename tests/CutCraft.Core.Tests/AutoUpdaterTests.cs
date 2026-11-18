using System.Text.Json;
using CutCraft.Core.Updates;
using Xunit;

namespace CutCraft.Core.Tests;

public class AutoUpdaterTests
{
    [Fact]
    public void UpdateManager_CurrentVersion_IsOnePointOnePointTwo()
    {
        Assert.Equal("1.1.2", UpdateManager.CurrentVersionString);
        Assert.Equal(new Version(1, 1, 2), UpdateManager.CurrentVersion);
    }

    [Theory]
    [InlineData("1.2", "1.1.1", true)]
    [InlineData("2.0.0", "1.1.1", true)]
    [InlineData("1.1.2", "1.1.1", true)]
    [InlineData("1.1.1", "1.1.1", false)]
    [InlineData("1.1.0", "1.1.1", false)]
    [InlineData("0.1.0", "1.1.1", false)]
    [InlineData("0.0", "1.1.1", false)]
    [InlineData("0.10.0", "0.9.0", true)]
    [InlineData("v1.2.0", "1.1.1", true)]
    [InlineData("v1.1.1", "1.1.1", false)]
    [InlineData("1.2.0-beta", "1.1.1", true)]
    public void IsVersionGreater_CorrectlyEvaluatesVersions(string candidate, string current, bool expected)
    {
        var result = UpdateManager.IsVersionGreater(candidate, current);
        Assert.Equal(expected, result);
    }

    [Fact]
    public async Task CheckForUpdatesAsync_WithLocalManifest_DetectsVersionOnePointOnePointTwo()
    {
        var tempFile = Path.GetTempFileName();
        try
        {
            var manifest = new UpdateInfo
            {
                Version = "1.1.2",
                Name = "CutCraft v1.1.2",
                ReleaseNotes = "Release v1.1.2",
                DownloadUrl = "https://github.com/matingohil11-ops/CutCraft/releases/download/v1.1.2/CutCraft-v1.1.2-win-x64.zip"
            };

            await File.WriteAllTextAsync(tempFile, JsonSerializer.Serialize(manifest));

            var updater = new UpdateManager();
            var checkResult = await updater.CheckForUpdatesAsync(tempFile);

            Assert.NotNull(checkResult);
            Assert.True(checkResult.IsSuccess);
            Assert.False(checkResult.HasUpdate); // 1.1.2 is current version
            Assert.Equal("1.1.2", checkResult.CurrentVersion);
            Assert.Contains("up to date", checkResult.Message, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("latest version", checkResult.Message, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }

    [Fact]
    public async Task CheckForUpdatesAsync_WithNewerManifest_ReportsUpdateAvailable()
    {
        var tempFile = Path.GetTempFileName();
        try
        {
            var manifest = new UpdateInfo
            {
                Version = "1.2.0",
                Name = "CutCraft v1.2.0 Preview",
                ReleaseNotes = "New features in v1.2",
                DownloadUrl = "https://github.com/matingohil11-ops/CutCraft/releases/download/v1.2.0/CutCraft-v1.2.0.zip"
            };

            await File.WriteAllTextAsync(tempFile, JsonSerializer.Serialize(manifest));

            var updater = new UpdateManager();
            var checkResult = await updater.CheckForUpdatesAsync(tempFile);

            Assert.NotNull(checkResult);
            Assert.True(checkResult.IsSuccess);
            Assert.True(checkResult.HasUpdate);
            Assert.Equal("1.2.0", checkResult.LatestVersion);
            Assert.Contains("Update available", checkResult.Message, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }

    [Fact]
    public async Task CheckForUpdatesAsync_WithProjectManifest_DetectsVersionOnePointOnePointTwo()
    {
        var manifestPath = @"C:\Users\admin\.gemini\antigravity\scratch\CutCraft\update-manifest.json";
        if (File.Exists(manifestPath))
        {
            var updater = new UpdateManager();
            var checkResult = await updater.CheckForUpdatesAsync(manifestPath);

            Assert.NotNull(checkResult);
            Assert.True(checkResult.IsSuccess);
            Assert.False(checkResult.HasUpdate);
            Assert.Equal("1.1.2", checkResult.LatestVersion);
            Assert.Equal("1.1.2", checkResult.CurrentVersion);
            Assert.Contains("up to date", checkResult.Message, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public async Task CheckForUpdatesAsync_WhenOffline_ProperlyReportsNetworkConnectionError()
    {
        var updater = new UpdateManager
        {
            RemoteManifestUrl = "https://127.0.0.1:59999/nonexistent-update-manifest.json",
            LocalFallbackManifestPath = "nonexistent-fallback.json"
        };

        var checkResult = await updater.CheckForUpdatesAsync();

        Assert.NotNull(checkResult);
        Assert.False(checkResult.IsSuccess);
        Assert.True(checkResult.IsNetworkError);
        Assert.False(checkResult.HasUpdate);
        Assert.Contains("internet connection", checkResult.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void UserSettings_DefaultValues_AndPersistence()
    {
        var tempFile = Path.GetTempFileName();
        try
        {
            // Default settings should have AutoCheckUpdatesOnStartup = true
            var settings = new UserSettings();
            Assert.True(settings.AutoCheckUpdatesOnStartup);
            Assert.Null(settings.SkippedVersion);

            // Toggle to disabled and save
            settings.AutoCheckUpdatesOnStartup = false;
            settings.Save(tempFile);

            // Reload and verify persisted value
            var loaded = UserSettings.Load(tempFile);
            Assert.False(loaded.AutoCheckUpdatesOnStartup);
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }

    [Fact]
    public void UserSettings_SkipThisVersion_StoresSkippedVersion()
    {
        var tempFile = Path.GetTempFileName();
        try
        {
            var settings = new UserSettings
            {
                SkippedVersion = "1.2.0"
            };
            settings.Save(tempFile);

            var loaded = UserSettings.Load(tempFile);
            Assert.Equal("1.2.0", loaded.SkippedVersion);
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }

    [Fact]
    public async Task DownloadUpdateAsync_ValidatesVersionBeforeDownload()
    {
        var updater = new UpdateManager();
        var update = new UpdateInfo
        {
            Version = "1.1.2", // Same as current version
            DownloadUrl = "https://example.com/CutCraft.zip"
        };

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await updater.DownloadUpdateAsync(update, Path.GetTempPath());
        });

        Assert.Contains("not newer", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task DownloadUpdateAsync_RejectsInsecureHttpUrl()
    {
        var updater = new UpdateManager();
        var update = new UpdateInfo
        {
            Version = "1.2.0",
            DownloadUrl = "http://example.com/CutCraft-Update.zip" // Insecure HTTP
        };

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await updater.DownloadUpdateAsync(update, Path.GetTempPath());
        });

        Assert.Contains("HTTPS", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task DownloadUpdateAsync_VerifiesSha256Checksum_AndPreventsTamperedFile()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        var sourceFile = Path.Combine(tempDir, "source.zip");
        Directory.CreateDirectory(tempDir);

        try
        {
            await File.WriteAllBytesAsync(sourceFile, new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 });

            var destinationDir = Path.Combine(tempDir, "downloads");
            var updater = new UpdateManager();
            var update = new UpdateInfo
            {
                Version = "1.2.0",
                DownloadUrl = sourceFile,
                Sha256Checksum = "0000000000000000000000000000000000000000000000000000000000000000" // Wrong checksum
            };

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            {
                await updater.DownloadUpdateAsync(update, destinationDir);
            });

            Assert.Contains("checksum mismatch", ex.Message, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, true);
            }
        }
    }

    [Fact]
    public async Task DownloadUpdateAsync_WithValidChecksum_SucceedsAndReportsProgress()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        var sourceFile = Path.Combine(tempDir, "CutCraft-v1.2.0-win-x64.zip");
        Directory.CreateDirectory(tempDir);

        try
        {
            var content = new byte[] { 10, 20, 30, 40, 50, 60, 70, 80 };
            await File.WriteAllBytesAsync(sourceFile, content);
            var expectedChecksum = UpdateManager.ComputeSha256(sourceFile);

            var destinationDir = Path.Combine(tempDir, "downloads");
            var updater = new UpdateManager();
            var update = new UpdateInfo
            {
                Version = "1.2.0",
                DownloadUrl = sourceFile,
                FileSizeBytes = content.Length,
                Sha256Checksum = expectedChecksum
            };

            double lastReportedProgress = 0.0;
            var progress = new Progress<double>(p => lastReportedProgress = p);

            var targetFile = await updater.DownloadUpdateAsync(update, destinationDir, progress);

            Assert.True(File.Exists(targetFile));
            Assert.Equal(expectedChecksum, UpdateManager.ComputeSha256(targetFile));
            Assert.Equal(1.0, lastReportedProgress);
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, true);
            }
        }
    }

    [Fact]
    public async Task DownloadUpdateAsync_PreventsRepeatedDownload_WhenPackageAlreadyCached()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        var sourceFile = Path.Combine(tempDir, "CutCraft-v1.2.0-win-x64.zip");
        Directory.CreateDirectory(tempDir);

        try
        {
            var content = new byte[] { 11, 22, 33, 44, 55, 66 };
            await File.WriteAllBytesAsync(sourceFile, content);
            var expectedChecksum = UpdateManager.ComputeSha256(sourceFile);

            var destinationDir = Path.Combine(tempDir, "downloads");
            var updater = new UpdateManager();
            var update = new UpdateInfo
            {
                Version = "1.2.0",
                DownloadUrl = sourceFile,
                FileSizeBytes = content.Length,
                Sha256Checksum = expectedChecksum
            };

            // First download
            var firstPath = await updater.DownloadUpdateAsync(update, destinationDir);
            var firstWriteTime = File.GetLastWriteTimeUtc(firstPath);

            // Wait brief moment
            await Task.Delay(50);

            // Second download to same folder - should reuse existing valid cached file
            var secondPath = await updater.DownloadUpdateAsync(update, destinationDir);
            var secondWriteTime = File.GetLastWriteTimeUtc(secondPath);

            Assert.Equal(firstPath, secondPath);
            Assert.Equal(firstWriteTime, secondWriteTime);
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, true);
            }
        }
    }
}
