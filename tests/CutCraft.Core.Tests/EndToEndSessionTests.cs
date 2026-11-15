using System.IO;
using CutCraft.Core.Commands;
using CutCraft.Core.Models;
using CutCraft.Core.Serialization;
using CutCraft.Core.Timeline;
using CutCraft.Media.Export;
using Xunit;

namespace CutCraft.Core.Tests;

public class EndToEndSessionTests
{
    [Fact]
    public void FullVideoEditingWorkflow_PerformsAllOperationsCleanly()
    {
        var tempIntro = Path.Combine(Path.GetTempPath(), "test_intro.mp4");
        var tempAction = Path.Combine(Path.GetTempPath(), "test_action.mp4");
        var tempMusic = Path.Combine(Path.GetTempPath(), "test_music.mp3");
        File.WriteAllText(tempIntro, "intro");
        File.WriteAllText(tempAction, "action");
        File.WriteAllText(tempMusic, "music");

        try
        {
            // 1. Create a Project
            var project = Project.CreateDefault("Master Documentary");
            var videoTrack = project.Tracks[0];
            var audioTrack = project.Tracks[1];
            var cmdManager = new CommandManager();
            var timelineEngine = new TimelineEngine { SnapThresholdSeconds = 0.25 };

            // 2. Add Clip 1 (10s)
            var clip1 = new Clip
            {
                Name = "IntroScene.mp4",
                AssetPath = tempIntro,
                InPoint = TimeSpan.Zero,
                OutPoint = TimeSpan.FromSeconds(10),
                TimelineStart = TimeSpan.Zero,
                Volume = 1.0,
                Speed = 1.0
            };
            cmdManager.Execute(new AddClipCommand(videoTrack, clip1));
            Assert.Single(videoTrack.Clips);
            Assert.Equal(TimeSpan.FromSeconds(10), project.Duration);

            // 3. Add Clip 2 (8s)
            var clip2 = new Clip
            {
                Name = "MainAction.mp4",
                AssetPath = tempAction,
                InPoint = TimeSpan.Zero,
                OutPoint = TimeSpan.FromSeconds(8),
                TimelineStart = TimeSpan.FromSeconds(10),
                Volume = 0.9,
                Speed = 1.0
            };
            cmdManager.Execute(new AddClipCommand(videoTrack, clip2));
            Assert.Equal(2, videoTrack.Clips.Count);
            Assert.Equal(TimeSpan.FromSeconds(18), project.Duration);

            // 4. Split Clip 1 at 4.0 seconds
            var splitCmd = new SplitClipCommand(videoTrack, clip1, TimeSpan.FromSeconds(4.0));
            cmdManager.Execute(splitCmd);
            Assert.Equal(3, videoTrack.Clips.Count);
            var part1 = videoTrack.Clips[0];
            var part2 = videoTrack.Clips[1];
            Assert.Equal(TimeSpan.FromSeconds(4.0), part1.TimelineDuration);
            Assert.Equal(TimeSpan.FromSeconds(6.0), part2.TimelineDuration);

            // 5. Trim Part 2 in-point by 1.0 second
            var trimCmd = new TrimClipCommand(part2, TrimEdge.Start, TimeSpan.FromSeconds(5.0));
            cmdManager.Execute(trimCmd);
            Assert.Equal(TimeSpan.FromSeconds(5.0), part2.TimelineDuration);

            // 6. Set Audio Fade-In and Fade-Out on Part 1
            var fadeCmd = new SetAudioFadeCommand(part1, 1.0, 1.5);
            cmdManager.Execute(fadeCmd);
            Assert.Equal(1.0, part1.AudioFadeIn);
            Assert.Equal(1.5, part1.AudioFadeOut);

            // 7. Apply Transformation to Part 2 (90 degree rotation & horizontal flip)
            var rotCmd = new RotateClipCommand(part2, 90);
            cmdManager.Execute(rotCmd);
            Assert.Equal(90, part2.Rotation);

            var flipCmd = new ToggleClipFlipCommand(part2, horizontal: true);
            cmdManager.Execute(flipCmd);
            Assert.True(part2.FlipHorizontal);

            // 8. Apply Ken Burns Pan & Zoom
            var pzCmd = new SetPanZoomCommand(part2, PanZoomType.ZoomIn);
            cmdManager.Execute(pzCmd);
            Assert.Equal(PanZoomType.ZoomIn, part2.PanZoom);

            // 9. Add Background Music Track
            var bgMusic = new Clip
            {
                Name = "Soundtrack.mp3",
                AssetPath = tempMusic,
                InPoint = TimeSpan.Zero,
                OutPoint = TimeSpan.FromSeconds(15),
                TimelineStart = TimeSpan.Zero,
                Volume = 0.5
            };
            cmdManager.Execute(new AddClipCommand(audioTrack, bgMusic));
            Assert.Single(audioTrack.Clips);

            // 10. Add Markers
            var m1 = new TimelineMarker(TimeSpan.FromSeconds(4.0), "Scene Transition");
            var m2 = new TimelineMarker(TimeSpan.FromSeconds(12.0), "Climax Beat");
            cmdManager.Execute(new AddMarkerCommand(project, m1));
            cmdManager.Execute(new AddMarkerCommand(project, m2));
            Assert.Equal(2, project.Markers.Count);

            // 11. Test Magnetic Snapping to Markers & Edges
            var snapToMarker = timelineEngine.SnapTime(project, TimeSpan.FromSeconds(3.9));
            Assert.Equal(TimeSpan.FromSeconds(4.0), snapToMarker);

            var snapToClimax = timelineEngine.SnapTime(project, TimeSpan.FromSeconds(12.1));
            Assert.Equal(TimeSpan.FromSeconds(12.0), snapToClimax);

            // 12. Add Captions
            project.Captions.Add(new CaptionItem(1, TimeSpan.FromSeconds(0.5), TimeSpan.FromSeconds(3.5), "In a world of sound and vision..."));
            project.Captions.Add(new CaptionItem(2, TimeSpan.FromSeconds(4.0), TimeSpan.FromSeconds(8.0), "The story unfolds."));
            Assert.Equal(2, project.Captions.Count);

            // 13. Serialize to JSON and Reload, asserting roundtrip fidelity
            var json = ProjectSerializer.Serialize(project);
            var reloaded = ProjectSerializer.Deserialize(json);
            Assert.NotNull(reloaded);
            Assert.Equal(project.Name, reloaded.Name);
            Assert.Equal(project.Tracks.Count, reloaded.Tracks.Count);
            Assert.Equal(project.Markers.Count, reloaded.Markers.Count);
            Assert.Equal(project.Captions.Count, reloaded.Captions.Count);

            var reloadedPart2 = reloaded.Tracks[0].Clips[1];
            Assert.Equal(90, reloadedPart2.Rotation);
            Assert.True(reloadedPart2.FlipHorizontal);
            Assert.Equal(PanZoomType.ZoomIn, reloadedPart2.PanZoom);

            // 14. Test FFmpeg Export Command Generation with Audio Ducking & Loudness Normalization
            var tempOutMp4 = Path.Combine(Path.GetTempPath(), "session_export.mp4");
            var exportSettings = new ExportSettings
            {
                OutputPath = tempOutMp4,
                Resolution = ExportResolution.Fhd1080p,
                EnableAudioDucking = true,
                NormalizeAudio = true
            };

            var builder = new FFmpegFilterGraphBuilder();
            var exportCmd = builder.BuildExportCommand(reloaded, exportSettings);
            Assert.NotNull(exportCmd);
            Assert.Contains("transpose=1", exportCmd.FilterComplex);
            Assert.Contains("hflip", exportCmd.FilterComplex);
            Assert.Contains("zoompan=", exportCmd.FilterComplex);
            Assert.Contains("sidechaincompress=", exportCmd.FilterComplex);
            Assert.Contains("loudnorm=I=-16", exportCmd.FilterComplex);

            // 15. Test Multiple Undo and Redo cycles
            Assert.True(cmdManager.CanUndo);
            cmdManager.Undo(); // undo marker 2
            Assert.Single(project.Markers);

            cmdManager.Undo(); // undo marker 1
            Assert.Empty(project.Markers);

            cmdManager.Undo(); // undo add music
            Assert.Empty(audioTrack.Clips);

            cmdManager.Undo(); // undo pan zoom
            Assert.Equal(PanZoomType.None, part2.PanZoom);

            cmdManager.Undo(); // undo flip
            Assert.False(part2.FlipHorizontal);

            cmdManager.Undo(); // undo rotation
            Assert.Equal(0, part2.Rotation);

            // Redo all
            cmdManager.Redo(); // redo rotation
            Assert.Equal(90, part2.Rotation);

            cmdManager.Redo(); // redo flip
            Assert.True(part2.FlipHorizontal);

            cmdManager.Redo(); // redo pan zoom
            Assert.Equal(PanZoomType.ZoomIn, part2.PanZoom);

            cmdManager.Redo(); // redo add music
            Assert.Single(audioTrack.Clips);

            cmdManager.Redo(); // redo marker 1
            Assert.Single(project.Markers);

            cmdManager.Redo(); // redo marker 2
            Assert.Equal(2, project.Markers.Count);
        }
        finally
        {
            try { File.Delete(tempIntro); } catch { }
            try { File.Delete(tempAction); } catch { }
            try { File.Delete(tempMusic); } catch { }
        }
    }
}
