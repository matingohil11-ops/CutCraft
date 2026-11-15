using CutCraft.Core.Models;

namespace CutCraft.Core.Commands;

public class SplitClipCommand : ICommand
{
    private readonly Track _track;
    private readonly Clip _originalClip;
    private readonly TimeSpan _splitTimelineTime;
    private Clip? _leftClip;
    private Clip? _rightClip;
    private int _originalIndex = -1;

    public string Description => $"Split clip '{_originalClip.Name}' at {_splitTimelineTime:mm\\:ss\\.fff}";
    public string AccessibilityAnnouncement => $"Split clip '{_originalClip.Name}' at {_splitTimelineTime:mm\\:ss\\.fff} into two parts";
    public bool CanUndo => true;

    public SplitClipCommand(Track track, Clip clip, TimeSpan splitTimelineTime)
    {
        _track = track;
        _originalClip = clip;
        _splitTimelineTime = splitTimelineTime;
    }

    public void Execute()
    {
        if (_splitTimelineTime <= _originalClip.TimelineStart || _splitTimelineTime >= _originalClip.TimelineEnd)
        {
            throw excitingInvalidOperation($"Split time {_splitTimelineTime} must be strictly inside clip bounds ({_originalClip.TimelineStart} - {_originalClip.TimelineEnd})");
        }

        _originalIndex = _track.Clips.IndexOf(_originalClip);
        if (_originalIndex < 0) return;

        var relativeSeconds = (_splitTimelineTime - _originalClip.TimelineStart).TotalSeconds * _originalClip.Speed;
        var sourceSplitPoint = _originalClip.InPoint + TimeSpan.FromSeconds(relativeSeconds);

        _leftClip = _originalClip.Clone();
        _leftClip.OutPoint = sourceSplitPoint;
        _leftClip.Name = $"{_originalClip.Name} (Part 1)";

        _rightClip = _originalClip.Clone();
        _rightClip.InPoint = sourceSplitPoint;
        _rightClip.TimelineStart = _splitTimelineTime;
        _rightClip.Name = $"{_originalClip.Name} (Part 2)";

        _track.Clips.RemoveAt(_originalIndex);
        _track.Clips.Insert(_originalIndex, _leftClip);
        _track.Clips.Insert(_originalIndex + 1, _rightClip);
    }

    public void Unexecute()
    {
        if (_leftClip != null) _track.Clips.Remove(_leftClip);
        if (_rightClip != null) _track.Clips.Remove(_rightClip);
        if (_originalIndex >= 0 && _originalIndex <= _track.Clips.Count)
        {
            _track.Clips.Insert(_originalIndex, _originalClip);
        }
        else
        {
            _track.Clips.Add(_originalClip);
        }
    }

    private static InvalidOperationException excitingInvalidOperation(string message) => new(message);
}
