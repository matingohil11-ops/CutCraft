using CutCraft.Core.Models;

namespace CutCraft.Core.Accessibility;

public interface IAccessibilityAnnouncer
{
    void Announce(string message);
}

public class TimelineAccessibilityNotifier
{
    private readonly IAccessibilityAnnouncer? _announcer;

    public event EventHandler<string>? SpokenMessageGenerated;

    public TimelineAccessibilityNotifier(IAccessibilityAnnouncer? announcer = null)
    {
        _announcer = announcer;
    }

    public void Announce(string message)
    {
        SpokenMessageGenerated?.Invoke(this, message);
        _announcer?.Announce(message);
    }

    public void AnnouncePlayheadMoved(TimeSpan position, Clip? activeClip, double fps = 30.0)
    {
        var timeStr = $"{position.Minutes} minutes {position.Seconds} seconds {position.Milliseconds} milliseconds";
        if (activeClip != null)
        {
            var relativeIn = position - activeClip.TimelineStart;
            Announce($"Position {timeStr}, inside clip '{activeClip.Name}', {relativeIn.TotalSeconds:F1} seconds in");
        }
        else
        {
            Announce($"Position {timeStr}, empty space");
        }
    }

    public void AnnounceClipSelected(Clip clip, int clipIndex, int totalClips)
    {
        Announce($"Selected clip {clipIndex + 1} of {totalClips}: '{clip.Name}', duration {clip.TimelineDuration.TotalSeconds:F1} seconds, starts at {clip.TimelineStart:mm\\:ss}");
    }

    public void AnnounceSplit(Clip original, Clip part1, Clip part2, TimeSpan atTime)
    {
        Announce($"Split '{original.Name}' at {atTime:mm\\:ss\\.fff}. Created '{part1.Name}' and '{part2.Name}'.");
    }

    public void AnnounceTrim(Clip clip, string edge, TimeSpan newPoint, TimeSpan newDuration)
    {
        Announce($"Trimmed {edge} of '{clip.Name}'. New duration: {newDuration.TotalSeconds:F1} seconds.");
    }
}
