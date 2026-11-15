namespace CutCraft.Core.Models;

public class TextOverlay
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Text { get; set; } = string.Empty;
    public string FontName { get; set; } = "Segoe UI";
    public double FontSize { get; set; } = 48.0;
    public string ColorHex { get; set; } = "#FFFFFFFF";
    public string? BackgroundColorHex { get; set; }
    public double PositionX { get; set; } = 0.5; // Normalized (0.0 to 1.0)
    public double PositionY { get; set; } = 0.85; // Normalized (0.0 to 1.0)
    public TimeSpan StartTime { get; set; } = TimeSpan.Zero;
    public TimeSpan Duration { get; set; } = TimeSpan.FromSeconds(5.0);
}
