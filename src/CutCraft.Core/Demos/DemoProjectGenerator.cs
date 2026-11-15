using CutCraft.Core.Models;
using CutCraft.Core.Serialization;

namespace CutCraft.Core.Demos;

public static class DemoProjectGenerator
{
    public static Project CreateSampleProject(string projectFolder)
    {
        if (!Directory.Exists(projectFolder))
        {
            Directory.CreateDirectory(projectFolder);
        }

        var project = new Project
        {
            Name = "Welcome to CutCraft",
            CanvasWidth = 1920,
            CanvasHeight = 1080,
            FrameRate = 30.0
        };

        // Track 0: Video Track
        var videoTrack = new Track
        {
            Name = "Main Video",
            Type = TrackType.Video,
            Order = 0
        };

        // Clip 1: Intro
        videoTrack.Clips.Add(new Clip
        {
            Name = "Intro Scene",
            AssetId = "intro-001",
            AssetPath = Path.Combine(projectFolder, "intro.mp4"),
            InPoint = TimeSpan.Zero,
            OutPoint = TimeSpan.FromSeconds(6),
            TimelineStart = TimeSpan.Zero,
            Speed = 1.0,
            Volume = 1.0,
            TransitionIn = new Transition { Type = TransitionType.FadeToBlack, Duration = TimeSpan.FromSeconds(1.0) }
        });

        // Clip 2: Action Shot with Sepia effect
        var clip2 = new Clip
        {
            Name = "Action Shot",
            AssetId = "action-002",
            AssetPath = Path.Combine(projectFolder, "action.mp4"),
            InPoint = TimeSpan.FromSeconds(2),
            OutPoint = TimeSpan.FromSeconds(10),
            TimelineStart = TimeSpan.FromSeconds(6),
            Speed = 1.0,
            Volume = 0.8,
            TransitionIn = new Transition { Type = TransitionType.CrossFade, Duration = TimeSpan.FromSeconds(1.0) }
        };
        clip2.Effects.Add(ClipEffect.CreateSepia());
        videoTrack.Clips.Add(clip2);

        project.Tracks.Add(videoTrack);

        // Track 1: Audio / Music Track
        var audioTrack = new Track
        {
            Name = "Background Music",
            Type = TrackType.Audio,
            Order = 1,
            Volume = 0.7
        };

        audioTrack.Clips.Add(new Clip
        {
            Name = "Acoustic Sunset (Theme)",
            AssetId = "audio-001",
            AssetPath = Path.Combine(projectFolder, "music.mp3"),
            Type = AssetType.Audio,
            InPoint = TimeSpan.Zero,
            OutPoint = TimeSpan.FromSeconds(14),
            TimelineStart = TimeSpan.Zero,
            Volume = 0.6
        });

        project.Tracks.Add(audioTrack);

        // Title Overlays
        project.Overlays.Add(new TextOverlay
        {
            Text = "CutCraft Video Editor",
            FontSize = 54,
            ColorHex = "#FFFFFF",
            StartTime = TimeSpan.FromSeconds(0.5),
            Duration = TimeSpan.FromSeconds(4.0),
            PositionX = 0.5,
            PositionY = 0.4
        });

        // Captions
        project.Captions.Add(new CaptionItem(1, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(4), "Welcome to CutCraft, the accessible C# video editor."));
        project.Captions.Add(new CaptionItem(2, TimeSpan.FromSeconds(6.5), TimeSpan.FromSeconds(10.5), "Built with WPF and .NET 10."));

        // Save project file
        var projectFile = Path.Combine(projectFolder, "sample_project.cutcraft");
        ProjectSerializer.SaveToFile(project, projectFile);

        return project;
    }
}
