using System.Text.Json;
using CutCraft.Core.Updates;
using Xunit;

namespace CutCraft.Core.Tests;

public class AutoUpdaterTests
{
    [Fact]
    public void UpdateManager_CurrentVersion_IsZeroPointOnePointOne()
    {
        Assert.Equal("0.1.1", UpdateManager.CurrentVersionString);
        Assert.Equal(new Version(0, 1, 1), UpdateManager.CurrentVersion);
    }

    [Theory]
    [InlineData("0.2", "0.1.1", true)]
    [InlineData("1.0.0", "0.1.1", true)]
    [InlineData("0.1.2", "0.1.1", true)]
    [InlineData("0.1.1", "0.1.1", false)]
    [InlineData("0.1.0", "0.1.1", false)]
    [InlineData("0.0", "0.1.1", false)]
    public void IsVersionGreater_CorrectlyEvaluatesVersions(string candidate, string current, bool expected)
    {
        var result = UpdateManager.IsVersionGreater(candidate, current);
        Assert.Equal(expected, result);
    }

    [Fact]
    public async Task CheckForUpdatesAsync_WithLocalManifest_DetectsVersionZeroPointOnePointOne()
    {
        var tempFile = Path.GetTempFileName();
        try
        {
            var manifest = new UpdateInfo
            {
                Version = "0.1.1",
                Name = "CutCraft v0.1.1",
                ReleaseNotes = "Release v0.1.1",
                DownloadUrl = "https://github.com/matingohil11-ops/CutCraft/releases/download/v0.1.1/CutCraft-v0.1.1-win-x64.zip"
            };

            await File.WriteAllTextAsync(tempFile, JsonSerializer.Serialize(manifest));

            var updater = new UpdateManager();
            var checkResult = await updater.CheckForUpdatesAsync(tempFile);

            Assert.NotNull(checkResult);
            Assert.False(checkResult.HasUpdate); // 0.1.1 is current version
            Assert.Equal("0.1.1", checkResult.CurrentVersion);
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
                Version = "0.2.0",
                Name = "CutCraft v0.2.0 Preview",
                ReleaseNotes = "New features in v0.2",
                DownloadUrl = "https://github.com/matingohil11-ops/CutCraft/releases/download/v0.2.0/CutCraft-v0.2.0.zip"
            };

            await File.WriteAllTextAsync(tempFile, JsonSerializer.Serialize(manifest));

            var updater = new UpdateManager();
            var checkResult = await updater.CheckForUpdatesAsync(tempFile);

            Assert.NotNull(checkResult);
            Assert.True(checkResult.HasUpdate);
            Assert.Equal("0.2.0", checkResult.LatestVersion);
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
