namespace CutCraft.Core.Commands;

public class CommandEventArgs : EventArgs
{
    public ICommand Command { get; }
    public string Message { get; }

    public CommandEventArgs(ICommand command, string message)
    {
        Command = command;
        Message = message;
    }
}

public class CommandManager
{
    private readonly Stack<ICommand> _undoStack = new();
    private readonly Stack<ICommand> _redoStack = new();
    private readonly int _maxHistory;

    public event EventHandler<CommandEventArgs>? CommandExecuted;
    public event EventHandler<CommandEventArgs>? UndoPerformed;
    public event EventHandler<CommandEventArgs>? RedoPerformed;
    public event EventHandler<string>? AccessibilityNotification;

    public bool CanUndo => _undoStack.Count > 0;
    public bool CanRedo => _redoStack.Count > 0;
    public int UndoCount => _undoStack.Count;
    public int RedoCount => _redoStack.Count;

    public CommandManager(int maxHistory = 100)
    {
        _maxHistory = maxHistory;
    }

    public void Execute(ICommand command)
    {
        command.Execute();

        if (command.CanUndo)
        {
            _undoStack.Push(command);
            if (_undoStack.Count > _maxHistory)
            {
                var list = _undoStack.Reverse().Skip(1).Reverse().ToList();
                _undoStack.Clear();
                foreach (var cmd in list) _undoStack.Push(cmd);
            }
            _redoStack.Clear();
        }

        CommandExecuted?.Invoke(this, new CommandEventArgs(command, command.Description));
        AccessibilityNotification?.Invoke(this, command.AccessibilityAnnouncement);
    }

    public bool Undo()
    {
        if (!CanUndo) return false;

        var command = _undoStack.Pop();
        command.Unexecute();
        _redoStack.Push(command);

        var announcement = $"Undid: {command.Description}";
        UndoPerformed?.Invoke(this, new CommandEventArgs(command, announcement));
        AccessibilityNotification?.Invoke(this, announcement);
        return true;
    }

    public bool Redo()
    {
        if (!CanRedo) return false;

        var command = _redoStack.Pop();
        command.Execute();
        _undoStack.Push(command);

        var announcement = $"Redid: {command.Description}";
        RedoPerformed?.Invoke(this, new CommandEventArgs(command, announcement));
        AccessibilityNotification?.Invoke(this, announcement);
        return true;
    }

    public void Clear()
    {
        _undoStack.Clear();
        _redoStack.Clear();
    }
}
