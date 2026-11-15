using CutCraft.Core.Demos;
using CutCraft.Core.Models;
using CutCraft.Core.Serialization;
using Xunit;

namespace CutCraft.Core.Tests;

public class DemoProjectTests
{
    [Fact]
    public void CreateSampleProject_CreatesValidProjectAndSavesFile()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "CutCraft_Demo_Test_" + Guid.NewGuid().ToString("N"));
        try
        {
            var project = DemoProjectGenerator.CreateSampleProject(tempDir);

            Assert.NotNull(project);
            Assert.Equal("Welcome to CutCraft", project.Name);
            Assert.Equal(2, project.Tracks.Count);
            Assert.Equal(2, project.Tracks[0].Clips.Count);
            Assert.Single(project.Tracks[1].Clips);
            Assert.Single(project.Overlays);
            Assert.Equal(2, project.Captions.Count);

            var projectFilePath = Path.Combine(tempDir, "sample_project.cutcraft");
            Assert.True(File.Exists(projectFilePath));

            var loaded = ProjectSerializer.LoadFromFile(projectFilePath);
            Assert.Equal(project.Name, loaded.Name);
            Assert.Equal(project.Tracks[0].Clips[1].Effects.Count, loaded.Tracks[0].Clips[1].Effects.Count);
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                try { Directory.Delete(tempDir, true); } catch { }
            }
        }
    }
}
