using CutCraft.Core.Models;
using CutCraft.Core.Subtitles;

namespace CutCraft.Core.Commands;

public class AddCaptionCommand : ICommand
{
    private readonly Project _project;
    private readonly CaptionItem _caption;

    public string Description => $"Add caption '{_caption.Text}' at {_caption.StartTime:mm\\:ss\\.fff}";
    public string AccessibilityAnnouncement => $"Added caption #{_caption.Index}: '{_caption.Text}' starting at {_caption.StartTime:mm\\:ss}.";
    public bool CanUndo => true;

    public AddCaptionCommand(Project project, CaptionItem caption)
    {
        _project = project;
        _caption = caption;
    }

    public void Execute()
    {
        if (!_project.Captions.Contains(_caption))
        {
            _project.Captions.Add(_caption);
            SrtSubtitleService.RenumberCaptions(_project.Captions);
        }
    }

    public void Unexecute()
    {
        if (_project.Captions.Contains(_caption))
        {
            _project.Captions.Remove(_caption);
            SrtSubtitleService.RenumberCaptions(_project.Captions);
        }
    }
}

public class RemoveCaptionCommand : ICommand
{
    private readonly Project _project;
    private readonly CaptionItem _caption;
    private readonly int _previousIndex;

    public string Description => $"Remove caption #{_caption.Index} '{_caption.Text}'";
    public string AccessibilityAnnouncement => $"Removed caption #{_caption.Index}: '{_caption.Text}'.";
    public bool CanUndo => true;

    public RemoveCaptionCommand(Project project, CaptionItem caption)
    {
        _project = project;
        _caption = caption;
        _previousIndex = project.Captions.IndexOf(caption);
    }

    public void Execute()
    {
        _project.Captions.Remove(_caption);
        SrtSubtitleService.RenumberCaptions(_project.Captions);
    }

    public void Unexecute()
    {
        if (_previousIndex >= 0 && _previousIndex <= _project.Captions.Count)
        {
            _project.Captions.Insert(_previousIndex, _caption);
        }
        else
        {
            _project.Captions.Add(_caption);
        }
        SrtSubtitleService.RenumberCaptions(_project.Captions);
    }
}

public class UpdateCaptionCommand : ICommand
{
    private readonly CaptionItem _caption;
    private readonly CaptionItem _oldSnapshot;
    private readonly CaptionItem _newSnapshot;

    public string Description => $"Edit caption #{_caption.Index}";
    public string AccessibilityAnnouncement => $"Updated caption #{_caption.Index} text to '{_newSnapshot.Text}' from {_newSnapshot.StartTime:mm\\:ss} to {_newSnapshot.EndTime:mm\\:ss}.";
    public bool CanUndo => true;

    public UpdateCaptionCommand(CaptionItem caption, CaptionItem newValues)
    {
        _caption = caption;
        _oldSnapshot = caption.Clone();
        _newSnapshot = newValues.Clone();
    }

    public void Execute()
    {
        ApplySnapshot(_caption, _newSnapshot);
    }

    public void Unexecute()
    {
        ApplySnapshot(_caption, _oldSnapshot);
    }

    private static void ApplySnapshot(CaptionItem target, CaptionItem source)
    {
        target.Text = source.Text;
        target.StartTime = source.StartTime;
        target.EndTime = source.EndTime;
        target.Speaker = source.Speaker;
        target.StylePreset = source.StylePreset;
    }
}

public class ShiftCaptionsCommand : ICommand
{
    private readonly Project _project;
    private readonly TimeSpan _offset;
    private readonly List<(CaptionItem Item, TimeSpan Start, TimeSpan End)> _snapshots = new();

    public string Description => $"Shift captions by {_offset.TotalSeconds:+#0.00;-#0.00}s";
    public string AccessibilityAnnouncement => $"Shifted {_snapshots.Count} captions by {_offset.TotalSeconds:F2} seconds.";
    public bool CanUndo => true;

    public ShiftCaptionsCommand(Project project, TimeSpan offset)
    {
        _project = project;
        _offset = offset;
        foreach (var cap in project.Captions)
        {
            _snapshots.Add((cap, cap.StartTime, cap.EndTime));
        }
    }

    public void Execute()
    {
        SrtSubtitleService.ShiftCaptions(_project.Captions, _offset);
    }

    public void Unexecute()
    {
        foreach (var snap in _snapshots)
        {
            snap.Item.StartTime = snap.Start;
            snap.Item.EndTime = snap.End;
        }
    }
}

public class SplitCaptionCommand : ICommand
{
    private readonly Project _project;
    private readonly CaptionItem _originalCaption;
    private readonly CaptionItem _leftCaption;
    private readonly CaptionItem _rightCaption;
    private readonly int _originalIndex;

    public string Description => $"Split caption #{_originalCaption.Index} at playhead";
    public string AccessibilityAnnouncement => $"Split caption #{_originalCaption.Index} into two captions.";
    public bool CanUndo => true;

    public SplitCaptionCommand(Project project, CaptionItem caption, TimeSpan splitPoint)
    {
        _project = project;
        _originalCaption = caption;
        _originalIndex = project.Captions.IndexOf(caption);

        var (left, right) = SrtSubtitleService.SplitCaption(caption, splitPoint);
        _leftCaption = left;
        _rightCaption = right;
    }

    public void Execute()
    {
        int idx = _project.Captions.IndexOf(_originalCaption);
        if (idx >= 0)
        {
            _project.Captions.RemoveAt(idx);
            _project.Captions.Insert(idx, _rightCaption);
            _project.Captions.Insert(idx, _leftCaption);
            SrtSubtitleService.RenumberCaptions(_project.Captions);
        }
    }

    public void Unexecute()
    {
        _project.Captions.Remove(_leftCaption);
        _project.Captions.Remove(_rightCaption);
        if (_originalIndex >= 0 && _originalIndex <= _project.Captions.Count)
        {
            _project.Captions.Insert(_originalIndex, _originalCaption);
        }
        else
        {
            _project.Captions.Add(_originalCaption);
        }
        SrtSubtitleService.RenumberCaptions(_project.Captions);
    }
}

public class MergeCaptionsCommand : ICommand
{
    private readonly Project _project;
    private readonly CaptionItem _first;
    private readonly CaptionItem _second;
    private readonly CaptionItem _merged;
    private readonly int _firstIndex;

    public string Description => $"Merge captions #{_first.Index} and #{_second.Index}";
    public string AccessibilityAnnouncement => $"Merged captions #{_first.Index} and #{_second.Index} into single subtitle.";
    public bool CanUndo => true;

    public MergeCaptionsCommand(Project project, CaptionItem first, CaptionItem second)
    {
        _project = project;
        _first = first;
        _second = second;
        _firstIndex = project.Captions.IndexOf(first);
        _merged = SrtSubtitleService.MergeCaptions(first, second);
    }

    public void Execute()
    {
        _project.Captions.Remove(_first);
        _project.Captions.Remove(_second);

        int insertIndex = Math.Clamp(_firstIndex, 0, _project.Captions.Count);
        _project.Captions.Insert(insertIndex, _merged);
        SrtSubtitleService.RenumberCaptions(_project.Captions);
    }

    public void Unexecute()
    {
        _project.Captions.Remove(_merged);
        int insertIndex = Math.Clamp(_firstIndex, 0, _project.Captions.Count);
        _project.Captions.Insert(insertIndex, _first);
        _project.Captions.Insert(insertIndex + 1, _second);
        SrtSubtitleService.RenumberCaptions(_project.Captions);
    }
}
