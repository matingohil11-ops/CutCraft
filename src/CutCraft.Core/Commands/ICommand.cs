namespace CutCraft.Core.Commands;

public interface ICommand
{
    string Description { get; }
    string AccessibilityAnnouncement { get; }
    bool CanUndo { get; }
    void Execute();
    void Unexecute();
}
