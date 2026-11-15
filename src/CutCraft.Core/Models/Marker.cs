namespace CutCraft.Core.Models;

public class TimelineMarker
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public TimeSpan Time { get; set; } = TimeSpan.Zero;
    public string Label { get; set; } = "Marker";
    public string ColorHex { get; set; } = "#FFCC00";
    public string? Comment { get; set; }

    public TimelineMarker() { }

    public TimelineMarker(TimeSpan time, string label = "Marker", string colorHex = "#FFCC00")
    {
        Time = time;
        Label = label;
        ColorHex = colorHex;
    }
}
