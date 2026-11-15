using CutCraft.Core.Commands;
using CutCraft.Core.Models;
using CutCraft.Core.Timeline;
using CutCraft.Media.Export;
using Xunit;

namespace CutCraft.Core.Tests;

public class Phase8To11Tests
{
    [Fact]
    public void SetAudioFadeCommand_AppliesFadeInAndOutWithUndo()
    {
        var clip = new Clip
        {
            Name = "Track.wav",
            InPoint = TimeSpan.Zero,
            OutPoint = TimeSpan.FromSeconds(10),
            AudioFadeIn = 0.0,
            AudioFadeOut = 0.0
        };

        var commandManager = new CommandManager();
        var fadeCmd = new SetAudioFadeCommand(clip, 1.5, 2.0);

        commandManager.Execute(fadeCmd);
        Assert.Equal(1.5, clip.AudioFadeIn);
        Assert.Equal(2.0, clip.AudioFadeOut);

        commandManager.Undo();
        Assert.Equal(0.0, clip.AudioFadeIn);
        Assert.Equal(0.0, clip.AudioFadeOut);
    }

    [Fact]
    public void RotateClipCommand_RotatesClockwiseWithUndo()
    {
        var clip = new Clip { Name = "Landscape.mp4", Rotation = 0 };
        var commandManager = new CommandManager();

        var rotCmd = new RotateClipCommand(clip); // defaults to +90
        commandManager.Execute(rotCmd);
        Assert.Equal(90, clip.Rotation);

        var rotCmd2 = new RotateClipCommand(clip);
        commandManager.Execute(rotCmd2);
        Assert.Equal(180, clip.Rotation);

        commandManager.Undo();
        Assert.Equal(90, clip.Rotation);

        commandManager.Undo();
        Assert.Equal(0, clip.Rotation);
    }

    [Fact]
    public void ToggleClipFlipCommand_TogglesHorizontalAndVerticalFlips()
    {
        var clip = new Clip { Name = "FaceCam.mp4", FlipHorizontal = false, FlipVertical = false };
        var commandManager = new CommandManager();

        var hFlipCmd = new ToggleClipFlipCommand(clip, horizontal: true);
        commandManager.Execute(hFlipCmd);
        Assert.True(clip.FlipHorizontal);

        var vFlipCmd = new ToggleClipFlipCommand(clip, horizontal: false);
        commandManager.Execute(vFlipCmd);
        Assert.True(clip.FlipVertical);

        commandManager.Undo();
        Assert.False(clip.FlipVertical);

        commandManager.Undo();
        Assert.False(clip.FlipHorizontal);
    }

    [Fact]
    public void SetPanZoomCommand_SetsKenBurnsPresetWithUndo()
    {
        var clip = new Clip { Name = "Photo.jpg", PanZoom = PanZoomType.None };
        var commandManager = new CommandManager();

        var pzCmd = new SetPanZoomCommand(clip, PanZoomType.ZoomIn);
        commandManager.Execute(pzCmd);
        Assert.Equal(PanZoomType.ZoomIn, clip.PanZoom);

        commandManager.Undo();
        Assert.Equal(PanZoomType.None, clip.PanZoom);
    }

    [Fact]
    public void FFmpegFilterGraphBuilder_GeneratesTransformAndAudioFadeFilters()
    {
        var project = Project.CreateDefault("Transform Test");
        var tempFile = Path.Combine(Path.GetTempPath(), "dummy_transform.mp4");
        File.WriteAllText(tempFile, "fake-content");

        try
        {
            var clip = new Clip
            {
                Name = "TransformDemo",
                AssetPath = tempFile,
                InPoint = TimeSpan.Zero,
                OutPoint = TimeSpan.FromSeconds(5),
                TimelineStart = TimeSpan.Zero,
                Rotation = 90,
                FlipHorizontal = true,
                PanZoom = PanZoomType.ZoomIn,
                AudioFadeIn = 1.0,
                AudioFadeOut = 1.5
            };
            project.Tracks[0].Clips.Add(clip);

            var builder = new FFmpegFilterGraphBuilder();
            var settings = new ExportSettings
            {
                OutputPath = Path.Combine(Path.GetTempPath(), "out_transform.mp4"),
                Resolution = ExportResolution.Fhd1080p
            };

            var cmd = builder.BuildExportCommand(project, settings);

            Assert.Contains("transpose=1", cmd.FilterComplex);
            Assert.Contains("hflip", cmd.FilterComplex);
            Assert.Contains("zoompan=z='min(zoom+0.0015,1.3)'", cmd.FilterComplex);
            Assert.Contains("afade=t=in:st=0:d=1.00", cmd.FilterComplex);
            Assert.Contains("afade=t=out:", cmd.FilterComplex);
        }
        finally
        {
            try { File.Delete(tempFile); } catch { }
        }
    }

    [Fact]
    public void FFmpegFilterGraphBuilder_GeneratesAudioDuckingAndNormalization()
    {
        var project = Project.CreateDefault("Ducking & Normalization Test");
        var tempVid = Path.Combine(Path.GetTempPath(), "dummy_speech.mp4");
        var tempMusic = Path.Combine(Path.GetTempPath(), "dummy_bgmusic.mp3");
        File.WriteAllText(tempVid, "v");
        File.WriteAllText(tempMusic, "m");

        try
        {
            // Primary speech audio clip
            project.Tracks[0].Clips.Add(new Clip
            {
                Name = "Speech",
                AssetPath = tempVid,
                InPoint = TimeSpan.Zero,
                OutPoint = TimeSpan.FromSeconds(10),
                TimelineStart = TimeSpan.Zero
            });

            // Music track clip
            project.Tracks[1].Clips.Add(new Clip
            {
                Name = "Music",
                AssetPath = tempMusic,
                InPoint = TimeSpan.Zero,
                OutPoint = TimeSpan.FromSeconds(10),
                TimelineStart = TimeSpan.Zero,
                Volume = 0.8
            });

            var builder = new FFmpegFilterGraphBuilder();
            var settings = new ExportSettings
            {
                OutputPath = Path.Combine(Path.GetTempPath(), "ducked.mp4"),
                EnableAudioDucking = true,
                NormalizeAudio = true
            };

            var cmd = builder.BuildExportCommand(project, settings);

            // Verify sidechain ducking filter is present
            Assert.Contains("sidechaincompress=", cmd.FilterComplex);

            // Verify EBU R128 loudness normalization filter is present
            Assert.Contains("loudnorm=I=-16", cmd.FilterComplex);
        }
        finally
        {
            try { File.Delete(tempVid); File.Delete(tempMusic); } catch { }
        }
    }

    [Fact]
    public void FFmpegFilterGraphBuilder_GeneratesGifAndAudioOnlyPipelines()
    {
        var project = Project.CreateDefault("Format Test");
        var tempFile = Path.Combine(Path.GetTempPath(), "dummy_fmt.mp4");
        File.WriteAllText(tempFile, "f");

        try
        {
            project.Tracks[0].Clips.Add(new Clip
            {
                Name = "FormatClip",
                AssetPath = tempFile,
                InPoint = TimeSpan.Zero,
                OutPoint = TimeSpan.FromSeconds(3),
                TimelineStart = TimeSpan.Zero
            });

            var builder = new FFmpegFilterGraphBuilder();

            // 1. Animated GIF
            var gifSettings = new ExportSettings
            {
                OutputPath = Path.Combine(Path.GetTempPath(), "anim.gif"),
                Format = ExportFormat.Gif
            };
            var gifCmd = builder.BuildExportCommand(project, gifSettings);
            Assert.Contains("palettegen", gifCmd.FilterComplex);
            Assert.Contains("paletteuse", gifCmd.FilterComplex);
            Assert.Empty(gifCmd.AudioMap); // No audio stream in GIF

            // 2. MP3 Audio Only
            var mp3Settings = new ExportSettings
            {
                OutputPath = Path.Combine(Path.GetTempPath(), "audio.mp3"),
                Format = ExportFormat.AudioOnlyMp3
            };
            var mp3Cmd = builder.BuildExportCommand(project, mp3Settings);
            Assert.Empty(mp3Cmd.VideoMap); // No video stream in audio-only
            Assert.Contains("-vn -c:a libmp3lame", mp3Cmd.FullCommandLine);
        }
        finally
        {
            try { File.Delete(tempFile); } catch { }
        }
    }

    [Fact]
    public void TimelineEngine_SnapsToMarkersAndCutPoints()
    {
        var project = Project.CreateDefault("Snapping Test");
        project.Tracks[0].Clips.Add(new Clip
        {
            Name = "Clip1",
            TimelineStart = TimeSpan.FromSeconds(0),
            InPoint = TimeSpan.Zero,
            OutPoint = TimeSpan.FromSeconds(5)
        });
        project.Tracks[0].Clips.Add(new Clip
        {
            Name = "Clip2",
            TimelineStart = TimeSpan.FromSeconds(5),
            InPoint = TimeSpan.Zero,
            OutPoint = TimeSpan.FromSeconds(5)
        });

        // Add a marker at 7.5 seconds
        project.Markers.Add(new TimelineMarker(TimeSpan.FromSeconds(7.5), "Chorus Start"));

        var engine = new TimelineEngine { SnapThresholdSeconds = 0.25 };

        // Test near clip cut point (4.9s -> snaps to 5.0s)
        var snappedCut = engine.SnapTime(project, TimeSpan.FromSeconds(4.9));
        Assert.Equal(TimeSpan.FromSeconds(5.0), snappedCut);

        // Test near marker (7.4s -> snaps to 7.5s)
        var snappedMarker = engine.SnapTime(project, TimeSpan.FromSeconds(7.4));
        Assert.Equal(TimeSpan.FromSeconds(7.5), snappedMarker);

        // Test far from any snap point (2.5s -> stays 2.5s)
        var notSnapped = engine.SnapTime(project, TimeSpan.FromSeconds(2.5));
        Assert.Equal(TimeSpan.FromSeconds(2.5), notSnapped);
    }
}
