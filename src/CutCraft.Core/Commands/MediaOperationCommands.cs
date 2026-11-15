using CutCraft.Core.Models;

namespace CutCraft.Core.Commands;

public class ChangeClipSpeedCommand : ICommand
{
    private readonly Clip _clip;
    private readonly double _newSpeed;
    private readonly double _previousSpeed;

    public string Description => $"Change speed of '{_clip.Name}' to {_newSpeed:0.00}x";
    public string AccessibilityAnnouncement => $"Playback speed of clip '{_clip.Name}' changed to {_newSpeed:0.00} times. Duration is now {_clip.TimelineDuration.TotalSeconds:F1} seconds.";
    public bool CanUndo => true;

    public ChangeClipSpeedCommand(Clip clip, double newSpeed)
    {
        _clip = clip;
        _newSpeed = Math.Clamp(newSpeed, 0.1, 8.0);
        _previousSpeed = clip.Speed;
    }

    public void Execute()
    {
        _clip.Speed = _newSpeed;
    }

    public void Unexecute()
    {
        _clip.Speed = _previousSpeed;
    }
}

public class SetClipTransitionCommand : ICommand
{
    private readonly Clip _clip;
    private readonly bool _isTransitionIn;
    private readonly Transition? _newTransition;
    private readonly Transition? _previousTransition;

    public string Description => $"Set {(_isTransitionIn ? "In" : "Out")} transition on '{_clip.Name}'";
    public string AccessibilityAnnouncement => _newTransition != null
        ? $"Set {_newTransition.Type} transition of {_newTransition.Duration.TotalSeconds:F1}s on '{_clip.Name}'"
        : $"Removed transition on '{_clip.Name}'";
    public bool CanUndo => true;

    public SetClipTransitionCommand(Clip clip, bool isTransitionIn, Transition? newTransition)
    {
        _clip = clip;
        _isTransitionIn = isTransitionIn;
        _newTransition = newTransition;
        _previousTransition = isTransitionIn ? clip.TransitionIn : clip.TransitionOut;
    }

    public void Execute()
    {
        if (_isTransitionIn) _clip.TransitionIn = _newTransition;
        else _clip.TransitionOut = _newTransition;
    }

    public void Unexecute()
    {
        if (_isTransitionIn) _clip.TransitionIn = _previousTransition;
        else _clip.TransitionOut = _previousTransition;
    }
}

public class DetachAudioCommand : ICommand
{
    private readonly Project _project;
    private readonly Clip _videoClip;
    private readonly Track _audioTrack;
    private Clip? _generatedAudioClip;
    private bool _previousMuted;

    public string Description => $"Detach audio from '{_videoClip.Name}'";
    public string AccessibilityAnnouncement => $"Detached audio from '{_videoClip.Name}' into audio track '{_audioTrack.Name}'.";
    public bool CanUndo => true;

    public DetachAudioCommand(Project project, Clip videoClip, Track? targetAudioTrack = null)
    {
        _project = project;
        _videoClip = videoClip;
        _audioTrack = targetAudioTrack ?? project.Tracks.FirstOrDefault(t => t.Type == TrackType.Audio)
                      ?? new Track { Name = "Audio Track", Type = TrackType.Audio };
        _previousMuted = videoClip.IsMuted;
    }

    public void Execute()
    {
        if (!_project.Tracks.Contains(_audioTrack))
        {
            _project.Tracks.Add(_audioTrack);
        }

        _videoClip.IsMuted = true;

        _generatedAudioClip = new Clip
        {
            Name = $"{_videoClip.Name} (Audio)",
            AssetId = _videoClip.AssetId,
            AssetPath = _videoClip.AssetPath,
            Type = AssetType.Audio,
            InPoint = _videoClip.InPoint,
            OutPoint = _videoClip.OutPoint,
            TimelineStart = _videoClip.TimelineStart,
            Speed = _videoClip.Speed,
            Volume = _videoClip.Volume
        };

        _audioTrack.Clips.Add(_generatedAudioClip);
    }

    public void Unexecute()
    {
        _videoClip.IsMuted = _previousMuted;
        if (_generatedAudioClip != null)
        {
            _audioTrack.Clips.Remove(_generatedAudioClip);
        }
    }
}
