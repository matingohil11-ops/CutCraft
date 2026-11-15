using CutCraft.Core.Models;

namespace CutCraft.Core.Commands;

public class MoveClipCommand : ICommand
{
    private readonly Track _sourceTrack;
    private readonly Track _targetTrack;
    private readonly Clip _clip;
    private readonly TimeSpan _newStartTime;
    private readonly TimeSpan _previousStartTime;
    private readonly int _previousIndex;

    public string Description => $"Move clip '{_clip.Name}' to {_newStartTime:mm\\:ss\\.fff}";
    public string AccessibilityAnnouncement => $"Moved clip '{_clip.Name}' to {_newStartTime:mm\\:ss\\.fff} on track '{_targetTrack.Name}'";
    public bool CanUndo => true;

    public MoveClipCommand(Track sourceTrack, Track targetTrack, Clip clip, TimeSpan newStartTime)
    {
        _sourceTrack = sourceTrack;
        _targetTrack = targetTrack;
        _clip = clip;
        _newStartTime = newStartTime;
        _previousStartTime = clip.TimelineStart;
        _previousIndex = sourceTrack.Clips.IndexOf(clip);
    }

    public void Execute()
    {
        if (_sourceTrack != _targetTrack)
        {
            _sourceTrack.Clips.Remove(_clip);
            _targetTrack.Clips.Add(_clip);
        }

        _clip.TimelineStart = _newStartTime;
    }

    public void Unexecute()
    {
        if (_sourceTrack != _targetTrack)
        {
            _targetTrack.Clips.Remove(_clip);
            if (_previousIndex >= 0 && _previousIndex <= _sourceTrack.Clips.Count)
                _sourceTrack.Clips.Insert(_previousIndex, _clip);
            else
                _sourceTrack.Clips.Add(_clip);
        }

        _clip.TimelineStart = _previousStartTime;
    }
}
