using CutCraft.Core.Commands;
using CutCraft.Core.Models;
using CutCraft.Core.Serialization;
using CutCraft.Core.Timeline;
using Xunit;

namespace CutCraft.Core.Tests;

public class ProjectAndCommandTests
{
    [Fact]
    public void CreateDefaultProject_HasVideoAndAudioTracks()
    {
        var project = Project.CreateDefault("Test Project");

        Assert.NotNull(project);
        Assert.Equal(2, project.Tracks.Count);
        Assert.Contains(project.Tracks, t => t.Type == TrackType.Video);
        Assert.Contains(project.Tracks, t => t.Type == TrackType.Audio);
    }

    [Fact]
    public void AddClipCommand_ExecutesAndUndosProperly()
    {
        var track = new Track { Name = "Main Video", Type = TrackType.Video };
        var clip = new Clip
        {
            Name = "Clip 1",
            InPoint = TimeSpan.Zero,
            OutPoint = TimeSpan.FromSeconds(10),
            TimelineStart = TimeSpan.Zero
        };

        var commandManager = new CommandManager();
        var addCmd = new AddClipCommand(track, clip);

        // Execute
        commandManager.Execute(addCmd);
        Assert.Single(track.Clips);
        Assert.Equal("Clip 1", track.Clips[0].Name);
        Assert.True(commandManager.CanUndo);

        // Undo
        bool undoSuccess = commandManager.Undo();
        Assert.True(undoSuccess);
        Assert.Empty(track.Clips);
        Assert.True(commandManager.CanRedo);

        // Redo
        bool redoSuccess = commandManager.Redo();
        Assert.True(redoSuccess);
        Assert.Single(track.Clips);
    }

    [Fact]
    public void SplitClipCommand_SplitsClipAccuratelyIntoTwoParts()
    {
        var track = new Track { Name = "Main Video", Type = TrackType.Video };
        var clip = new Clip
        {
            Name = "ActionShot.mp4",
            InPoint = TimeSpan.Zero,
            OutPoint = TimeSpan.FromSeconds(20),
            TimelineStart = TimeSpan.FromSeconds(5)
        };
        track.Clips.Add(clip);

        var splitTime = TimeSpan.FromSeconds(12); // 7s into the clip
        var commandManager = new CommandManager();
        var splitCmd = new SplitClipCommand(track, clip, splitTime);

        commandManager.Execute(splitCmd);

        Assert.Equal(2, track.Clips.Count);
        var part1 = track.Clips[0];
        var part2 = track.Clips[1];

        // Part 1 starts at 5s, ends at 12s (duration 7s)
        Assert.Equal(TimeSpan.FromSeconds(5), part1.TimelineStart);
        Assert.Equal(TimeSpan.FromSeconds(7), part1.TimelineDuration);
        Assert.Equal(TimeSpan.FromSeconds(7), part1.OutPoint);

        // Part 2 starts at 12s, ends at 25s (duration 13s)
        Assert.Equal(TimeSpan.FromSeconds(12), part2.TimelineStart);
        Assert.Equal(TimeSpan.FromSeconds(13), part2.TimelineDuration);
        Assert.Equal(TimeSpan.FromSeconds(7), part2.InPoint);
        Assert.Equal(TimeSpan.FromSeconds(20), part2.OutPoint);

        // Undo split
        commandManager.Undo();
        Assert.Single(track.Clips);
        Assert.Equal(clip.Id, track.Clips[0].Id);
        Assert.Equal(TimeSpan.FromSeconds(20), track.Clips[0].TimelineDuration);
    }

    [Fact]
    public void TrimClipCommand_AdjustsInAndOutPointsWithUndo()
    {
        var clip = new Clip
        {
            Name = "Interview.mp4",
            InPoint = TimeSpan.Zero,
            OutPoint = TimeSpan.FromSeconds(30),
            TimelineStart = TimeSpan.FromSeconds(10)
        };

        var commandManager = new CommandManager();

        // Trim In-point to 5 seconds
        var trimStartCmd = new TrimClipCommand(clip, TrimEdge.Start, TimeSpan.FromSeconds(5));
        commandManager.Execute(trimStartCmd);

        Assert.Equal(TimeSpan.FromSeconds(5), clip.InPoint);
        Assert.Equal(TimeSpan.FromSeconds(15), clip.TimelineStart); // Timeline start shifted by 5s
        Assert.Equal(TimeSpan.FromSeconds(25), clip.TimelineDuration);

        // Undo
        commandManager.Undo();
        Assert.Equal(TimeSpan.Zero, clip.InPoint);
        Assert.Equal(TimeSpan.FromSeconds(10), clip.TimelineStart);
    }

    [Fact]
    public void ProjectSerializer_RoundTripsJsonCorrectly()
    {
        var project = Project.CreateDefault("Sample Project");
        project.Tracks[0].Clips.Add(new Clip
        {
            Name = "Clip A",
            AssetPath = "C:/Videos/sample.mp4",
            InPoint = TimeSpan.FromSeconds(1),
            OutPoint = TimeSpan.FromSeconds(11),
            TimelineStart = TimeSpan.Zero,
            Volume = 0.8
        });

        project.Overlays.Add(new TextOverlay
        {
            Text = "Intro Title",
            FontSize = 36,
            ColorHex = "#FFFF00",
            StartTime = TimeSpan.Zero,
            Duration = TimeSpan.FromSeconds(3)
        });

        var json = ProjectSerializer.Serialize(project);
        Assert.NotNull(json);
        Assert.Contains("sample project", json, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Intro Title", json);

        var restored = ProjectSerializer.Deserialize(json);
        Assert.NotNull(restored);
        Assert.Equal(project.Name, restored.Name);
        Assert.Single(restored.Tracks[0].Clips);
        Assert.Equal("Clip A", restored.Tracks[0].Clips[0].Name);
        Assert.Equal(0.8, restored.Tracks[0].Clips[0].Volume);
        Assert.Single(restored.Overlays);
        Assert.Equal("Intro Title", restored.Overlays[0].Text);
    }

    [Fact]
    public void TimelineEngine_CalculatesSnappingToClipEdges()
    {
        var project = Project.CreateDefault("Snap Test");
        var track = project.Tracks[0];
        track.Clips.Add(new Clip
        {
            TimelineStart = TimeSpan.FromSeconds(5),
            InPoint = TimeSpan.Zero,
            OutPoint = TimeSpan.FromSeconds(10) // ends at 15s
        });

        var engine = new TimelineEngine { SnapThresholdSeconds = 0.5 };

        // Test snap to start (5.1s -> snaps to 5.0s)
        var snappedStart = engine.SnapTime(project, TimeSpan.FromSeconds(5.1));
        Assert.Equal(TimeSpan.FromSeconds(5.0), snappedStart);

        // Test snap to end (14.9s -> snaps to 15.0s)
        var snappedEnd = engine.SnapTime(project, TimeSpan.FromSeconds(14.9));
        Assert.Equal(TimeSpan.FromSeconds(15.0), snappedEnd);

        // Test outside threshold (10.0s -> remains 10.0s)
        var noSnap = engine.SnapTime(project, TimeSpan.FromSeconds(10.0));
        Assert.Equal(TimeSpan.FromSeconds(10.0), noSnap);
    }

    [Fact]
    public void AddMusicClip_AtArbitraryTimelinePoint_SetsTimelineStartAndDuration()
    {
        var project = Project.CreateDefault("Music Test");
        var audioTrack = project.Tracks.First(t => t.Type == TrackType.Audio);

        var musicStartPoint = TimeSpan.FromSeconds(15.5);
        var musicClip = new Clip
        {
            Name = "Background Beat.mp3",
            Type = AssetType.Audio,
            TimelineStart = musicStartPoint,
            InPoint = TimeSpan.Zero,
            OutPoint = TimeSpan.FromSeconds(30)
        };

        var commandManager = new CommandManager();
        commandManager.Execute(new AddClipCommand(audioTrack, musicClip));

        Assert.Single(audioTrack.Clips);
        Assert.Equal(musicStartPoint, audioTrack.Clips[0].TimelineStart);
        Assert.Equal(TimeSpan.FromSeconds(45.5), audioTrack.Clips[0].TimelineEnd);
        Assert.Equal(TimeSpan.FromSeconds(45.5), project.Duration);
    }

    [Fact]
    public void MoveClipCommand_MovesAudioClipToAnyPoint()
    {
        var track = new Track { Name = "Audio Track", Type = TrackType.Audio };
        var clip = new Clip
        {
            Name = "Music 1",
            Type = AssetType.Audio,
            TimelineStart = TimeSpan.FromSeconds(5),
            InPoint = TimeSpan.Zero,
            OutPoint = TimeSpan.FromSeconds(20)
        };
        track.Clips.Add(clip);

        var commandManager = new CommandManager();
        var newPoint = TimeSpan.FromSeconds(12.25);
        commandManager.Execute(new MoveClipCommand(track, track, clip, newPoint));

        Assert.Equal(newPoint, clip.TimelineStart);

        commandManager.Undo();
        Assert.Equal(TimeSpan.FromSeconds(5), clip.TimelineStart);
    }
}
