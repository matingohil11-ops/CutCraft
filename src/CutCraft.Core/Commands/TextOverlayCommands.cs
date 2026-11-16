using CutCraft.Core.Models;

namespace CutCraft.Core.Commands;

public class AddTextOverlayCommand : ICommand
{
    private readonly Project _project;
    private readonly TextOverlay _overlay;

    public string Description => $"Add text overlay '{_overlay.Text}' ({_overlay.Style})";
    public string AccessibilityAnnouncement => $"Added {_overlay.Style} title card: '{_overlay.Text}' at {_overlay.StartTime:mm\\:ss}.";
    public bool CanUndo => true;

    public AddTextOverlayCommand(Project project, TextOverlay overlay)
    {
        _project = project;
        _overlay = overlay;
    }

    public void Execute()
    {
        if (!_project.Overlays.Contains(_overlay))
        {
            _project.Overlays.Add(_overlay);
        }
    }

    public void Unexecute()
    {
        _project.Overlays.Remove(_overlay);
    }
}

public class RemoveTextOverlayCommand : ICommand
{
    private readonly Project _project;
    private readonly TextOverlay _overlay;
    private readonly int _previousIndex;

    public string Description => $"Remove text overlay '{_overlay.Text}'";
    public string AccessibilityAnnouncement => $"Removed title card: '{_overlay.Text}'.";
    public bool CanUndo => true;

    public RemoveTextOverlayCommand(Project project, TextOverlay overlay)
    {
        _project = project;
        _overlay = overlay;
        _previousIndex = project.Overlays.IndexOf(overlay);
    }

    public void Execute()
    {
        _project.Overlays.Remove(_overlay);
    }

    public void Unexecute()
    {
        if (_previousIndex >= 0 && _previousIndex <= _project.Overlays.Count)
        {
            _project.Overlays.Insert(_previousIndex, _overlay);
        }
        else
        {
            _project.Overlays.Add(_overlay);
        }
    }
}

public class UpdateTextOverlayCommand : ICommand
{
    private readonly TextOverlay _target;
    private readonly TextOverlay _oldSnapshot;
    private readonly TextOverlay _newSnapshot;

    public string Description => $"Update title card '{_newSnapshot.Text}'";
    public string AccessibilityAnnouncement => $"Updated title card properties for '{_newSnapshot.Text}'.";
    public bool CanUndo => true;

    public UpdateTextOverlayCommand(TextOverlay target, TextOverlay newValues)
    {
        _target = target;
        _oldSnapshot = target.Clone();
        _newSnapshot = newValues.Clone();
    }

    public void Execute()
    {
        ApplySnapshot(_target, _newSnapshot);
    }

    public void Unexecute()
    {
        ApplySnapshot(_target, _oldSnapshot);
    }

    private static void ApplySnapshot(TextOverlay target, TextOverlay source)
    {
        target.Text = source.Text;
        target.SubtitleText = source.SubtitleText;
        target.FontName = source.FontName;
        target.FontSize = source.FontSize;
        target.ColorHex = source.ColorHex;
        target.BackgroundColorHex = source.BackgroundColorHex;
        target.PositionX = source.PositionX;
        target.PositionY = source.PositionY;
        target.StartTime = source.StartTime;
        target.Duration = source.Duration;
        target.Style = source.Style;
        target.Animation = source.Animation;
        target.AnimationDuration = source.AnimationDuration;
        target.Alignment = source.Alignment;
        target.IsBold = source.IsBold;
        target.IsItalic = source.IsItalic;
        target.BorderColorHex = source.BorderColorHex;
        target.BorderWidth = source.BorderWidth;
        target.ShadowColorHex = source.ShadowColorHex;
        target.ShadowDepth = source.ShadowDepth;
        target.BoxPadding = source.BoxPadding;
    }
}

public class ApplyTitleCardPresetCommand : ICommand
{
    private readonly TextOverlay _target;
    private readonly TextOverlay _oldSnapshot;
    private readonly TextOverlay _newPreset;

    public string Description => $"Apply {_newPreset.Style} preset to '{_target.Text}'";
    public string AccessibilityAnnouncement => $"Applied {_newPreset.Style} styling preset to title card '{_target.Text}'.";
    public bool CanUndo => true;

    public ApplyTitleCardPresetCommand(TextOverlay target, TextOverlay newPresetTemplate)
    {
        _target = target;
        _oldSnapshot = target.Clone();

        _newPreset = newPresetTemplate.Clone();
        // Preserve current timing and custom text if available
        _newPreset.StartTime = target.StartTime;
        _newPreset.Duration = target.Duration;
        if (!string.IsNullOrWhiteSpace(target.Text))
        {
            _newPreset.Text = target.Text;
        }
        if (!string.IsNullOrWhiteSpace(target.SubtitleText))
        {
            _newPreset.SubtitleText = target.SubtitleText;
        }
    }

    public void Execute()
    {
        CopyStyling(_newPreset, _target);
    }

    public void Unexecute()
    {
        CopyStyling(_oldSnapshot, _target);
    }

    private static void CopyStyling(TextOverlay source, TextOverlay destination)
    {
        destination.Text = source.Text;
        destination.SubtitleText = source.SubtitleText;
        destination.FontName = source.FontName;
        destination.FontSize = source.FontSize;
        destination.ColorHex = source.ColorHex;
        destination.BackgroundColorHex = source.BackgroundColorHex;
        destination.PositionX = source.PositionX;
        destination.PositionY = source.PositionY;
        destination.Style = source.Style;
        destination.Animation = source.Animation;
        destination.AnimationDuration = source.AnimationDuration;
        destination.Alignment = source.Alignment;
        destination.IsBold = source.IsBold;
        destination.IsItalic = source.IsItalic;
        destination.BorderColorHex = source.BorderColorHex;
        destination.BorderWidth = source.BorderWidth;
        destination.ShadowColorHex = source.ShadowColorHex;
        destination.ShadowDepth = source.ShadowDepth;
        destination.BoxPadding = source.BoxPadding;
    }
}
