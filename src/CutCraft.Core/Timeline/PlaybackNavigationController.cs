using CutCraft.Core.Models;

namespace CutCraft.Core.Timeline;

public enum NavigationPlaybackDirection
{
    Reverse,
    Stopped,
    Forward
}

public class PlaybackNavigationController
{
    private readonly TimelineEngine _engine;
    private double _currentRate = 0.0;
    private NavigationPlaybackDirection _direction = NavigationPlaybackDirection.Stopped;

    public TimeSpan CurrentPosition { get; private set; } = TimeSpan.Zero;
    public double PlaybackRate => _currentRate;
    public NavigationPlaybackDirection Direction => _direction;

    public event EventHandler<TimeSpan>? PositionChanged;
    public event EventHandler<double>? RateChanged;
    public event EventHandler<string>? NavigationAnnouncement;

    public PlaybackNavigationController(TimelineEngine? engine = null)
    {
        _engine = engine ?? new TimelineEngine();
    }

    public void SetPosition(TimeSpan position, Project? project = null)
    {
        var clamped = position < TimeSpan.Zero ? TimeSpan.Zero : position;
        if (project != null && clamped > project.Duration && project.Duration > TimeSpan.Zero)
        {
            clamped = project.Duration;
        }

        CurrentPosition = clamped;
        PositionChanged?.Invoke(this, CurrentPosition);
    }

    /// <summary>
    /// Handles 'L' key press: Starts forward playback or accelerates (1x -> 2x -> 4x -> 8x).
    /// </summary>
    public void PressL()
    {
        if (_direction != NavigationPlaybackDirection.Forward)
        {
            _direction = NavigationPlaybackDirection.Forward;
            _currentRate = 1.0;
        }
        else
        {
            _currentRate = _currentRate >= 8.0 ? 8.0 : _currentRate * 2.0;
        }

        RateChanged?.Invoke(this, _currentRate);
        NavigationAnnouncement?.Invoke(this, $"Playing forward at {_currentRate}x");
    }

    /// <summary>
    /// Handles 'K' key press: Pauses / stops playback.
    /// </summary>
    public void PressK()
    {
        _direction = NavigationPlaybackDirection.Stopped;
        _currentRate = 0.0;
        RateChanged?.Invoke(this, _currentRate);
        NavigationAnnouncement?.Invoke(this, "Playback paused");
    }

    /// <summary>
    /// Handles 'J' key press: Starts reverse playback or accelerates (-1x -> -2x -> -4x -> -8x).
    /// </summary>
    public void PressJ()
    {
        if (_direction != NavigationPlaybackDirection.Reverse)
        {
            _direction = NavigationPlaybackDirection.Reverse;
            _currentRate = -1.0;
        }
        else
        {
            _currentRate = _currentRate <= -8.0 ? -8.0 : _currentRate * 2.0;
        }

        RateChanged?.Invoke(this, _currentRate);
        NavigationAnnouncement?.Invoke(this, $"Playing in reverse at {Math.Abs(_currentRate)}x");
    }

    public void JumpToNextEditPoint(Project project)
    {
        var target = _engine.GetNextEditPoint(project, CurrentPosition);
        SetPosition(target, project);
        NavigationAnnouncement?.Invoke(this, $"Jumped to next cut at {target:mm\\:ss\\.fff}");
    }

    public void JumpToPreviousEditPoint(Project project)
    {
        var target = _engine.GetPreviousEditPoint(project, CurrentPosition);
        SetPosition(target, project);
        NavigationAnnouncement?.Invoke(this, $"Jumped to previous cut at {target:mm\\:ss\\.fff}");
    }

    public void JumpToStart(Project project)
    {
        SetPosition(TimeSpan.Zero, project);
        NavigationAnnouncement?.Invoke(this, "Jumped to start of project");
    }

    public void JumpToEnd(Project project)
    {
        SetPosition(project.Duration, project);
        NavigationAnnouncement?.Invoke(this, $"Jumped to end of project at {project.Duration:mm\\:ss\\.fff}");
    }

    public void StepFrames(int frames, double fps, Project project)
    {
        var delta = TimeSpan.FromSeconds(frames / Math.Max(1.0, fps));
        SetPosition(CurrentPosition + delta, project);
    }
}
