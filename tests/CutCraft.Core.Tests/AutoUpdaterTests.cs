using System.Text.Json;
using CutCraft.Core.Updates;
using Xunit;

namespace CutCraft.Core.Tests;

public class AutoUpdaterTests
{
    [Fact]
    public void UpdateManager_CurrentVersion_IsZeroPointZero()
    {
        Assert.Equal("0.0", UpdateManager.CurrentVersionString);
        Assert.Equal(new Version(0, 0, 0), UpdateManager.CurrentVersion);
    }

    [Theory]
    [InlineData("0.1", "0.0", true)]
    [InlineData("1.0.0", "0.0", true)]
    [InlineData("0.0.1", "0.0", true)]
    [InlineData("0.0", "0.0", false)]
    [InlineData("0.0.0", "0.0", false)]
    public void IsVersionGreater_CorrectlyEvaluatesVersions(string candidate, string current, bool expected)
    {
        var result = UpdateManager.IsVersionGreater(candidate, current);
        Assert.Equal(expected, result);
    }

    [Fact]
    public async Task CheckForUpdatesAsync_WithLocalManifest_DetectsVersionZeroPointZero()
    {
        var tempFile = Path.GetTempFileName();
        try
        {
            var manifest = new UpdateInfo
            {
                Version = "0.0",
                Name = "CutCraft v0.0",
                ReleaseNotes = "Initial Release v0.0",
                DownloadUrl = "https://github.com/matingohil11-ops/CutCraft/releases/download/v0.0/CutCraft-v0.0.zip"
            };

            await File.WriteAllTextAsync(tempFile, JsonSerializer.Serialize(manifest));

            var updater = new UpdateManager();
            var checkResult = await updater.CheckForUpdatesAsync(tempFile);

            Assert.NotNull(checkResult);
            Assert.False(checkResult.HasUpdate); // 0.0 is current version
            Assert.Equal("0.0", checkResult.CurrentVersion);
            Assert.Contains("up to date", checkResult.Message, StringComparison.OrdinalIgnoreCase);
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
                Version = "0.1",
                Name = "CutCraft v0.1 Preview",
                ReleaseNotes = "New features in v0.1",
                DownloadUrl = "https://github.com/matingohil11-ops/CutCraft/releases/download/v0.1/CutCraft-v0.1.zip"
            };

            await File.WriteAllTextAsync(tempFile, JsonSerializer.Serialize(manifest));

            var updater = new UpdateManager();
            var checkResult = await updater.CheckForUpdatesAsync(tempFile);

            Assert.NotNull(checkResult);
            Assert.True(checkResult.HasUpdate);
            Assert.Equal("0.1", checkResult.LatestVersion);
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
}
