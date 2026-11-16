using System.Text.Json.Serialization;

namespace CutCraft.Core.Models;

public enum CaptionStylePreset
{
    Default,
    YellowCinema,
    HighContrastWhite,
    BoxedDark,
    TopHeader
}

public class CaptionItem
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public int Index { get; set; } = 1;
    public TimeSpan StartTime { get; set; } = TimeSpan.Zero;
    public TimeSpan EndTime { get; set; } = TimeSpan.FromSeconds(3);
    public string Text { get; set; } = string.Empty;
    public string? Speaker { get; set; }
    public CaptionStylePreset StylePreset { get; set; } = CaptionStylePreset.Default;

    [JsonIgnore]
    public TimeSpan Duration => EndTime > StartTime ? EndTime - StartTime : TimeSpan.Zero;

    [JsonIgnore]
    public double CharactersPerSecond => Duration.TotalSeconds > 0.05
        ? (Text?.Length ?? 0) / Duration.TotalSeconds
        : 0.0;

    [JsonIgnore]
    public bool HasReadingSpeedWarning => CharactersPerSecond > 26.0;

    [JsonIgnore]
    public bool HasOverlapWarning { get; set; } = false;

    public CaptionItem() { }

    public CaptionItem(int index, TimeSpan start, TimeSpan end, string text, string? speaker = null, CaptionStylePreset style = CaptionStylePreset.Default)
    {
        Index = index;
        StartTime = start;
        EndTime = end;
        Text = text;
        Speaker = speaker;
        StylePreset = style;
    }

    public CaptionItem Clone()
    {
        return new CaptionItem
        {
            Id = Guid.NewGuid().ToString("N"),
            Index = Index,
            StartTime = StartTime,
            EndTime = EndTime,
            Text = Text,
            Speaker = Speaker,
            StylePreset = StylePreset
        };
    }
}
