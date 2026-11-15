using CutCraft.Core.Models;
using CutCraft.Media.Export;
using Xunit;

namespace CutCraft.Core.Tests;

public class FFmpegBuilderTests
{
    [Fact]
    public void BuildExportCommand_GeneratesValidComplexFilter()
    {
        var project = Project.CreateDefault("Export Graph Test");
        var videoTrack = project.Tracks[0];

        // Create temporary sample file path
        var tempFile = Path.Combine(Path.GetTempPath(), "test_video_1.mp4");
        File.WriteAllText(tempFile, "dummy");

        videoTrack.Clips.Add(new Clip
        {
            Name = "Clip 1",
            AssetPath = tempFile,
            InPoint = TimeSpan.FromSeconds(2),
            OutPoint = TimeSpan.FromSeconds(8),
            TimelineStart = TimeSpan.Zero,
            Speed = 1.0,
            Volume = 1.0
        });

        // Add brightness effect
        videoTrack.Clips[0].Effects.Add(new ClipEffect(EffectType.Brightness, new Dictionary<string, double> { { "value", 0.1 } }));

        // Add Text Overlay
        project.Overlays.Add(new TextOverlay
        {
            Text = "Welcome to CutCraft",
            FontSize = 32,
            ColorHex = "#FFFFFF",
            StartTime = TimeSpan.Zero,
            Duration = TimeSpan.FromSeconds(4)
        });

        var builder = new FFmpegFilterGraphBuilder();
        var settings = new ExportSettings
        {
            OutputPath = Path.Combine(Path.GetTempPath(), "output.mp4"),
            Resolution = ExportResolution.Fhd1080p,
            Quality = ExportQuality.Standard
        };

        var command = builder.BuildExportCommand(project, settings);

        Assert.NotNull(command);
        Assert.Single(command.InputFiles);
        Assert.Contains(tempFile, command.InputFiles);
        Assert.Contains("trim=start=2.000:end=8.000", command.FilterComplex);
        Assert.Contains("eq=brightness=0.10", command.FilterComplex);
        Assert.Contains("drawtext=text='Welcome to CutCraft'", command.FilterComplex);
        Assert.Contains("libx264", command.FullCommandLine);
        Assert.Contains("aac", command.FullCommandLine);

        // Cleanup
        try { File.Delete(tempFile); } catch { }
    }
}
