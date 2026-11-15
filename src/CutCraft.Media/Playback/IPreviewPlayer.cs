namespace CutCraft.Media.Playback;

public enum PlaybackState
{
    Stopped,
    Playing,
    Paused
}

public interface IPreviewPlayer : IDisposable
{
    PlaybackState State { get; }
    TimeSpan CurrentTime { get; }
    TimeSpan TotalTime { get; }
    double Volume { get; set; }
    double PlaybackRate { get; set; }

    event EventHandler<TimeSpan>? TimeChanged;
    event EventHandler<PlaybackState>? StateChanged;

    Task OpenAsync(string filePath);
    void Play();
    void Pause();
    void Stop();
    void Seek(TimeSpan position);
}
