using CutCraft.Core.Models;

namespace CutCraft.Core.Commands;

public class SetAudioFadeCommand : ICommand
{
    private readonly Clip _clip;
    private readonly double _newFadeIn;
    private readonly double _newFadeOut;
    private readonly double _prevFadeIn;
    private readonly double _prevFadeOut;

    public string Description => $"Set audio fade on '{_clip.Name}' (In: {_newFadeIn:F1}s, Out: {_newFadeOut:F1}s)";
    public string AccessibilityAnnouncement => $"Audio fade on '{_clip.Name}' set to {_newFadeIn:F1} seconds fade in, {_newFadeOut:F1} seconds fade out";
    public bool CanUndo => true;

    public SetAudioFadeCommand(Clip clip, double fadeInSeconds, double fadeOutSeconds)
    {
        _clip = clip;
        _newFadeIn = Math.Max(0, fadeInSeconds);
        _newFadeOut = Math.Max(0, fadeOutSeconds);
        _prevFadeIn = clip.AudioFadeIn;
        _prevFadeOut = clip.AudioFadeOut;
    }

    public void Execute()
    {
        _clip.AudioFadeIn = _newFadeIn;
        _clip.AudioFadeOut = _newFadeOut;
    }

    public void Unexecute()
    {
        _clip.AudioFadeIn = _prevFadeIn;
        _clip.AudioFadeOut = _prevFadeOut;
    }
}

public class RotateClipCommand : ICommand
{
    private readonly Clip _clip;
    private readonly int _newRotation;
    private readonly int _prevRotation;

    public string Description => $"Rotate '{_clip.Name}' to {_newRotation}°";
    public string AccessibilityAnnouncement => $"Rotated '{_clip.Name}' to {_newRotation} degrees";
    public bool CanUndo => true;

    public RotateClipCommand(Clip clip, int rotationAngle = -1)
    {
        _clip = clip;
        _prevRotation = clip.Rotation;
        _newRotation = rotationAngle >= 0 ? (rotationAngle % 360) : ((clip.Rotation + 90) % 360);
    }

    public void Execute()
    {
        _clip.Rotation = _newRotation;
    }

    public void Unexecute()
    {
        _clip.Rotation = _prevRotation;
    }
}

public class ToggleClipFlipCommand : ICommand
{
    private readonly Clip _clip;
    private readonly bool _horizontal;
    private readonly bool _prevValue;

    public string Description => $"Toggle {(_horizontal ? "Horizontal" : "Vertical")} Flip on '{_clip.Name}'";
    public string AccessibilityAnnouncement => $"{(_horizontal ? "Horizontal" : "Vertical")} flip toggled on '{_clip.Name}'";
    public bool CanUndo => true;

    public ToggleClipFlipCommand(Clip clip, bool horizontal)
    {
        _clip = clip;
        _horizontal = horizontal;
        _prevValue = horizontal ? clip.FlipHorizontal : clip.FlipVertical;
    }

    public void Execute()
    {
        if (_horizontal)
            _clip.FlipHorizontal = !_prevValue;
        else
            _clip.FlipVertical = !_prevValue;
    }

    public void Unexecute()
    {
        if (_horizontal)
            _clip.FlipHorizontal = _prevValue;
        else
            _clip.FlipVertical = _prevValue;
    }
}

public class SetPanZoomCommand : ICommand
{
    private readonly Clip _clip;
    private readonly PanZoomType _newPanZoom;
    private readonly PanZoomType _prevPanZoom;

    public string Description => $"Set Pan & Zoom to {_newPanZoom} on '{_clip.Name}'";
    public string AccessibilityAnnouncement => $"Pan and Zoom animation set to {_newPanZoom} on '{_clip.Name}'";
    public bool CanUndo => true;

    public SetPanZoomCommand(Clip clip, PanZoomType panZoom)
    {
        _clip = clip;
        _newPanZoom = panZoom;
        _prevPanZoom = clip.PanZoom;
    }

    public void Execute()
    {
        _clip.PanZoom = _newPanZoom;
    }

    public void Unexecute()
    {
        _clip.PanZoom = _prevPanZoom;
    }
}
