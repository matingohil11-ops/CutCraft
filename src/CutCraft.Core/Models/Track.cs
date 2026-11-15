using System.Collections.ObjectModel;
using System.Text.Json.Serialization;

namespace CutCraft.Core.Models;

public enum TrackType
{
    Video,
    Overlay,
    Audio,
    Caption
}

public class Track
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "Track";
    public TrackType Type { get; set; } = TrackType.Video;
    public int Order { get; set; } = 0;
    public bool IsMuted { get; set; } = false;
    public bool IsLocked { get; set; } = false;
    public double Volume { get; set; } = 1.0;

    public ObservableCollection<Clip> Clips { get; set; } = new();

    [JsonIgnore]
    public TimeSpan Duration
    {
        get
        {
            if (Clips.Count == 0) return TimeSpan.Zero;
            return Clips.Max(c => c.TimelineEnd);
        }
    }
}
