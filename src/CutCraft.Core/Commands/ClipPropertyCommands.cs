using CutCraft.Core.Models;

namespace CutCraft.Core.Commands;

public class ChangeClipVolumeCommand : ICommand
{
    private readonly Clip _clip;
    private readonly double _newVolume;
    private readonly double _previousVolume;

    public string Description => $"Change volume of '{_clip.Name}' to {(_newVolume * 100):0}%";
    public string AccessibilityAnnouncement => $"Volume of '{_clip.Name}' set to {(_newVolume * 100):0} percent";
    public bool CanUndo => true;

    public ChangeClipVolumeCommand(Clip clip, double newVolume)
    {
        _clip = clip;
        _newVolume = Math.Clamp(newVolume, 0.0, 2.0);
        _previousVolume = clip.Volume;
    }

    public void Execute()
    {
        _clip.Volume = _newVolume;
    }

    public void Unexecute()
    {
        _clip.Volume = _previousVolume;
    }
}

public class AddEffectCommand : ICommand
{
    private readonly Clip _clip;
    private readonly ClipEffect _effect;

    public string Description => $"Add {_effect.Type} effect to '{_clip.Name}'";
    public string AccessibilityAnnouncement => $"Applied {_effect.Type} effect to clip '{_clip.Name}'";
    public bool CanUndo => true;

    public AddEffectCommand(Clip clip, ClipEffect effect)
    {
        _clip = clip;
        _effect = effect;
    }

    public void Execute()
    {
        _clip.Effects.Add(_effect);
    }

    public void Unexecute()
    {
        _clip.Effects.Remove(_effect);
    }
}
