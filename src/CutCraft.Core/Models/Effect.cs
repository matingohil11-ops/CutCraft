namespace CutCraft.Core.Models;

public enum EffectType
{
    Brightness,
    Contrast,
    Saturation,
    BlackAndWhite,
    Blur,
    FadeIn,
    FadeOut
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
}
