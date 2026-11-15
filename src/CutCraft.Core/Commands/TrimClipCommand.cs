using CutCraft.Core.Models;

namespace CutCraft.Core.Commands;

public enum TrimEdge
{
    Start,
    End
}

public class TrimClipCommand : ICommand
{
    private readonly Clip _clip;
    private readonly TrimEdge _edge;
    private readonly TimeSpan _newPoint;
    private readonly TimeSpan _previousInPoint;
    private readonly TimeSpan _previousOutPoint;
    private readonly TimeSpan _previousTimelineStart;

    public string Description => $"Trim {_edge} of '{_clip.Name}'";
    public string AccessibilityAnnouncement => $"Trimmed {_edge} of clip '{_clip.Name}' to {_newPoint:mm\\:ss\\.fff}";
    public bool CanUndo => true;

    public TrimClipCommand(Clip clip, TrimEdge edge, TimeSpan newPoint)
    {
        _clip = clip;
        _edge = edge;
        _newPoint = newPoint;
        _previousInPoint = clip.InPoint;
        _previousOutPoint = clip.OutPoint;
        _previousTimelineStart = clip.TimelineStart;
    }

    public void Execute()
    {
        if (_edge == TrimEdge.Start)
        {
            if (_newPoint >= _clip.OutPoint)
                throw new InvalidOperationException("In-point cannot be greater than or equal to Out-point.");

            var delta = (_newPoint - _clip.InPoint).TotalSeconds / Math.Max(0.1, _clip.Speed);
            _clip.InPoint = _newPoint;
            _clip.TimelineStart += TimeSpan.FromSeconds(delta);
        }
        else
        {
            if (_newPoint <= _clip.InPoint)
                throw new InvalidOperationException("Out-point cannot be less than or equal to In-point.");

            _clip.OutPoint = _newPoint;
        }
    }

    public void Unexecute()
    {
        _clip.InPoint = _previousInPoint;
        _clip.OutPoint = _previousOutPoint;
        _clip.TimelineStart = _previousTimelineStart;
    }
}
