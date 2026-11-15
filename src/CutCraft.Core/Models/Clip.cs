using System.Text.Json.Serialization;

namespace CutCraft.Core.Models;

public class Clip
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "Untitled Clip";
    public string AssetId { get; set; } = string.Empty;
    public string AssetPath { get; set; } = string.Empty;
    public AssetType Type { get; set; } = AssetType.Video;

    /// <summary>
    /// Start point inside the source asset.
    /// </summary>
    public TimeSpan InPoint { get; set; } = TimeSpan.Zero;

    /// <summary>
    /// End point inside the source asset.
    /// </summary>
    public TimeSpan OutPoint { get; set; } = TimeSpan.FromSeconds(10);

    /// <summary>
    /// Time where this clip starts on the timeline track.
    /// </summary>
    public TimeSpan TimelineStart { get; set; } = TimeSpan.Zero;

    /// <summary>
    /// Playback speed multiplier (1.0 = normal, 0.5 = 2x slow motion, 2.0 = 2x speed).
    /// </summary>
    public double Speed { get; set; } = 1.0;

    /// <summary>
    /// Duration of the clip on the timeline considering speed.
    /// </summary>
    [JsonIgnore]
    public TimeSpan TimelineDuration
    {
        get
        {
            var rawDuration = OutPoint - InPoint;
            if (rawDuration <= TimeSpan.Zero) return TimeSpan.FromMilliseconds(100);
            var safeSpeed = Math.Max(0.1, Speed);
            return TimeSpan.FromSeconds(rawDuration.TotalSeconds / safeSpeed);
        }
    }

    [JsonIgnore]
    public TimeSpan TimelineEnd => TimelineStart + TimelineDuration;

    /// <summary>
    /// Volume multiplier (1.0 = 100%, 0.0 = muted).
    /// </summary>
    public double Volume { get; set; } = 1.0;

    public bool IsMuted { get; set; } = false;

    public double Opacity { get; set; } = 1.0;

    // PiP / Overlay positioning
    public double PositionX { get; set; } = 0.0; // 0.0 to 1.0 normalized or 0 for full screen
    public double PositionY { get; set; } = 0.0;
    public double Scale { get; set; } = 1.0; // 1.0 = full scale, 0.25 = quarter screen (PiP)

    public List<ClipEffect> Effects { get; set; } = new();

    public Transition? TransitionIn { get; set; }
    public Transition? TransitionOut { get; set; }

    // Phase 8: Audio Fade In & Fade Out
    public double AudioFadeIn { get; set; } = 0.0; // Seconds
    public double AudioFadeOut { get; set; } = 0.0; // Seconds

    // Phase 9: Clip Transformations & Orientation
    public int Rotation { get; set; } = 0; // 0, 90, 180, 270 degrees
    public bool FlipHorizontal { get; set; } = false;
    public bool FlipVertical { get; set; } = false;
    public PanZoomType PanZoom { get; set; } = PanZoomType.None;

    public Clip Clone()
    {
        return new Clip
        {
            Id = Guid.NewGuid().ToString("N"),
            Name = this.Name,
            AssetId = this.AssetId,
            AssetPath = this.AssetPath,
            Type = this.Type,
            InPoint = this.InPoint,
            OutPoint = this.OutPoint,
            TimelineStart = this.TimelineStart,
            Speed = this.Speed,
            Volume = this.Volume,
            IsMuted = this.IsMuted,
            Opacity = this.Opacity,
            PositionX = this.PositionX,
            PositionY = this.PositionY,
            Scale = this.Scale,
            AudioFadeIn = this.AudioFadeIn,
            AudioFadeOut = this.AudioFadeOut,
            Rotation = this.Rotation,
            FlipHorizontal = this.FlipHorizontal,
            FlipVertical = this.FlipVertical,
            PanZoom = this.PanZoom,
            Effects = this.Effects.Select(e => new ClipEffect(e.Type, e.Parameters)).ToList(),
            TransitionIn = this.TransitionIn != null ? new Transition { Type = this.TransitionIn.Type, Duration = this.TransitionIn.Duration } : null,
            TransitionOut = this.TransitionOut != null ? new Transition { Type = this.TransitionOut.Type, Duration = this.TransitionOut.Duration } : null
        };
    }
}
