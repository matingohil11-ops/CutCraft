using CutCraft.Core.Commands;
using CutCraft.Core.Models;
using CutCraft.Core.Subtitles;
using CutCraft.Media.Export;
using Xunit;

namespace CutCraft.Core.Tests;

public class AdvancedPhasesTests
{
    [Fact]
    public void ChangeClipSpeedCommand_RecalculatesTimelineDurationAndUndos()
    {
        var clip = new Clip
        {
            Name = "SpeedDemo.mp4",
            InPoint = TimeSpan.Zero,
            OutPoint = TimeSpan.FromSeconds(10), // 10s source
            Speed = 1.0
        };

        var commandManager = new CommandManager();

        // 2x Fast motion -> duration should become 5s
        var fastCmd = new ChangeClipSpeedCommand(clip, 2.0);
        commandManager.Execute(fastCmd);

        Assert.Equal(2.0, clip.Speed);
        Assert.Equal(TimeSpan.FromSeconds(5), clip.TimelineDuration);

        // Undo -> back to 1.0x and 10s
        commandManager.Undo();
        Assert.Equal(1.0, clip.Speed);
        Assert.Equal(TimeSpan.FromSeconds(10), clip.TimelineDuration);

        // 0.5x Slow motion -> duration should become 20s
        var slowCmd = new ChangeClipSpeedCommand(clip, 0.5);
        commandManager.Execute(slowCmd);
        Assert.Equal(0.5, clip.Speed);
        Assert.Equal(TimeSpan.FromSeconds(20), clip.TimelineDuration);
    }

    [Fact]
    public void DetachAudioCommand_MutesVideoAndCreatesAudioClipOnAudioTrack()
    {
        var project = Project.CreateDefault("Detach Test");
        var videoTrack = project.Tracks[0];
        var audioTrack = project.Tracks[1];

        var videoClip = new Clip
        {
            Name = "Interview.mp4",
            AssetId = "asset-1",
            AssetPath = "C:/media/interview.mp4",
            InPoint = TimeSpan.FromSeconds(2),
            OutPoint = TimeSpan.FromSeconds(12),
            TimelineStart = TimeSpan.FromSeconds(4),
            Volume = 0.9,
            IsMuted = false
        };
        videoTrack.Clips.Add(videoClip);

        var commandManager = new CommandManager();
        var detachCmd = new DetachAudioCommand(project, videoClip, audioTrack);

        commandManager.Execute(detachCmd);

        Assert.True(videoClip.IsMuted);
        Assert.Single(audioTrack.Clips);
        var generatedAudio = audioTrack.Clips[0];
        Assert.Equal(AssetType.Audio, generatedAudio.Type);
        Assert.Equal(videoClip.TimelineStart, generatedAudio.TimelineStart);
        Assert.Equal(videoClip.TimelineDuration, generatedAudio.TimelineDuration);
        Assert.Equal(0.9, generatedAudio.Volume);

        // Undo detach
        commandManager.Undo();
        Assert.False(videoClip.IsMuted);
        Assert.Empty(audioTrack.Clips);
    }

    [Fact]
    public void SrtSubtitleService_ParsesAndGeneratesSubRipFormat()
    {
        var srtData = @"1
00:00:01,000 --> 00:00:03,500
Welcome to CutCraft!

2
00:00:04,200 --> 00:00:08,000
Building an accessible video editor.
";

        var captions = SrtSubtitleService.ParseSrt(srtData);
        Assert.Equal(2, captions.Count);
        Assert.Equal("Welcome to CutCraft!", captions[0].Text);
        Assert.Equal(TimeSpan.FromSeconds(1), captions[0].StartTime);
        Assert.Equal(TimeSpan.FromMilliseconds(3500), captions[0].EndTime);

        Assert.Equal("Building an accessible video editor.", captions[1].Text);
        Assert.Equal(TimeSpan.FromMilliseconds(4200), captions[1].StartTime);
        Assert.Equal(TimeSpan.FromSeconds(8), captions[1].EndTime);

        // Roundtrip generation
        var generatedSrt = SrtSubtitleService.GenerateSrt(captions);
        Assert.Contains("00:00:01,000 --> 00:00:03,500", generatedSrt);
        Assert.Contains("Welcome to CutCraft!", generatedSrt);
        Assert.Contains("Building an accessible video editor.", generatedSrt);
    }

    [Fact]
    public void FFmpegFilterGraphBuilder_GeneratesXfadeAndPictureInPictureOverlay()
    {
        var project = Project.CreateDefault("Xfade and PiP Test");
        var baseTrack = project.Tracks[0];

        // Create temporary sample files
        var tempBase1 = Path.Combine(Path.GetTempPath(), "base1.mp4");
        var tempBase2 = Path.Combine(Path.GetTempPath(), "base2.mp4");
        var tempPip = Path.Combine(Path.GetTempPath(), "pip.mp4");
        File.WriteAllText(tempBase1, "a");
        File.WriteAllText(tempBase2, "b");
        File.WriteAllText(tempPip, "c");

        // Clip 1
        baseTrack.Clips.Add(new Clip
        {
            Name = "Clip 1",
            AssetPath = tempBase1,
            InPoint = TimeSpan.Zero,
            OutPoint = TimeSpan.FromSeconds(5),
            TimelineStart = TimeSpan.Zero
        });

        // Clip 2 with Crossfade Transition
        baseTrack.Clips.Add(new Clip
        {
            Name = "Clip 2",
            AssetPath = tempBase2,
            InPoint = TimeSpan.Zero,
            OutPoint = TimeSpan.FromSeconds(5),
            TimelineStart = TimeSpan.FromSeconds(5),
            TransitionIn = new Transition { Type = TransitionType.CrossFade, Duration = TimeSpan.FromSeconds(1.0) }
        });

        // PiP Track
        var pipTrack = new Track { Name = "PiP Track", Type = TrackType.Overlay, Order = 2 };
        pipTrack.Clips.Add(new Clip
        {
            Name = "Webcam",
            AssetPath = tempPip,
            InPoint = TimeSpan.Zero,
            OutPoint = TimeSpan.FromSeconds(4),
            TimelineStart = TimeSpan.FromSeconds(1),
            Scale = 0.25,
            PositionX = 0.75,
            PositionY = 0.75
        });
        project.Tracks.Add(pipTrack);

        // Add Captions
        project.Captions.Add(new CaptionItem(1, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(3), "Demo Subtitle"));

        var builder = new FFmpegFilterGraphBuilder();
        var settings = new ExportSettings
        {
            OutputPath = Path.Combine(Path.GetTempPath(), "pip_out.mp4")
        };

        var command = builder.BuildExportCommand(project, settings);

        // Verify xfade is present
        Assert.Contains("xfade=transition=fade", command.FilterComplex);

        // Verify PiP overlay is present
        Assert.Contains("overlay=", command.FilterComplex);

        // Verify Subtitle drawtext is present
        Assert.Contains("Demo Subtitle", command.FilterComplex);

        // Cleanup
        try
        {
            File.Delete(tempBase1);
            File.Delete(tempBase2);
            File.Delete(tempPip);
        }
        catch { }
    }
}
