using CutCraft.Core.Models;

namespace CutCraft.Core.Commands;

public class RemoveClipCommand : ICommand
{
    private readonly Track _track;
    private readonly Clip _clip;
    private readonly int _originalIndex;

    public string Description => $"Remove clip '{_clip.Name}' from track '{_track.Name}'";
    public string AccessibilityAnnouncement => $"Removed clip {_clip.Name} from track {_track.Name}";
    public bool CanUndo => true;

    public RemoveClipCommand(Track track, Clip clip)
    {
        _track = track;
        _clip = clip;
        _originalIndex = track.Clips.IndexOf(clip);
    }

    public void Execute()
    {
        _track.Clips.Remove(_clip);
    }

    public void Unexecute()
    {
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
