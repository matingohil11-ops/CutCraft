using CutCraft.Core.Models;

namespace CutCraft.Core.Timeline;

public record TimelineGap(TimeSpan Start, TimeSpan End)
{
    public TimeSpan Duration => End > Start ? End - Start : TimeSpan.Zero;
}

public class TimelineEngine
{
    public double SnapThresholdSeconds { get; set; } = 0.20;

    public TimeSpan SnapTime(Project project, TimeSpan targetTime, TimeSpan? excludeThreshold = null)
    {
        var threshold = excludeThreshold ?? TimeSpan.FromSeconds(SnapThresholdSeconds);
        var bestSnap = targetTime;
        var minDiff = threshold;

        // Snap to zero if within threshold
        if (targetTime < threshold)
        {
            minDiff = targetTime;
            bestSnap = TimeSpan.Zero;
        }

        // Check all clips in all tracks for start or end points
        foreach (var track in project.Tracks)
        {
            foreach (var clip in track.Clips)
            {
                var diffStart = (clip.TimelineStart - targetTime).Duration();
                if (diffStart < minDiff)
                {
                    minDiff = diffStart;
                    bestSnap = clip.TimelineStart;
                }

                var diffEnd = (clip.TimelineEnd - targetTime).Duration();
                if (diffEnd < minDiff)
                {
                    minDiff = diffEnd;
                    bestSnap = clip.TimelineEnd;
                }
            }
        }

        // Check markers for snapping
        foreach (var marker in project.Markers)
        {
            var diffMarker = (marker.Time - targetTime).Duration();
            if (diffMarker < minDiff)
            {
                minDiff = diffMarker;
                bestSnap = marker.Time;
            }
        }

        return bestSnap;
    }

    public List<Clip> GetClipsAtTime(Project project, TimeSpan time)
    {
        var result = new List<Clip>();
        foreach (var track in project.Tracks)
        {
            if (track.IsMuted) continue;
            foreach (var clip in track.Clips)
            {
                if (time >= clip.TimelineStart && time < clip.TimelineEnd)
                {
                    result.Add(clip);
                }
            }
        }
        return result;
    }

    public Clip? GetClipAtTimeOnTrack(Track track, TimeSpan time)
    {
        return track.Clips.FirstOrDefault(c => time >= c.TimelineStart && time < c.TimelineEnd);
    }

    public TimeSpan GetNextEditPoint(Project project, TimeSpan currentTime)
    {
        var editPoints = new SortedSet<TimeSpan>();
        foreach (var track in project.Tracks)
        {
            foreach (var clip in track.Clips)
            {
                if (clip.TimelineStart > currentTime) editPoints.Add(clip.TimelineStart);
                if (clip.TimelineEnd > currentTime) editPoints.Add(clip.TimelineEnd);
            }
        }

        if (project.Duration > currentTime)
        {
            editPoints.Add(project.Duration);
        }

        return editPoints.Count > 0 ? editPoints.Min : currentTime;
    }

    public TimeSpan GetPreviousEditPoint(Project project, TimeSpan currentTime)
    {
        var editPoints = new SortedSet<TimeSpan>();
        editPoints.Add(TimeSpan.Zero);

        foreach (var track in project.Tracks)
        {
            foreach (var clip in track.Clips)
            {
                if (clip.TimelineStart < currentTime) editPoints.Add(clip.TimelineStart);
                if (clip.TimelineEnd < currentTime) editPoints.Add(clip.TimelineEnd);
            }
        }

        var smallerPoints = editPoints.Where(t => t < currentTime).ToList();
        return smallerPoints.Count > 0 ? smallerPoints.Max() : TimeSpan.Zero;
    }

    public void RippleShiftAfter(Track track, TimeSpan afterTime, TimeSpan delta)
    {
        foreach (var clip in track.Clips)
        {
            if (clip.TimelineStart >= afterTime)
            {
                clip.TimelineStart += delta;
                if (clip.TimelineStart < TimeSpan.Zero)
                {
                    clip.TimelineStart = TimeSpan.Zero;
                }
            }
        }
    }

    public List<TimelineGap> FindGaps(Track track)
    {
        var gaps = new List<TimelineGap>();
        var ordered = track.Clips.OrderBy(c => c.TimelineStart).ToList();
        if (ordered.Count == 0) return gaps;

        // Check if there is a gap before the first clip
        if (ordered[0].TimelineStart > TimeSpan.FromMilliseconds(10))
        {
            gaps.Add(new TimelineGap(TimeSpan.Zero, ordered[0].TimelineStart));
        }

        // Check gaps between adjacent clips
        for (int i = 0; i < ordered.Count - 1; i++)
        {
            var currentEnd = ordered[i].TimelineEnd;
            var nextStart = ordered[i + 1].TimelineStart;

            if (nextStart - currentEnd > TimeSpan.FromMilliseconds(10))
            {
                gaps.Add(new TimelineGap(currentEnd, nextStart));
            }
        }

        return gaps;
    }

    public TimelineGap? FindGapAt(Track track, TimeSpan time)
    {
        var gaps = FindGaps(track);
        return gaps.FirstOrDefault(g => time >= g.Start && time < g.End);
    }

    public void CloseGap(Track track, TimelineGap gap)
    {
        var delta = -gap.Duration;
        foreach (var clip in track.Clips)
        {
            if (clip.TimelineStart >= gap.End - TimeSpan.FromMilliseconds(5))
            {
                clip.TimelineStart += delta;
                if (clip.TimelineStart < TimeSpan.Zero) clip.TimelineStart = TimeSpan.Zero;
            }
        }
    }

    public void ArrangeSequentially(Track track, TimeSpan gap = default)
    {
        var current = TimeSpan.Zero;
        foreach (var clip in track.Clips.OrderBy(c => c.TimelineStart))
        {
            clip.TimelineStart = current;
            current += clip.TimelineDuration + gap;
        }
    }

    public static string FormatTimecode(TimeSpan time, double fps = 30.0)
    {
        if (time < TimeSpan.Zero) time = TimeSpan.Zero;
        var validFps = Math.Max(1.0, fps);
        var hours = (int)time.TotalHours;
        var minutes = time.Minutes;
        var seconds = time.Seconds;
        var frames = (int)Math.Clamp((time.TotalSeconds - Math.Truncate(time.TotalSeconds)) * validFps, 0, Math.Max(0, (int)validFps - 1));

        return $"{hours:D2}:{minutes:D2}:{seconds:D2};{frames:D2}";
    }

    public static long TimeToFrames(TimeSpan time, double fps = 30.0)
    {
        if (time < TimeSpan.Zero) return 0;
        return (long)Math.Round(time.TotalSeconds * Math.Max(1.0, fps));
    }

    public static TimeSpan FramesToTime(long frames, double fps = 30.0)
    {
        if (frames < 0) return TimeSpan.Zero;
        return TimeSpan.FromSeconds(frames / Math.Max(1.0, fps));
    }
}
