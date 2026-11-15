using CutCraft.Core.Models;

namespace CutCraft.Core.Commands;

public class AddMarkerCommand : ICommand
{
    private readonly Project _project;
    private readonly TimelineMarker _marker;

    public string Description => $"Add marker '{_marker.Label}' at {_marker.Time:mm\\:ss\\.fff}";
    public string AccessibilityAnnouncement => $"Added marker '{_marker.Label}' at {_marker.Time:mm\\:ss\\.fff}";
    public bool CanUndo => true;

    public AddMarkerCommand(Project project, TimelineMarker marker)
    {
        _project = project;
        _marker = marker;
    }

    public void Execute()
    {
        _project.Markers.Add(_marker);
    }

    public void Unexecute()
    {
        _project.Markers.Remove(_marker);
    }
}

public class RemoveMarkerCommand : ICommand
{
    private readonly Project _project;
    private readonly TimelineMarker _marker;
    private readonly int _previousIndex;

    public string Description => $"Remove marker '{_marker.Label}'";
    public string AccessibilityAnnouncement => $"Removed marker '{_marker.Label}' from {_marker.Time:mm\\:ss\\.fff}";
    public bool CanUndo => true;

    public RemoveMarkerCommand(Project project, TimelineMarker marker)
    {
        _project = project;
        _marker = marker;
        _previousIndex = project.Markers.IndexOf(marker);
    }

    public void Execute()
    {
        _project.Markers.Remove(_marker);
    }

    public void Unexecute()
    {
        if (_previousIndex >= 0 && _previousIndex <= _project.Markers.Count)
            _project.Markers.Insert(_previousIndex, _marker);
        else
            _project.Markers.Add(_marker);
    }
}
