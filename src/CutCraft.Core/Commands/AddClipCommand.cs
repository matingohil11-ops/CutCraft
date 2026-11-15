using CutCraft.Core.Models;

namespace CutCraft.Core.Commands;

public class AddClipCommand : ICommand
{
    private readonly Track _track;
    private readonly Clip _clip;
    private readonly int _insertIndex;

    public string Description => $"Add clip '{_clip.Name}' to track '{_track.Name}'";
    public string AccessibilityAnnouncement => $"Added clip {_clip.Name} at {_clip.TimelineStart:mm\\:ss\\.fff} to track {_track.Name}";
    public bool CanUndo => true;

    public AddClipCommand(Track track, Clip clip, int? insertIndex = null)
    {
        _track = track;
        _clip = clip;
        _insertIndex = insertIndex ?? track.Clips.Count;
    }

    public void Execute()
    {
        if (_insertIndex >= 0 && _insertIndex <= _track.Clips.Count)
        {
            _track.Clips.Insert(_insertIndex, _clip);
        }
        else
        {
            _track.Clips.Add(_clip);
        }
    }

    public void Unexecute()
    {
        _track.Clips.Remove(_clip);
    }
}
