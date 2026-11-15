using CutCraft.Core.Commands;
using CutCraft.Core.Models;
using CutCraft.Core.Timeline;
using Xunit;

namespace CutCraft.Core.Tests;

public class TimelineNavigationTests
{
    [Fact]
    public void JKL_Controls_AccelerateForwardAndReverse()
    {
        var controller = new PlaybackNavigationController();

        // Press L once -> 1.0x forward
        controller.PressL();
        Assert.Equal(1.0, controller.PlaybackRate);
        Assert.Equal(NavigationPlaybackDirection.Forward, controller.Direction);

        // Press L twice -> 2.0x
        controller.PressL();
        Assert.Equal(2.0, controller.PlaybackRate);

        // Press L three times -> 4.0x
        controller.PressL();
        Assert.Equal(4.0, controller.PlaybackRate);

        // Press K -> paused
        controller.PressK();
        Assert.Equal(0.0, controller.PlaybackRate);
        Assert.Equal(NavigationPlaybackDirection.Stopped, controller.Direction);

        // Press J once -> -1.0x reverse
        controller.PressJ();
        Assert.Equal(-1.0, controller.PlaybackRate);
        Assert.Equal(NavigationPlaybackDirection.Reverse, controller.Direction);

        // Press J twice -> -2.0x reverse
        controller.PressJ();
        Assert.Equal(-2.0, controller.PlaybackRate);
    }

    [Fact]
    public void EditPointNavigation_JumpsToNextAndPreviousCuts()
    {
        var project = Project.CreateDefault("Cut Nav Test");
        var track = project.Tracks[0];

        // Clip 1: 0s to 5s
        track.Clips.Add(new Clip
        {
            Name = "Clip 1",
            TimelineStart = TimeSpan.Zero,
            InPoint = TimeSpan.Zero,
            OutPoint = TimeSpan.FromSeconds(5)
        });

        // Clip 2: 5s to 12s
        track.Clips.Add(new Clip
        {
            Name = "Clip 2",
            TimelineStart = TimeSpan.FromSeconds(5),
            InPoint = TimeSpan.Zero,
            OutPoint = TimeSpan.FromSeconds(7)
        });

        var engine = new TimelineEngine();
        var controller = new PlaybackNavigationController(engine);

        // Start at 2.0s
        controller.SetPosition(TimeSpan.FromSeconds(2), project);

        // Jump next edit point -> 5.0s (the cut between clip 1 and 2)
        controller.JumpToNextEditPoint(project);
        Assert.Equal(TimeSpan.FromSeconds(5), controller.CurrentPosition);

        // Jump next edit point -> 12.0s (end of clip 2)
        controller.JumpToNextEditPoint(project);
        Assert.Equal(TimeSpan.FromSeconds(12), controller.CurrentPosition);

        // Jump previous edit point -> back to 5.0s
        controller.JumpToPreviousEditPoint(project);
        Assert.Equal(TimeSpan.FromSeconds(5), controller.CurrentPosition);

        // Jump previous edit point -> back to 0.0s
        controller.JumpToPreviousEditPoint(project);
        Assert.Equal(TimeSpan.Zero, controller.CurrentPosition);
    }

    [Fact]
    public void MarkerCommands_AddAndRemoveWithUndo()
    {
        var project = Project.CreateDefault("Marker Test");
        var commandManager = new CommandManager();

        var marker = new TimelineMarker(TimeSpan.FromSeconds(4.5), "Chorus Drop", "#FF0000");

        var addCmd = new AddMarkerCommand(project, marker);
        commandManager.Execute(addCmd);

        Assert.Single(project.Markers);
        Assert.Equal("Chorus Drop", project.Markers[0].Label);

        // Undo
        commandManager.Undo();
        Assert.Empty(project.Markers);

        // Redo
        commandManager.Redo();
        Assert.Single(project.Markers);

        // Remove
        var removeCmd = new RemoveMarkerCommand(project, marker);
        commandManager.Execute(removeCmd);
        Assert.Empty(project.Markers);
    }
}
