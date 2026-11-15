using System.Collections.ObjectModel;
using System.Text.Json.Serialization;

namespace CutCraft.Core.Models;

public class Project
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "Untitled Project";
    public int SchemaVersion { get; set; } = 1;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime ModifiedAt { get; set; } = DateTime.UtcNow;

    // Video canvas properties
    public int CanvasWidth { get; set; } = 1920;
    public int CanvasHeight { get; set; } = 1080;
    public double FrameRate { get; set; } = 30.0;

    // Audio master properties
    public int AudioSampleRate { get; set; } = 48000;
    public int AudioChannels { get; set; } = 2;
    public double MasterVolume { get; set; } = 1.0;

    public ObservableCollection<MediaAsset> MediaLibrary { get; set; } = new();
    public ObservableCollection<Track> Tracks { get; set; } = new();
    public ObservableCollection<TextOverlay> Overlays { get; set; } = new();
    public ObservableCollection<CaptionItem> Captions { get; set; } = new();
    public ObservableCollection<TimelineMarker> Markers { get; set; } = new();

    [JsonIgnore]
    public TimeSpan Duration
    {
        get
        {
            var maxTrack = Tracks.Count > 0 ? Tracks.Max(t => t.Duration) : TimeSpan.Zero;
            var maxOverlay = Overlays.Count > 0 ? Overlays.Max(o => o.StartTime + o.Duration) : TimeSpan.Zero;
            var maxCaption = Captions.Count > 0 ? Captions.Max(c => c.EndTime) : TimeSpan.Zero;
            var maxVal = maxTrack > maxOverlay ? maxTrack : maxOverlay;
            return maxVal > maxCaption ? maxVal : maxCaption;
        }
    }

    public static Project CreateDefault(string name = "New Video Project")
    {
        var project = new Project
        {
            Name = name,
            CanvasWidth = 1920,
            CanvasHeight = 1080,
            FrameRate = 30.0
        };

        // Standard Movie Maker default layout:
        // Track 0: Main Video Track
        project.Tracks.Add(new Track
        {
            Name = "Video 1",
            Type = TrackType.Video,
            Order = 0
        });

        // Track 1: Music / Audio Track
        project.Tracks.Add(new Track
        {
            Name = "Audio / Music",
            Type = TrackType.Audio,
            Order = 1
        });

        return project;
    }
}
