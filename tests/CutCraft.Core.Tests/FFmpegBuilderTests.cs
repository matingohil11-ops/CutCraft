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

    [Fact]
    public void BuildExportCommand_WithMusicAtArbitraryPoint_AppliesAudioDelayAndMixing()
    {
        var project = Project.CreateDefault("Music Export Test");
        var videoTrack = project.Tracks[0];
        var audioTrack = project.Tracks[1];

        var tempVideo = Path.Combine(Path.GetTempPath(), "test_vid_bgm.mp4");
        var tempAudio = Path.Combine(Path.GetTempPath(), "test_music_bgm.mp3");
        File.WriteAllText(tempVideo, "dummy-video");
        File.WriteAllText(tempAudio, "dummy-audio");

        try
        {
            videoTrack.Clips.Add(new Clip
            {
                Name = "Base Video",
                AssetPath = tempVideo,
                InPoint = TimeSpan.Zero,
                OutPoint = TimeSpan.FromSeconds(30),
                TimelineStart = TimeSpan.Zero
            });

            // Music starting at 12.5 seconds
            var musicStart = TimeSpan.FromSeconds(12.5);
            audioTrack.Clips.Add(new Clip
            {
                Name = "Music Clip",
                AssetPath = tempAudio,
                Type = AssetType.Audio,
                InPoint = TimeSpan.Zero,
                OutPoint = TimeSpan.FromSeconds(10),
                TimelineStart = musicStart,
                Volume = 0.75
            });

            var builder = new FFmpegFilterGraphBuilder();
            var settings = new ExportSettings
            {
                OutputPath = Path.Combine(Path.GetTempPath(), "music_out.mp4"),
                Resolution = ExportResolution.Hd720p,
                Quality = ExportQuality.Draft
            };

            var command = builder.BuildExportCommand(project, settings);

            Assert.NotNull(command);
            Assert.Equal(2, command.InputFiles.Count);
            // Delay 12.5s = 12500ms
            Assert.Contains("adelay=12500|12500", command.FilterComplex);
            Assert.Contains("volume=0.75", command.FilterComplex);
            Assert.Contains("amix=inputs=2", command.FilterComplex);
        }
        finally
        {
            try { File.Delete(tempVideo); } catch { }
            try { File.Delete(tempAudio); } catch { }
        }
    }
}
