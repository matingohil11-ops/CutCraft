namespace CutCraft.Core.Timeline;

/// <summary>
/// Manages full-screen display state, keyboard shortcuts (F11/Esc), and accessibility announcements.
/// </summary>
public class FullScreenStateController
{
    private bool _isFullScreen;

    public bool IsFullScreen => _isFullScreen;
    public string ButtonText => _isFullScreen ? "🗗 Exit Full Screen" : "⛶ Full Screen";
    public string ButtonTooltip => _isFullScreen ? "Exit Full Screen (F11 or Esc)" : "Enter Full Screen (F11)";
    public string StatusAnnouncement => _isFullScreen 
        ? "Full screen mode enabled. Press F11 or Esc to exit full screen." 
        : "Full screen mode disabled.";

    public event EventHandler<bool>? FullScreenChanged;

    public bool Toggle()
    {
        _isFullScreen = !_isFullScreen;
        FullScreenChanged?.Invoke(this, _isFullScreen);
        return _isFullScreen;
    }

    public bool Enter()
    {
        if (_isFullScreen) return false;
        _isFullScreen = true;
        FullScreenChanged?.Invoke(this, true);
        return true;
    }

    public bool Exit()
    {
        if (!_isFullScreen) return false;
        _isFullScreen = false;
        FullScreenChanged?.Invoke(this, false);
        return true;
    }
}
