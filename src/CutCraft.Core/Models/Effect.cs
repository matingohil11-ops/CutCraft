namespace CutCraft.Core.Models;

public enum EffectType
{
    Brightness,
    Contrast,
    Saturation,
    BlackAndWhite,
    Blur,
    Sharpen,
    Sepia,
    Invert,
    Vignette,
    FadeIn,
    FadeOut,
    AudioFadeIn,
    AudioFadeOut,
    Hue
}

public class ClipEffect
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public EffectType Type { get; set; }
    public Dictionary<string, double> Parameters { get; set; } = new();

    public ClipEffect() { }

    public ClipEffect(EffectType type, Dictionary<string, double>? parameters = null)
    {
        Type = type;
        if (parameters != null)
        {
            Parameters = new Dictionary<string, double>(parameters);
        }
    }

    public static ClipEffect CreateFadeIn(double seconds = 1.0) =>
        new(EffectType.FadeIn, new Dictionary<string, double> { { "duration", seconds } });

    public static ClipEffect CreateFadeOut(double seconds = 1.0) =>
        new(EffectType.FadeOut, new Dictionary<string, double> { { "duration", seconds } });

    public static ClipEffect CreateAudioFadeIn(double seconds = 1.0) =>
        new(EffectType.AudioFadeIn, new Dictionary<string, double> { { "duration", seconds } });

    public static ClipEffect CreateAudioFadeOut(double seconds = 1.0) =>
        new(EffectType.AudioFadeOut, new Dictionary<string, double> { { "duration", seconds } });

    public static ClipEffect CreateBrightness(double value = 0.1) =>
        new(EffectType.Brightness, new Dictionary<string, double> { { "value", value } });

    public static ClipEffect CreateContrast(double value = 1.2) =>
        new(EffectType.Contrast, new Dictionary<string, double> { { "value", value } });

    public static ClipEffect CreateSaturation(double value = 1.5) =>
        new(EffectType.Saturation, new Dictionary<string, double> { { "value", value } });

    public static ClipEffect CreateBlackAndWhite() =>
        new(EffectType.BlackAndWhite);

    public static ClipEffect CreateBlur(double radius = 5.0) =>
        new(EffectType.Blur, new Dictionary<string, double> { { "radius", radius } });

    public static ClipEffect CreateSepia() =>
        new(EffectType.Sepia);

    public static ClipEffect CreateVignette(double angle = 0.5) =>
        new(EffectType.Vignette, new Dictionary<string, double> { { "angle", angle } });
}
