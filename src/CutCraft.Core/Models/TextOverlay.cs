using System.Text.Json.Serialization;

namespace CutCraft.Core.Models;

public enum TitleCardStyle
{
    Custom,
    LowerThird,
    CinematicHeader,
    BoldHeadline,
    Minimalist,
    NeonGlow,
    VintageRetro
}

public enum TextAnimationType
{
    None,
    Fade,
    SlideUp,
    SlideLeft,
    ZoomIn,
    Typewriter
}

public enum OverlayAlignment
{
    Center,
    Left,
    Right
}

public class TextOverlay
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Text { get; set; } = "Title Text";
    public string? SubtitleText { get; set; }
    public string FontName { get; set; } = "Segoe UI";
    public double FontSize { get; set; } = 48.0;
    public string ColorHex { get; set; } = "#FFFFFFFF";
    public string? BackgroundColorHex { get; set; }
    public double PositionX { get; set; } = 0.5; // Normalized (0.0 to 1.0)
    public double PositionY { get; set; } = 0.5; // Normalized (0.0 to 1.0)
    public TimeSpan StartTime { get; set; } = TimeSpan.Zero;
    public TimeSpan Duration { get; set; } = TimeSpan.FromSeconds(4.0);

    // Styling & Presentation
    public TitleCardStyle Style { get; set; } = TitleCardStyle.Custom;
    public TextAnimationType Animation { get; set; } = TextAnimationType.Fade;
    public double AnimationDuration { get; set; } = 0.6; // Seconds for in/out transitions
    public OverlayAlignment Alignment { get; set; } = OverlayAlignment.Center;
    public bool IsBold { get; set; } = false;
    public bool IsItalic { get; set; } = false;
    public string? BorderColorHex { get; set; }
    public int BorderWidth { get; set; } = 0;
    public string? ShadowColorHex { get; set; }
    public int ShadowDepth { get; set; } = 0;
    public int BoxPadding { get; set; } = 12;

    [JsonIgnore]
    public TimeSpan EndTime => StartTime + Duration;

    public TextOverlay Clone()
    {
        return new TextOverlay
        {
            Id = Guid.NewGuid().ToString("N"),
            Text = Text,
            SubtitleText = SubtitleText,
            FontName = FontName,
            FontSize = FontSize,
            ColorHex = ColorHex,
            BackgroundColorHex = BackgroundColorHex,
            PositionX = PositionX,
            PositionY = PositionY,
            StartTime = StartTime,
            Duration = Duration,
            Style = Style,
            Animation = Animation,
            AnimationDuration = AnimationDuration,
            Alignment = Alignment,
            IsBold = IsBold,
            IsItalic = IsItalic,
            BorderColorHex = BorderColorHex,
            BorderWidth = BorderWidth,
            ShadowColorHex = ShadowColorHex,
            ShadowDepth = ShadowDepth,
            BoxPadding = BoxPadding
        };
    }
}

public static class TitleCardPresets
{
    public static TextOverlay CreateLowerThird(string title = "Presenter Name", string subtitle = "Title / Designation", TimeSpan? start = null, TimeSpan? duration = null)
    {
        return new TextOverlay
        {
            Text = title,
            SubtitleText = subtitle,
            FontName = "Segoe UI",
            FontSize = 36.0,
            ColorHex = "#FFFFFFFF",
            BackgroundColorHex = "#CC111827", // Translucent slate dark
            PositionX = 0.08,
            PositionY = 0.80,
            StartTime = start ?? TimeSpan.Zero,
            Duration = duration ?? TimeSpan.FromSeconds(5.0),
            Style = TitleCardStyle.LowerThird,
            Animation = TextAnimationType.SlideUp,
            AnimationDuration = 0.5,
            Alignment = OverlayAlignment.Left,
            IsBold = true,
            BorderColorHex = "#FF00BCF9", // Cyan accent bar
            BorderWidth = 4,
            BoxPadding = 14
        };
    }

    public static TextOverlay CreateCinematicHeader(string title = "EPISODE TITLE", TimeSpan? start = null, TimeSpan? duration = null)
    {
        return new TextOverlay
        {
            Text = title.ToUpperInvariant(),
            SubtitleText = null,
            FontName = "Georgia",
            FontSize = 62.0,
            ColorHex = "#FFF5E6C8", // Warm champagne gold
            BackgroundColorHex = null,
            PositionX = 0.5,
            PositionY = 0.45,
            StartTime = start ?? TimeSpan.Zero,
            Duration = duration ?? TimeSpan.FromSeconds(4.5),
            Style = TitleCardStyle.CinematicHeader,
            Animation = TextAnimationType.Fade,
            AnimationDuration = 1.0,
            Alignment = OverlayAlignment.Center,
            IsBold = false,
            IsItalic = false,
            ShadowColorHex = "#AA000000",
            ShadowDepth = 3
        };
    }

    public static TextOverlay CreateBoldHeadline(string title = "BREAKING HIGHLIGHT", TimeSpan? start = null, TimeSpan? duration = null)
    {
        return new TextOverlay
        {
            Text = title.ToUpperInvariant(),
            SubtitleText = null,
            FontName = "Impact",
            FontSize = 56.0,
            ColorHex = "#FFFFFFFF",
            BackgroundColorHex = "#EE0F172A",
            PositionX = 0.5,
            PositionY = 0.5,
            StartTime = start ?? TimeSpan.Zero,
            Duration = duration ?? TimeSpan.FromSeconds(4.0),
            Style = TitleCardStyle.BoldHeadline,
            Animation = TextAnimationType.ZoomIn,
            AnimationDuration = 0.4,
            Alignment = OverlayAlignment.Center,
            IsBold = true,
            BorderColorHex = "#FFFFCC00", // Yellow highlight border
            BorderWidth = 3,
            BoxPadding = 16
        };
    }

    public static TextOverlay CreateMinimalist(string title = "Chapter One: The Beginning", TimeSpan? start = null, TimeSpan? duration = null)
    {
        return new TextOverlay
        {
            Text = title,
            SubtitleText = null,
            FontName = "Segoe UI",
            FontSize = 38.0,
            ColorHex = "#FFF1F5F9",
            BackgroundColorHex = null,
            PositionX = 0.5,
            PositionY = 0.85,
            StartTime = start ?? TimeSpan.Zero,
            Duration = duration ?? TimeSpan.FromSeconds(3.5),
            Style = TitleCardStyle.Minimalist,
            Animation = TextAnimationType.Fade,
            AnimationDuration = 0.6,
            Alignment = OverlayAlignment.Center,
            IsBold = false,
            ShadowColorHex = "#CC000000",
            ShadowDepth = 2
        };
    }

    public static TextOverlay CreateNeonGlow(string title = "CYBER SOUNDS", TimeSpan? start = null, TimeSpan? duration = null)
    {
        return new TextOverlay
        {
            Text = title,
            SubtitleText = null,
            FontName = "Consolas",
            FontSize = 52.0,
            ColorHex = "#FF00F0FF", // Bright Neon Cyan
            BackgroundColorHex = "#DD050814",
            PositionX = 0.5,
            PositionY = 0.46,
            StartTime = start ?? TimeSpan.Zero,
            Duration = duration ?? TimeSpan.FromSeconds(4.0),
            Style = TitleCardStyle.NeonGlow,
            Animation = TextAnimationType.Fade,
            AnimationDuration = 0.5,
            Alignment = OverlayAlignment.Center,
            IsBold = true,
            BorderColorHex = "#FFFF007F", // Neon Magenta glow border
            BorderWidth = 3,
            ShadowColorHex = "#FF00F0FF",
            ShadowDepth = 4,
            BoxPadding = 14
        };
    }

    public static TextOverlay CreateVintageRetro(string title = "Classic Chronicles", TimeSpan? start = null, TimeSpan? duration = null)
    {
        return new TextOverlay
        {
            Text = title,
            SubtitleText = null,
            FontName = "Times New Roman",
            FontSize = 48.0,
            ColorHex = "#FFFFE0B2", // Warm amber
            BackgroundColorHex = "#CC2B1B17", // Vintage dark sepia
            PositionX = 0.5,
            PositionY = 0.5,
            StartTime = start ?? TimeSpan.Zero,
            Duration = duration ?? TimeSpan.FromSeconds(4.0),
            Style = TitleCardStyle.VintageRetro,
            Animation = TextAnimationType.Fade,
            AnimationDuration = 0.8,
            Alignment = OverlayAlignment.Center,
            IsBold = false,
            IsItalic = true,
            BorderColorHex = "#FFB8860B", // Dark golden rod
            BorderWidth = 2,
            BoxPadding = 12
        };
    }
}
