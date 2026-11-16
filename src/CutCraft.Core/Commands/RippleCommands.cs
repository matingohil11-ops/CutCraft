using CutCraft.Core.Models;
using CutCraft.Core.Timeline;

namespace CutCraft.Core.Commands;

public class RippleDeleteClipCommand : ICommand
{
    private readonly Track _track;
    private readonly Clip _clip;
    private readonly int _originalIndex;
    private readonly TimeSpan _clipStart;
    private readonly TimeSpan _clipDuration;
    private readonly List<(Clip Clip, TimeSpan OriginalStart)> _subsequentClips = new();

    public string Description => $"Ripple delete '{_clip.Name}' from '{_track.Name}'";
    public string AccessibilityAnnouncement => $"Ripple deleted clip '{_clip.Name}', closing gap of {_clipDuration.TotalSeconds:F1}s across {_subsequentClips.Count} subsequent clip(s).";
    public bool CanUndo => true;

    public RippleDeleteClipCommand(Track track, Clip clip)
    {
        _track = track;
        _clip = clip;
        _originalIndex = track.Clips.IndexOf(clip);
        _clipStart = clip.TimelineStart;
        _clipDuration = clip.TimelineDuration;

        foreach (var c in track.Clips)
        {
            if (c != clip && c.TimelineStart >= clip.TimelineEnd - TimeSpan.FromMilliseconds(5))
            {
                _subsequentClips.Add((c, c.TimelineStart));
            }
        }
    }

    public void Execute()
    {
        _track.Clips.Remove(_clip);
        foreach (var (c, _) in _subsequentClips)
        {
            var newStart = c.TimelineStart - _clipDuration;
            c.TimelineStart = newStart < TimeSpan.Zero ? TimeSpan.Zero : newStart;
        }
    }

    public void Unexecute()
    {
        foreach (var (c, origStart) in _subsequentClips)
        {
            c.TimelineStart = origStart;
        }

        if (_originalIndex >= 0 && _originalIndex <= _track.Clips.Count)
        {
            _track.Clips.Insert(_originalIndex, _clip);
        }
        else
        {
            _track.Clips.Add(_clip);
        }
    }
}

public class RippleTrimClipCommand : ICommand
{
    private readonly Track _track;
    private readonly Clip _clip;
    private readonly TrimEdge _edge;
    private readonly TimeSpan _newPoint;
    private readonly TimeSpan _previousInPoint;
    private readonly TimeSpan _previousOutPoint;
    private readonly TimeSpan _previousTimelineStart;
    private readonly List<(Clip Clip, TimeSpan OriginalStart)> _subsequentClips = new();
    private TimeSpan _durationDelta;

    public string Description => $"Ripple trim {_edge} of '{_clip.Name}'";
    public string AccessibilityAnnouncement => $"Ripple trimmed {_edge} of clip '{_clip.Name}' to {_newPoint:mm\\:ss\\.fff}. Shifted {_subsequentClips.Count} following clip(s) by {_durationDelta.TotalSeconds:+#0.00;-#0.00}s.";
    public bool CanUndo => true;

    public RippleTrimClipCommand(Track track, Clip clip, TrimEdge edge, TimeSpan newPoint)
    {
        _track = track;
        _clip = clip;
        _edge = edge;
        _newPoint = newPoint;
        _previousInPoint = clip.InPoint;
        _previousOutPoint = clip.OutPoint;
        _previousTimelineStart = clip.TimelineStart;

        var oldDuration = clip.TimelineDuration;
        TimeSpan newDuration;

        if (edge == TrimEdge.Start)
        {
            var rawDeltaSec = (clip.OutPoint - newPoint).TotalSeconds / Math.Max(0.1, clip.Speed);
            newDuration = TimeSpan.FromSeconds(Math.Max(0.1, rawDeltaSec));
        }
        else
        {
            var rawDeltaSec = (newPoint - clip.InPoint).TotalSeconds / Math.Max(0.1, clip.Speed);
            newDuration = TimeSpan.FromSeconds(Math.Max(0.1, rawDeltaSec));
        }

        _durationDelta = newDuration - oldDuration;

        foreach (var c in track.Clips)
        {
            if (c != clip && c.TimelineStart >= clip.TimelineEnd - TimeSpan.FromMilliseconds(5))
            {
                _subsequentClips.Add((c, c.TimelineStart));
            }
        }
    }

    public void Execute()
    {
        if (_edge == TrimEdge.Start)
        {
            if (_newPoint >= _clip.OutPoint)
                throw new InvalidOperationException("In-point cannot be greater than or equal to Out-point.");

            _clip.InPoint = _newPoint;
        }
        else
        {
            if (_newPoint <= _clip.InPoint)
                throw new InvalidOperationException("Out-point cannot be less than or equal to In-point.");

            _clip.OutPoint = _newPoint;
        }

        foreach (var (c, _) in _subsequentClips)
        {
            var newStart = c.TimelineStart + _durationDelta;
            c.TimelineStart = newStart < TimeSpan.Zero ? TimeSpan.Zero : newStart;
        }
    }

    public void Unexecute()
    {
        _clip.InPoint = _previousInPoint;
        _clip.OutPoint = _previousOutPoint;
        _clip.TimelineStart = _previousTimelineStart;

        foreach (var (c, origStart) in _subsequentClips)
        {
            c.TimelineStart = origStart;
        }
    }
}

public class CloseGapCommand : ICommand
{
    private readonly Track _track;
    private readonly TimelineGap _gap;
    private readonly List<(Clip Clip, TimeSpan OriginalStart)> _shiftedClips = new();

    public string Description => $"Close gap of {_gap.Duration.TotalSeconds:F2}s at {_gap.Start:mm\\:ss}";
    public string AccessibilityAnnouncement => $"Closed gap of {_gap.Duration.TotalSeconds:F2}s at {_gap.Start:mm\\:ss}. Shifted {_shiftedClips.Count} clip(s).";
    public bool CanUndo => true;

    public CloseGapCommand(Track track, TimelineGap gap)
    {
        _track = track;
        _gap = gap;

        foreach (var c in track.Clips)
        {
            if (c.TimelineStart >= gap.End - TimeSpan.FromMilliseconds(5))
            {
                _shiftedClips.Add((c, c.TimelineStart));
            }
        }
    }

    public void Execute()
    {
        var delta = -_gap.Duration;
        foreach (var (c, _) in _shiftedClips)
        {
            var newStart = c.TimelineStart + delta;
            c.TimelineStart = newStart < TimeSpan.Zero ? TimeSpan.Zero : newStart;
        }
    }

    public void Unexecute()
    {
        foreach (var (c, origStart) in _shiftedClips)
        {
            c.TimelineStart = origStart;
        }
    }
}

public class CloseAllGapsCommand : ICommand
{
    private readonly Track _track;
    private readonly List<(Clip Clip, TimeSpan OriginalStart)> _originalPositions = new();

    public string Description => $"Close all blank gaps on '{_track.Name}'";
    public string AccessibilityAnnouncement => $"Closed all gaps on track '{_track.Name}'. Condensed {_originalPositions.Count} clip(s) sequentially.";
    public bool CanUndo => true;

    public CloseAllGapsCommand(Track track)
    {
        _track = track;
        foreach (var c in track.Clips)
        {
            _originalPositions.Add((c, c.TimelineStart));
        }
    }

    public void Execute()
    {
        var current = TimeSpan.Zero;
        foreach (var c in _track.Clips.OrderBy(x => x.TimelineStart))
        {
            c.TimelineStart = current;
            current += c.TimelineDuration;
        }
    }

    public void Unexecute()
    {
        foreach (var (c, origStart) in _originalPositions)
        {
            c.TimelineStart = origStart;
        }
    }
}

public class InsertClipRippleCommand : ICommand
{
    private readonly Track _track;
    private readonly Clip _clipToInsert;
    private readonly TimeSpan _insertPosition;
    private readonly List<(Clip Clip, TimeSpan OriginalStart)> _shiftedClips = new();
    private SplitClipCommand? _splitCommand;

    public string Description => $"Insert '{_clipToInsert.Name}' with ripple at {_insertPosition:mm\\:ss}";
    public string AccessibilityAnnouncement => $"Inserted '{_clipToInsert.Name}' at {_insertPosition:mm\\:ss} and rippled {_shiftedClips.Count} subsequent clip(s).";
    public bool CanUndo => true;

    public InsertClipRippleCommand(Track track, Clip clipToInsert, TimeSpan insertPosition)
    {
        _track = track;
        _clipToInsert = clipToInsert;
        _insertPosition = insertPosition;

        // Check if insertPosition falls strictly inside an existing clip
        var bisectedClip = track.Clips.FirstOrDefault(c => insertPosition > c.TimelineStart && insertPosition < c.TimelineEnd);
        if (bisectedClip != null)
        {
            _splitCommand = new SplitClipCommand(track, bisectedClip, insertPosition);
        }

        foreach (var c in track.Clips)
        {
            if (c.TimelineStart >= insertPosition - TimeSpan.FromMilliseconds(5))
            {
                _shiftedClips.Add((c, c.TimelineStart));
            }
        }
    }

    public void Execute()
    {
        if (_splitCommand != null)
        {
            _splitCommand.Execute();
        }

        _clipToInsert.TimelineStart = _insertPosition;
        if (!_track.Clips.Contains(_clipToInsert))
        {
            _track.Clips.Add(_clipToInsert);
        }

        var delta = _clipToInsert.TimelineDuration;
        foreach (var c in _track.Clips)
        {
            if (c != _clipToInsert && c.TimelineStart >= _insertPosition)
            {
                c.TimelineStart += delta;
            }
        }
    }

    public void Unexecute()
    {
        _track.Clips.Remove(_clipToInsert);

        foreach (var (c, origStart) in _shiftedClips)
        {
            c.TimelineStart = origStart;
        }

        if (_splitCommand != null)
        {
            _splitCommand.Unexecute();
        }
    }
}
