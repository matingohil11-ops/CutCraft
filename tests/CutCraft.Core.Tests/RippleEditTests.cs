using CutCraft.Core.Commands;
using CutCraft.Core.Models;
using CutCraft.Core.Timeline;
using Xunit;

namespace CutCraft.Core.Tests;

public class RippleEditTests
{
    [Fact]
    public void RippleDelete_RemovesClip_AndShiftsSubsequentClipsBackward()
    {
        var project = Project.CreateDefault("Ripple Delete Test");
        var track = project.Tracks[0];

        var c1 = new Clip { Name = "Clip 1", TimelineStart = TimeSpan.Zero, InPoint = TimeSpan.Zero, OutPoint = TimeSpan.FromSeconds(4), Speed = 1.0 };
        var c2 = new Clip { Name = "Clip 2", TimelineStart = TimeSpan.FromSeconds(4), InPoint = TimeSpan.Zero, OutPoint = TimeSpan.FromSeconds(4), Speed = 1.0 };
        var c3 = new Clip { Name = "Clip 3", TimelineStart = TimeSpan.FromSeconds(8), InPoint = TimeSpan.Zero, OutPoint = TimeSpan.FromSeconds(4), Speed = 1.0 };

        track.Clips.Add(c1);
        track.Clips.Add(c2);
        track.Clips.Add(c3);

        var cmdManager = new CommandManager();
        var rippleDeleteCmd = new RippleDeleteClipCommand(track, c2);

        // Execute Ripple Delete
        cmdManager.Execute(rippleDeleteCmd);

        Assert.Equal(2, track.Clips.Count);
        Assert.DoesNotContain(c2, track.Clips);
        Assert.Equal(TimeSpan.Zero, c1.TimelineStart);
        // c3 shifted backward by 4 seconds (from 8s to 4s)
        Assert.Equal(TimeSpan.FromSeconds(4), c3.TimelineStart);
        Assert.Equal(TimeSpan.FromSeconds(8), track.Duration);

        // Undo
        cmdManager.Undo();
        Assert.Equal(3, track.Clips.Count);
        Assert.Contains(c2, track.Clips);
        Assert.Equal(TimeSpan.FromSeconds(4), c2.TimelineStart);
        Assert.Equal(TimeSpan.FromSeconds(8), c3.TimelineStart);
    }

    [Fact]
    public void RippleTrimStart_AdjustsInPoint_AndRipplesSubsequentClips()
    {
        var project = Project.CreateDefault("Ripple Trim Start Test");
        var track = project.Tracks[0];

        var c1 = new Clip { Name = "Clip 1", TimelineStart = TimeSpan.Zero, InPoint = TimeSpan.Zero, OutPoint = TimeSpan.FromSeconds(6), Speed = 1.0 };
        var c2 = new Clip { Name = "Clip 2", TimelineStart = TimeSpan.FromSeconds(6), InPoint = TimeSpan.Zero, OutPoint = TimeSpan.FromSeconds(4), Speed = 1.0 };

        track.Clips.Add(c1);
        track.Clips.Add(c2);

        var cmdManager = new CommandManager();
        // Trim Start of c1 from 0 to 2s (shortening c1 from 6s to 4s)
        var trimCmd = new RippleTrimClipCommand(track, c1, TrimEdge.Start, TimeSpan.FromSeconds(2));
        cmdManager.Execute(trimCmd);

        Assert.Equal(TimeSpan.FromSeconds(2), c1.InPoint);
        Assert.Equal(TimeSpan.FromSeconds(4), c1.TimelineDuration);
        // c2 rippled backwards by 2s (from 6s to 4s)
        Assert.Equal(TimeSpan.FromSeconds(4), c2.TimelineStart);

        // Undo
        cmdManager.Undo();
        Assert.Equal(TimeSpan.Zero, c1.InPoint);
        Assert.Equal(TimeSpan.FromSeconds(6), c1.TimelineDuration);
        Assert.Equal(TimeSpan.FromSeconds(6), c2.TimelineStart);
    }

    [Fact]
    public void RippleTrimEnd_AdjustsOutPoint_AndRipplesSubsequentClips()
    {
        var project = Project.CreateDefault("Ripple Trim End Test");
        var track = project.Tracks[0];

        var c1 = new Clip { Name = "Clip 1", TimelineStart = TimeSpan.Zero, InPoint = TimeSpan.Zero, OutPoint = TimeSpan.FromSeconds(6), Speed = 1.0 };
        var c2 = new Clip { Name = "Clip 2", TimelineStart = TimeSpan.FromSeconds(6), InPoint = TimeSpan.Zero, OutPoint = TimeSpan.FromSeconds(4), Speed = 1.0 };

        track.Clips.Add(c1);
        track.Clips.Add(c2);

        var cmdManager = new CommandManager();
        // Trim End of c1 from 6s down to 3s (shortening by 3s)
        var trimCmd = new RippleTrimClipCommand(track, c1, TrimEdge.End, TimeSpan.FromSeconds(3));
        cmdManager.Execute(trimCmd);

        Assert.Equal(TimeSpan.FromSeconds(3), c1.OutPoint);
        Assert.Equal(TimeSpan.FromSeconds(3), c1.TimelineDuration);
        // c2 rippled backwards by 3s (from 6s to 3s)
        Assert.Equal(TimeSpan.FromSeconds(3), c2.TimelineStart);

        // Undo
        cmdManager.Undo();
        Assert.Equal(TimeSpan.FromSeconds(6), c1.OutPoint);
        Assert.Equal(TimeSpan.FromSeconds(6), c2.TimelineStart);
    }

    [Fact]
    public void CloseGap_RemovesBlankSpace_AndShiftsClips()
    {
        var project = Project.CreateDefault("Close Gap Test");
        var track = project.Tracks[0];

        // c1: 0..3s, Gap: 3..6s, c2: 6..10s
        var c1 = new Clip { Name = "Clip 1", TimelineStart = TimeSpan.Zero, InPoint = TimeSpan.Zero, OutPoint = TimeSpan.FromSeconds(3), Speed = 1.0 };
        var c2 = new Clip { Name = "Clip 2", TimelineStart = TimeSpan.FromSeconds(6), InPoint = TimeSpan.Zero, OutPoint = TimeSpan.FromSeconds(4), Speed = 1.0 };

        track.Clips.Add(c1);
        track.Clips.Add(c2);

        var engine = new TimelineEngine();
        var gap = engine.FindGapAt(track, TimeSpan.FromSeconds(4.5));
        Assert.NotNull(gap);
        Assert.Equal(TimeSpan.FromSeconds(3), gap.Start);
        Assert.Equal(TimeSpan.FromSeconds(6), gap.End);
        Assert.Equal(TimeSpan.FromSeconds(3), gap.Duration);

        var cmdManager = new CommandManager();
        var closeGapCmd = new CloseGapCommand(track, gap);
        cmdManager.Execute(closeGapCmd);

        // c2 now starts at 3s
        Assert.Equal(TimeSpan.FromSeconds(3), c2.TimelineStart);
        Assert.Empty(engine.FindGaps(track));

        // Undo
        cmdManager.Undo();
        Assert.Equal(TimeSpan.FromSeconds(6), c2.TimelineStart);
    }

    [Fact]
    public void CloseAllGaps_CondensesTrackSequentially()
    {
        var project = Project.CreateDefault("Close All Gaps Test");
        var track = project.Tracks[0];

        // Gap: 0..2s, c1: 2..5s, Gap: 5..8s, c2: 8..12s
        var c1 = new Clip { Name = "Clip 1", TimelineStart = TimeSpan.FromSeconds(2), InPoint = TimeSpan.Zero, OutPoint = TimeSpan.FromSeconds(3), Speed = 1.0 };
        var c2 = new Clip { Name = "Clip 2", TimelineStart = TimeSpan.FromSeconds(8), InPoint = TimeSpan.Zero, OutPoint = TimeSpan.FromSeconds(4), Speed = 1.0 };

        track.Clips.Add(c1);
        track.Clips.Add(c2);

        var cmdManager = new CommandManager();
        var closeAllCmd = new CloseAllGapsCommand(track);
        cmdManager.Execute(closeAllCmd);

        // c1 condensed to 0..3s, c2 condensed to 3..7s
        Assert.Equal(TimeSpan.Zero, c1.TimelineStart);
        Assert.Equal(TimeSpan.FromSeconds(3), c2.TimelineStart);

        // Undo
        cmdManager.Undo();
        Assert.Equal(TimeSpan.FromSeconds(2), c1.TimelineStart);
        Assert.Equal(TimeSpan.FromSeconds(8), c2.TimelineStart);
    }

    [Fact]
    public void InsertClipRipple_PushesSubsequentClipsForward()
    {
        var project = Project.CreateDefault("Insert Ripple Test");
        var track = project.Tracks[0];

        var c1 = new Clip { Name = "Clip 1", TimelineStart = TimeSpan.Zero, InPoint = TimeSpan.Zero, OutPoint = TimeSpan.FromSeconds(4), Speed = 1.0 };
        var c2 = new Clip { Name = "Clip 2", TimelineStart = TimeSpan.FromSeconds(4), InPoint = TimeSpan.Zero, OutPoint = TimeSpan.FromSeconds(4), Speed = 1.0 };
        track.Clips.Add(c1);
        track.Clips.Add(c2);

        var insertClip = new Clip { Name = "Inserted", InPoint = TimeSpan.Zero, OutPoint = TimeSpan.FromSeconds(3), Speed = 1.0 };

        var cmdManager = new CommandManager();
        // Insert at 4s (between c1 and c2)
        var insertCmd = new InsertClipRippleCommand(track, insertClip, TimeSpan.FromSeconds(4));
        cmdManager.Execute(insertCmd);

        Assert.Equal(3, track.Clips.Count);
        Assert.Equal(TimeSpan.Zero, c1.TimelineStart);
        Assert.Equal(TimeSpan.FromSeconds(4), insertClip.TimelineStart);
        // c2 pushed forward by 3s (from 4s to 7s)
        Assert.Equal(TimeSpan.FromSeconds(7), c2.TimelineStart);

        // Undo
        cmdManager.Undo();
        Assert.Equal(2, track.Clips.Count);
        Assert.DoesNotContain(insertClip, track.Clips);
        Assert.Equal(TimeSpan.FromSeconds(4), c2.TimelineStart);
    }
}
